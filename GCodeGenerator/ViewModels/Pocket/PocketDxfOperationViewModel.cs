#nullable enable
using System.Threading;
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
        private readonly DxfImportFlow _importFlow;
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
            // Общий поток импорта двух DXF-диалогов: выбор файла, чтение
            // в фоне, сообщения. Диалогу остаётся своя геометрия. Команда
            // отменяема: токен доходит до перебора циклов в ядре, где
            // сложный чертёж занимает заметное время.
            _importFlow = new DxfImportFlow(
                localizationManager, messageService, fileDialogService, dxfImportService, logger);
            _logger = logger ?? NullAppLogger.Instance;
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

        private async Task ImportDxfFileAsync(CancellationToken cancellation)
        {
            // Импорт правит открытую в окне операцию: без неё импортировать
            // некуда, и команда до этого места не доходит.
            if (Operation == null)
                return;

            var import = await _importFlow.ImportPocketAsync(cancellation);
            if (import == null)
                return;

            var (fileName, closedContours) = import.Value;
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
    }
}