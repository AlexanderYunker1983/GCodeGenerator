#nullable enable
using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GCodeGenerator.Diagnostics;

namespace GCodeGenerator.Services
{
    /// <summary>
    /// Не допускает два процесса к общему recovery-файлу и передаёт запрос
    /// второго запуска первому процессу через именованный канал.
    /// </summary>
    internal sealed class SingleInstanceCoordinator : IDisposable
    {
        private const int MaximumRequestBytes = 256 * 1024;
        internal static readonly TimeSpan DefaultRequestReadTimeout = TimeSpan.FromSeconds(2);

        private readonly string _lockPath;
        private readonly string _pipeName;
        private readonly Action<string?> _requestHandler;
        private readonly IAppLogger _logger;
        private readonly TimeSpan _requestReadTimeout;
        private readonly CancellationTokenSource _shutdown = new CancellationTokenSource();
        private readonly object _sync = new object();
        private FileStream? _instanceLock;
        private NamedPipeServerStream? _activeServer;
        private Task? _listenerTask;
        private bool _disposed;

        internal SingleInstanceCoordinator(
            string lockPath,
            string pipeName,
            Action<string?> requestHandler,
            IAppLogger? logger = null,
            TimeSpan? requestReadTimeout = null)
        {
            if (string.IsNullOrWhiteSpace(lockPath))
                throw new ArgumentException("Instance lock path is not specified.", nameof(lockPath));
            if (string.IsNullOrWhiteSpace(pipeName))
                throw new ArgumentException("Instance pipe name is not specified.", nameof(pipeName));

            _lockPath = Path.GetFullPath(lockPath);
            _pipeName = pipeName;
            _requestHandler = requestHandler ?? throw new ArgumentNullException(nameof(requestHandler));
            _logger = logger ?? NullAppLogger.Instance;
            _requestReadTimeout = requestReadTimeout ?? DefaultRequestReadTimeout;
            if (_requestReadTimeout <= TimeSpan.Zero
                || _requestReadTimeout.TotalMilliseconds > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(requestReadTimeout));
            }
        }

        internal static SingleInstanceCoordinator CreateDefault(
            Action<string?> requestHandler,
            IAppLogger? logger = null)
        {
            var recoveryDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GCodeGenerator",
                "recovery");
            var identity = Path.GetFullPath(recoveryDirectory).ToUpperInvariant();
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
            return new SingleInstanceCoordinator(
                Path.Combine(recoveryDirectory, "instance.lock"),
                "GCodeGenerator.SingleInstance." + hash.Substring(0, 24),
                requestHandler,
                logger);
        }

        /// <summary>Захватывает межпроцессный файловый lock до создания окна.</summary>
        internal bool TryAcquire()
        {
            ThrowIfDisposed();
            if (_instanceLock != null)
                return true;

            try
            {
                var directory = Path.GetDirectoryName(_lockPath)
                    ?? throw new InvalidOperationException("Instance lock path has no directory.");
                Directory.CreateDirectory(directory);
                _instanceLock = new FileStream(
                    _lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None);
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        /// <summary>Начинает принимать открытия файлов после готовности UI.</summary>
        internal void StartListening()
        {
            ThrowIfDisposed();
            if (_instanceLock == null)
                throw new InvalidOperationException("The instance lock is not owned.");

            lock (_sync)
            {
                if (_listenerTask != null)
                    return;
                _listenerTask = ListenAsync(_shutdown.Token);
            }
        }

        /// <summary>
        /// Передаёт первому процессу абсолютный путь либо пустой запрос,
        /// означающий активацию уже открытого окна.
        /// </summary>
        internal bool TryForward(string? projectFile, TimeSpan timeout)
        {
            ThrowIfDisposed();
            if (timeout <= TimeSpan.Zero || timeout.TotalMilliseconds > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(timeout));

            var payload = Encoding.UTF8.GetBytes(projectFile ?? string.Empty);
            if (payload.Length > MaximumRequestBytes)
                throw new ArgumentException("Forwarded project path is too long.", nameof(projectFile));

            try
            {
                using var client = new NamedPipeClientStream(
                    ".",
                    _pipeName,
                    PipeDirection.Out,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                client.Connect(checked((int)timeout.TotalMilliseconds));
                using var writer = new BinaryWriter(client, Encoding.UTF8, leaveOpen: true);
                writer.Write(payload.Length);
                writer.Write(payload);
                writer.Flush();
                return true;
            }
            catch (Exception ex) when (
                ex is IOException
                || ex is TimeoutException
                || ex is UnauthorizedAccessException)
            {
                _logger.Error("Forwarding a request to the running application failed.", ex);
                return false;
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _shutdown.Cancel();

            lock (_sync)
            {
                _activeServer?.Dispose();
                _activeServer = null;
            }

            try
            {
                _listenerTask?.Wait(TimeSpan.FromSeconds(2));
            }
            catch (AggregateException ex) when (
                ex.InnerException is OperationCanceledException
                || ex.InnerException is ObjectDisposedException)
            {
                // Ожидаемое завершение WaitForConnectionAsync.
            }

            _instanceLock?.Dispose();
            _instanceLock = null;
            _shutdown.Dispose();
        }

        private async Task ListenAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                NamedPipeServerStream? server = null;
                try
                {
                    server = new NamedPipeServerStream(
                        _pipeName,
                        PipeDirection.In,
                        1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                    lock (_sync)
                        _activeServer = server;

                    await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                    using var requestCancellation =
                        CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    requestCancellation.CancelAfter(_requestReadTimeout);

                    var lengthBytes = new byte[sizeof(int)];
                    await server.ReadExactlyAsync(lengthBytes, requestCancellation.Token)
                        .ConfigureAwait(false);
                    var length = BinaryPrimitives.ReadInt32LittleEndian(lengthBytes);
                    if (length < 0 || length > MaximumRequestBytes)
                        throw new InvalidDataException("Forwarded request has an invalid length.");
                    var payload = new byte[length];
                    await server.ReadExactlyAsync(payload, requestCancellation.Token)
                        .ConfigureAwait(false);

                    var projectFile = Encoding.UTF8.GetString(payload);
                    _requestHandler(projectFile.Length == 0 ? null : projectFile);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (OperationCanceledException)
                {
                    _logger.Warning("Receiving a request from another application instance timed out.");
                }
                catch (Exception ex)
                {
                    _logger.Error("Receiving a request from another application instance failed.", ex);
                }
                finally
                {
                    lock (_sync)
                    {
                        if (ReferenceEquals(_activeServer, server))
                            _activeServer = null;
                    }
                    server?.Dispose();
                }
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SingleInstanceCoordinator));
        }
    }
}
