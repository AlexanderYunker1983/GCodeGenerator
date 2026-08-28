#nullable enable
using System;
using System.Diagnostics;
using GCodeGenerator.Diagnostics;

namespace GCodeGenerator.Services
{
    /// <summary>
    /// Показ файла и открытие ссылки средствами оболочки.
    ///
    /// Отдельный контракт нужен view-моделям: сами они ни с файловой системой,
    /// ни с запуском процессов не работают — это правило архитектуры, которое
    /// держат тесты. Окно «О программе» при этом обязано уметь показать журнал
    /// работы и открыть страницу продукта.
    /// </summary>
    public interface IShellService
    {
        /// <summary>
        /// Открывает проводник на указанном файле, выделив его. Если файла
        /// ещё нет — открывает его каталог.
        /// </summary>
        /// <param name="path">Путь к файлу.</param>
        void ShowFile(string? path);

        /// <summary>Открывает ссылку в браузере по умолчанию.</summary>
        /// <param name="url">Адрес страницы.</param>
        void OpenUrl(string? url);
    }

    /// <summary>
    /// Реализация поверх оболочки Windows.
    ///
    /// Отказ оболочки не должен ронять программу: пользователь нажал
    /// «показать журнал», а не «сделай или умри». Причина остаётся в журнале.
    /// </summary>
    public sealed class ShellService : IShellService
    {
        private readonly IAppLogger _logger;

        public ShellService(IAppLogger? logger = null)
        {
            _logger = logger ?? NullAppLogger.Instance;
        }

        /// <inheritdoc />
        public void ShowFile(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            // /select показывает файл в проводнике выделенным; без него
            // открылся бы сам файл, а журнал открывать в блокноте незачем.
            Start("explorer.exe", $"/select,\"{path}\"", path!);
        }

        /// <inheritdoc />
        public void OpenUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return;

            Start(url!, arguments: null, description: url!);
        }

        /// <summary>Запускает оболочку, не пробрасывая её отказ наружу.</summary>
        /// <param name="fileName">Что запускать.</param>
        /// <param name="arguments">Аргументы или <c>null</c>.</param>
        /// <param name="description">Что именно открывали — для журнала.</param>
        private void Start(string fileName, string? arguments, string description)
        {
            try
            {
                var startInfo = new ProcessStartInfo(fileName) { UseShellExecute = true };
                if (arguments != null)
                    startInfo.Arguments = arguments;

                Process.Start(startInfo);
            }
            catch (Exception failure)
            {
                _logger.Error($"Shell could not open: {description}", failure);
            }
        }
    }
}
