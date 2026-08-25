using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GCodeGenerator.Diagnostics;
using GCodeGenerator.Localization;
using GCodeGenerator.Import;
using GCodeGenerator.Models;
using GCodeGenerator.Services;

namespace GCodeGenerator.ViewModels.PocketMill
{
    /// <summary>
    /// Диалог обработки контура из чертежа: импорт DXF и общие параметры
    /// профильной обработки. Геометрия приходит из файла, поэтому собственных
    /// размеров у операции нет.
    /// </summary>
    public partial class ProfileDxfOperationViewModel
        : ProfileOperationEditorViewModelBase<ProfileDxfOperation>, IHasDisplayName
    {
        private readonly ILocalizationManager _localizationManager;
        private readonly IMessageService _messageService;
        private readonly IFileDialogService _fileDialogService;
        private readonly IDxfImportService _dxfImportService;
        private readonly IAppLogger _logger;

        [ObservableProperty]
        private string _displayName;

        /// <summary>Путь к импортированному чертежу.</summary>
        [ObservableProperty]
        private string _filePath;

        /// <summary>Итог импорта: сколько отрезков контура получено.</summary>
        [ObservableProperty]
        private string _importInfo;

        public ProfileDxfOperationViewModel(
            ILocalizationManager localizationManager,
            IMessageService messageService,
            IFileDialogService fileDialogService,
            IDxfImportService dxfImportService,
            IAppLogger logger = null)
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
            DisplayName = _localizationManager?.GetString("ProfileDxfName") ?? "ProfileDxfName";

            // Пункт 7.3: операция по умолчанию для автономного создания
            // (в потоках добавления/редактирования операцию задаёт фабрика).
            if (Operation == null)
                Operation = new ProfileDxfOperation();
        }

        public ICommand ImportDxfCommand { get; }

        protected override void OnOperationChanged(ProfileDxfOperation operation)
        {
            base.OnOperationChanged(operation);

            FilePath = operation.DxfFilePath;
            if (operation.Polylines != null && operation.Polylines.Count > 0)
            {
                var lineCount = operation.Polylines.Sum(p => p?.Points?.Count > 1 ? p.Points.Count - 1 : 0);
                var infoTemplate = _localizationManager?.GetString("DxfImportInfo") ?? "DxfImportInfo";
                ImportInfo = string.Format(infoTemplate, lineCount);
            }
            else
            {
                ImportInfo = null;
            }
        }

        private async Task ImportDxfFileAsync()
        {
            var title = _localizationManager?.GetString("DxfImportDialogTitle") ?? "DxfImportDialogTitle";
            var fileName = _fileDialogService?.ShowOpenDialog(title, "DXF files (*.dxf)|*.dxf|All files (*.*)|*.*", "dxf");
            if (fileName == null)
                return;

            try
            {
                var polylines = await Task.Run(() => _dxfImportService.ReadProfilePolylines(fileName));
                if (polylines.Count == 0)
                {
                    _logger.Warning($"DXF import found no profile geometry: {fileName}");
                    var msg = _localizationManager?.GetString("DxfImportNoLines") ?? "DxfImportNoLines";
                    _messageService?.ShowInfo(msg, title);
                    return;
                }

                Operation.Polylines = polylines;
                Operation.DxfFilePath = fileName;
                // Пункт 7.2 плана: импорт DXF перерисовывает 2D-превью
                // (Polylines — авто-свойство, без PropertyChanged).
                Operation.NotifyContentChanged();
                FilePath = fileName;
                var lineCount = polylines.Sum(p => Math.Max(0, p.Points.Count - 1));
                var infoTemplate = _localizationManager?.GetString("DxfImportInfo") ?? "DxfImportInfo";
                ImportInfo = string.Format(infoTemplate, lineCount);
                _logger.Info($"DXF imported for profile: {fileName} ({polylines.Count} polyline(s), {lineCount} segment(s))");
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