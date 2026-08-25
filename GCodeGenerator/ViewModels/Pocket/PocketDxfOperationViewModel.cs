#nullable enable
using System;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GCodeGenerator.Diagnostics;
using GCodeGenerator.Localization;
using GCodeGenerator.Import;
using GCodeGenerator.Models;
using GCodeGenerator.Services;

namespace GCodeGenerator.ViewModels.Pocket
{
    /// <summary>
    /// Диалог кармана по контуру из чертежа: импорт DXF и общие параметры
    /// выборки. Геометрия приходит из файла, поэтому собственных размеров
    /// у операции нет.
    /// </summary>
    public partial class PocketDxfOperationViewModel
        : PocketOperationEditorViewModelBase<PocketDxfOperation>, IHasDisplayName
    {
        private readonly ILocalizationManager? _localizationManager;
        private readonly IMessageService _messageService;
        private readonly IFileDialogService _fileDialogService;
        private readonly IDxfImportService _dxfImportService;
        private readonly IAppLogger _logger;

        [ObservableProperty]
        private string _displayName = string.Empty;

        /// <summary>Путь к импортированному чертежу.</summary>
        [ObservableProperty]
        private string _filePath = string.Empty;

        /// <summary>Итог импорта: сколько замкнутых контуров получено.</summary>
        [ObservableProperty]
        private string? _importInfo;

        public PocketDxfOperationViewModel(
            ILocalizationManager? localizationManager,
            IMessageService messageService,
            IFileDialogService fileDialogService,
            IDxfImportService dxfImportService,
            IAppLogger? logger = null)
        {
            _localizationManager = localizationManager;
            _messageService = messageService;
            _fileDialogService = fileDialogService;
            _dxfImportService = dxfImportService ?? throw new ArgumentNullException(nameof(dxfImportService));
            _logger = logger ?? NullAppLogger.Instance;
            // Пункт 8.4 плана: импорт DXF — async: разбор файла выполняется в пуле,
            // поэтому интерфейс не замирает даже на больших чертежах.
            ImportDxfCommand = new AsyncRelayCommand(ImportDxfFileAsync);
            // Пункт 8.3: без захардкоженного фолбэка — отсутствующий ключ
            // вернёт «?Key?» (лог — в LocalizationManager).
            DisplayName = _localizationManager?.GetString("PocketDxfName") ?? "PocketDxfName";

            // Пункт 7.3: операция по умолчанию для автономного создания
            // (в потоках добавления/редактирования операцию задаёт фабрика).
            if (Operation == null)
                Operation = new PocketDxfOperation();
        }

        public ICommand ImportDxfCommand { get; }

        protected override void OnOperationChanged(PocketDxfOperation operation)
        {
            base.OnOperationChanged(operation);

            FilePath = operation.DxfFilePath;
            if (operation.ClosedContours != null && operation.ClosedContours.Count > 0)
            {
                var infoTemplate = _localizationManager?.GetString("DxfImportContoursInfo") ?? "DxfImportContoursInfo";
                ImportInfo = string.Format(infoTemplate, operation.ClosedContours.Count);
            }
            else
            {
                ImportInfo = null;
            }
        }

        private async Task ImportDxfFileAsync()
        {
            // Импорт правит открытую в окне операцию: без неё импортировать
            // некуда, и команда до этого места не доходит.
            if (Operation == null)
                return;

            var title = _localizationManager?.GetString("DxfImportDialogTitle") ?? "DxfImportDialogTitle";
            var fileName = _fileDialogService?.ShowOpenDialog(title, "DXF files (*.dxf)|*.dxf|All files (*.*)|*.*", "dxf");
            if (fileName == null)
                return;

            try
            {
                var closedContours = await Task.Run(() => _dxfImportService.ReadPocketClosedContours(fileName));
                if (closedContours.Count == 0)
                {
                    _logger.Warning($"DXF import found no closed contours: {fileName}");
                    var msg = _localizationManager?.GetString("DxfImportNoClosedContours") ?? "DxfImportNoClosedContours";
                    _messageService?.ShowInfo(msg, title);
                    return;
                }

                Operation.ClosedContours = closedContours;
                Operation.DxfFilePath = fileName;
                // Пункт 7.2 плана: импорт DXF перерисовывает 2D-превью
                // (ClosedContours — авто-свойство, без PropertyChanged).
                Operation.NotifyContentChanged();
                FilePath = fileName;
                var contourCount = closedContours.Count;
                var infoTemplate = _localizationManager?.GetString("DxfImportContoursInfo") ?? "DxfImportContoursInfo";
                ImportInfo = string.Format(infoTemplate, contourCount);
                _logger.Info($"DXF imported for pocket: {fileName} ({contourCount} closed contour(s))");
            }
            catch (Exception ex)
            {
                _logger.Error($"DXF import failed: {fileName}", ex);
                var msg = _localizationManager?.GetString("DxfImportFailed") ?? "DxfImportFailed";
                _messageService?.ShowError($"{msg} {ex.Message}", title);
            }
        }

    }
}