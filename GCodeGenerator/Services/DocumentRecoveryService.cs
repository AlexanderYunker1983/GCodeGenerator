#nullable enable
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GCodeGenerator.Diagnostics;
using GCodeGenerator.Persistence;

namespace GCodeGenerator.Services
{
    /// <summary>
    /// Debounced recovery-файл для завершения процесса, отключения питания и
    /// других случаев, в которых обработчик исключения не успевает сделать
    /// аварийный снимок.
    /// </summary>
    public sealed class DocumentRecoveryService : IDocumentRecoveryService
    {
        public static readonly TimeSpan DefaultDelay = TimeSpan.FromSeconds(2);

        private readonly IProjectFileService _projectFiles;
        private readonly IAppLogger _logger;
        private readonly TimeSpan _delay;
        private readonly SynchronizationContext? _uiContext;
        private readonly object _sync = new object();
        private readonly SemaphoreSlim _writeGate = new SemaphoreSlim(1, 1);
        private CancellationTokenSource? _pendingCancellation;
        private Task _pendingSave = Task.CompletedTask;

        public DocumentRecoveryService(IProjectFileService projectFiles, IAppLogger? logger = null)
            : this(
                projectFiles,
                logger,
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "GCodeGenerator",
                    "recovery",
                    "autosave.ygc"),
                DefaultDelay,
                SynchronizationContext.Current)
        {
        }

        internal DocumentRecoveryService(
            IProjectFileService projectFiles,
            IAppLogger? logger,
            string recoveryPath,
            TimeSpan delay,
            SynchronizationContext? uiContext)
        {
            _projectFiles = projectFiles ?? throw new ArgumentNullException(nameof(projectFiles));
            _logger = logger ?? NullAppLogger.Instance;
            RecoveryPath = Path.GetFullPath(recoveryPath ?? throw new ArgumentNullException(nameof(recoveryPath)));
            _delay = delay >= TimeSpan.Zero ? delay : throw new ArgumentOutOfRangeException(nameof(delay));
            _uiContext = uiContext;
        }

        public string RecoveryPath { get; }

        public string BackupPath => RecoveryPath + ".bak";

        public bool Exists => File.Exists(RecoveryPath);

        public bool BackupExists => File.Exists(BackupPath);

        public DateTimeOffset? SnapshotTimeUtc
        {
            get
            {
                try
                {
                    return File.Exists(RecoveryPath)
                        ? new DateTimeOffset(File.GetLastWriteTimeUtc(RecoveryPath))
                        : null;
                }
                catch
                {
                    // Время — пояснение в диалоге, а не условие доступа к
                    // снимку. Само открытие даст полную ошибку, если файл
                    // исчез или стал недоступен между проверками.
                    return null;
                }
            }
        }

        public string? QuarantineCorruptSnapshot()
        {
            _writeGate.Wait();
            try
            {
                if (!File.Exists(RecoveryPath))
                    return null;

                var quarantinePath = RecoveryPath
                    + ".corrupt-"
                    + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ", System.Globalization.CultureInfo.InvariantCulture)
                    + "-"
                    + Guid.NewGuid().ToString("N");
                File.Move(RecoveryPath, quarantinePath);
                _logger.Warning($"Corrupt project recovery snapshot quarantined: {quarantinePath}");
                return quarantinePath;
            }
            catch (Exception ex)
            {
                _logger.Error($"Quarantining project recovery snapshot failed: {RecoveryPath}", ex);
                return null;
            }
            finally
            {
                _writeGate.Release();
            }
        }

        public void Schedule(Func<string> snapshotFactory)
        {
            if (snapshotFactory == null)
                throw new ArgumentNullException(nameof(snapshotFactory));

            lock (_sync)
            {
                _pendingCancellation?.Cancel();
                var cancellation = new CancellationTokenSource();
                _pendingCancellation = cancellation;
                _pendingSave = SaveAfterDelayAsync(snapshotFactory, cancellation);
            }
        }

        public void Clear()
        {
            lock (_sync)
            {
                _pendingCancellation?.Cancel();
                _pendingCancellation = null;
                _pendingSave = Task.CompletedTask;
            }

            // Serialize deletion with an already running atomic write. A task
            // waiting behind us observes cancellation before it can write.
            _writeGate.Wait();
            try
            {
                DeleteIfExists(RecoveryPath);
                DeleteIfExists(BackupPath);
            }
            catch (Exception ex)
            {
                _logger.Error($"Clearing project recovery file failed: {RecoveryPath}", ex);
            }
            finally
            {
                _writeGate.Release();
            }
        }

        public Task WaitForPendingSaveAsync()
        {
            lock (_sync)
                return _pendingSave;
        }

        private async Task SaveAfterDelayAsync(
            Func<string> snapshotFactory,
            CancellationTokenSource cancellation)
        {
            try
            {
                await Task.Delay(_delay, cancellation.Token).ConfigureAwait(false);
                var json = await CaptureOnUiContextAsync(snapshotFactory, cancellation.Token).ConfigureAwait(false);
                cancellation.Token.ThrowIfCancellationRequested();

                await _writeGate.WaitAsync(cancellation.Token).ConfigureAwait(false);
                try
                {
                    cancellation.Token.ThrowIfCancellationRequested();
                    var directory = Path.GetDirectoryName(RecoveryPath)
                        ?? throw new InvalidOperationException("Recovery path has no directory.");
                    Directory.CreateDirectory(directory);
                    await Task.Run(
                        () => _projectFiles.SaveSerialized(RecoveryPath, json),
                        cancellation.Token).ConfigureAwait(false);
                }
                finally
                {
                    _writeGate.Release();
                }

                _logger.Info($"Project recovery snapshot saved: {RecoveryPath}");
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                // A newer document revision owns the recovery file.
            }
            catch (Exception ex)
            {
                // Autosave must not interrupt editing; the full reason stays
                // in the application log and the next edit retries it.
                _logger.Error($"Saving project recovery snapshot failed: {RecoveryPath}", ex);
            }
            finally
            {
                lock (_sync)
                {
                    if (ReferenceEquals(_pendingCancellation, cancellation))
                        _pendingCancellation = null;
                }
                cancellation.Dispose();
            }
        }

        private Task<string> CaptureOnUiContextAsync(
            Func<string> snapshotFactory,
            CancellationToken cancellationToken)
        {
            if (_uiContext == null || ReferenceEquals(SynchronizationContext.Current, _uiContext))
                return Task.FromResult(snapshotFactory());

            var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            _uiContext.Post(_ =>
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    completion.TrySetResult(snapshotFactory());
                }
                catch (OperationCanceledException)
                {
                    completion.TrySetCanceled(cancellationToken);
                }
                catch (Exception ex)
                {
                    completion.TrySetException(ex);
                }
            }, null);
            return completion.Task;
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
