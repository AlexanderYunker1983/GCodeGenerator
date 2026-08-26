#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GCodeGenerator.Diagnostics;
using GCodeGenerator.Import;
using GCodeGenerator.Localization;
using GCodeGenerator.Models;

namespace GCodeGenerator.Services
{
    /// <summary>
    /// Общий поток импорта чертежа для двух диалогов DXF — профиля и
    /// кармана: выбор файла, чтение в фоне, сообщения о пустом чертеже,
    /// отмене и сбое. Прежде поток был скопирован в оба диалога целиком
    /// и уже разошёлся в мелочах: карман узнал об отмене, профиль — нет.
    /// Диалогам остаётся то, что у них действительно разное: какая
    /// геометрия читается и куда она кладётся в операцию.
    /// </summary>
    public sealed class DxfImportFlow
    {
        private readonly ILocalizationManager? _localizationManager;
        private readonly IMessageService? _messageService;
        private readonly IFileDialogService? _fileDialogService;
        private readonly IDxfImportService _dxfImportService;
        private readonly IAppLogger _logger;

        public DxfImportFlow(
            ILocalizationManager? localizationManager,
            IMessageService? messageService,
            IFileDialogService? fileDialogService,
            IDxfImportService dxfImportService,
            IAppLogger? logger = null)
        {
            _localizationManager = localizationManager;
            _messageService = messageService;
            _fileDialogService = fileDialogService;
            _dxfImportService = dxfImportService ?? throw new ArgumentNullException(nameof(dxfImportService));
            _logger = logger ?? NullAppLogger.Instance;
        }

        /// <summary>
        /// Импорт геометрии профильной обработки; null — пользователь отменил
        /// выбор, чертёж пуст или чтение не удалось (пользователь уже уведомлён).
        /// </summary>
        public Task<(string FileName, List<Polyline2D> Polylines)?> ImportProfileAsync()
            => ImportAsync(
                (path, _) => _dxfImportService.ReadProfilePolylines(path),
                "DxfImportNoLines",
                "no profile geometry",
                CancellationToken.None);

        /// <summary>
        /// Импорт замкнутых контуров кармана; null — как у
        /// <see cref="ImportProfileAsync"/>, а также при отмене восстановления
        /// контуров: на сложном чертеже оно занимает время.
        /// </summary>
        public Task<(string FileName, List<Polyline2D> Contours)?> ImportPocketAsync(CancellationToken cancellation)
            => ImportAsync(
                (path, token) => _dxfImportService.ReadPocketClosedContours(path, token),
                "DxfImportNoClosedContours",
                "no closed contours",
                cancellation);

        private async Task<(string FileName, List<Polyline2D> Geometry)?> ImportAsync(
            Func<string, CancellationToken, List<Polyline2D>> read,
            string emptyMessageKey,
            string emptyLogReason,
            CancellationToken cancellation)
        {
            var title = Localize("DxfImportDialogTitle");
            var fileName = _fileDialogService?.ShowOpenDialog(title, Localize("DxfFileFilter"), "dxf");
            if (fileName == null)
                return null;

            try
            {
                // Пункт 8.4 плана: разбор файла выполняется в пуле, поэтому
                // интерфейс не замирает даже на больших чертежах.
                var geometry = await Task.Run(() => read(fileName, cancellation), cancellation);
                if (geometry.Count == 0)
                {
                    _logger.Warning($"DXF import found {emptyLogReason}: {fileName}");
                    _messageService?.ShowInfo(Localize(emptyMessageKey), title);
                    return null;
                }

                return (fileName, geometry);
            }
            catch (OperationCanceledException)
            {
                // Отменённый импорт — не ошибка: пользователь передумал,
                // сообщать не о чем.
                _logger.Info($"DXF import canceled: {fileName}");
                return null;
            }
            catch (Exception ex)
            {
                _logger.Error($"DXF import failed: {fileName}", ex);
                var message = Localize("DxfImportFailed");
                _messageService?.ShowError($"{message} {CoreErrorMessages.Describe(ex, _localizationManager)}", title);
                return null;
            }
        }

        // Пункт 8.3: без захардкоженного фолбэка — отсутствующий ключ
        // вернёт «?Key?» (лог — в LocalizationManager).
        private string Localize(string key)
            => _localizationManager?.GetString(key) ?? key;
    }
}
