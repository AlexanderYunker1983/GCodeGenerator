using CommunityToolkit.Mvvm.Input;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using GCodeGenerator.Models;
using GCodeGenerator.Localization;
using GCodeGenerator.Services;

namespace GCodeGenerator.ViewModels.PocketMill
{
    public class ProfileDxfOperationViewModel : OperationEditorViewModelBase<ProfileDxfOperation>, IHasDisplayName
    {
        private readonly ILocalizationManager _localizationManager;
        private readonly IDialogService _dialogService;
        private readonly IDxfImportService _dxfImportService;

        public ICommand ImportDxfCommand { get; }

        public ProfileDxfOperationViewModel(
            ILocalizationManager localizationManager,
            IDialogService dialogService,
            IDxfImportService dxfImportService)
        {
            _localizationManager = localizationManager;
            _dialogService = dialogService;
            _dxfImportService = dxfImportService ?? throw new ArgumentNullException(nameof(dxfImportService));
            // Пункт 8.4 плана: импорт DXF — async: парсинг файла выполняется в пуле (Task.Run), UI-поток не блокируется даже на больших файлах.
            ImportDxfCommand = new AsyncRelayCommand(ImportDxfFileAsync);

            // Пункт 8.3: без захардкоженного фолбэка — отсутствующий ключ
            // вернёт «?Key?» (лог — в LocalizationManager).
            DisplayName = _localizationManager?.GetString("ProfileDxfName") ?? "ProfileDxfName";

            // Пункт 7.3: операция по умолчанию для автономного создания
            // (в потоках добавления/редактирования фабрику задаёт Operation).
            if (Operation == null)
                Operation = new ProfileDxfOperation();
        }

        protected override void LoadFromOperation(ProfileDxfOperation operation)
        {
            if (operation == null)
                return;

            // Загружаем сохраненный путь к файлу
            FilePath = operation.DxfFilePath;
            
            // Показываем информацию об импорте, если данные уже загружены
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
            
            // Уведомляем об изменении всех свойств, которые зависят от Operation
            OnPropertyChanged(nameof(TotalDepth));
            OnPropertyChanged(nameof(StepDepth));
            OnPropertyChanged(nameof(ToolDiameter));
            OnPropertyChanged(nameof(ContourHeight));
            OnPropertyChanged(nameof(FeedXYRapid));
            OnPropertyChanged(nameof(FeedXYWork));
            OnPropertyChanged(nameof(FeedZRapid));
            OnPropertyChanged(nameof(FeedZWork));
            OnPropertyChanged(nameof(SafeZHeight));
            OnPropertyChanged(nameof(RetractHeight));
            OnPropertyChanged(nameof(Decimals));
        }

        // Пункт 7.3: свойства пишут в Operation напрямую (pass-through),
        // отдельное сохранение не требуется.
        protected override void ApplyToOperation()
        {
        }

        private string _displayName;
        public string DisplayName
        {
            get => _displayName;
            set
            {
                if (Equals(value, _displayName)) return;
                _displayName = value;
                OnPropertyChanged();
            }
        }

        private string _filePath;
        public string FilePath
        {
            get => _filePath;
            set
            {
                if (Equals(value, _filePath)) return;
                _filePath = value;
                OnPropertyChanged();
            }
        }

        private string _importInfo;
        public string ImportInfo
        {
            get => _importInfo;
            set
            {
                if (Equals(value, _importInfo)) return;
                _importInfo = value;
                OnPropertyChanged();
            }
        }

        public double TotalDepth
        {
            get => Operation.TotalDepth;
            set { if (value.Equals(Operation.TotalDepth)) return; Operation.TotalDepth = value; OnPropertyChanged(); }
        }

        public double StepDepth
        {
            get => Operation.StepDepth;
            set { if (value.Equals(Operation.StepDepth)) return; Operation.StepDepth = value; OnPropertyChanged(); }
        }

        public double ToolDiameter
        {
            get => Operation.ToolDiameter;
            set { if (value.Equals(Operation.ToolDiameter)) return; Operation.ToolDiameter = value; OnPropertyChanged(); }
        }

        public double ContourHeight
        {
            get => Operation.ContourHeight;
            set { if (value.Equals(Operation.ContourHeight)) return; Operation.ContourHeight = value; OnPropertyChanged(); }
        }

        public double FeedXYRapid
        {
            get => Operation.FeedXYRapid;
            set { if (value.Equals(Operation.FeedXYRapid)) return; Operation.FeedXYRapid = value; OnPropertyChanged(); }
        }

        public double FeedXYWork
        {
            get => Operation.FeedXYWork;
            set { if (value.Equals(Operation.FeedXYWork)) return; Operation.FeedXYWork = value; OnPropertyChanged(); }
        }

        public double FeedZRapid
        {
            get => Operation.FeedZRapid;
            set { if (value.Equals(Operation.FeedZRapid)) return; Operation.FeedZRapid = value; OnPropertyChanged(); }
        }

        public double FeedZWork
        {
            get => Operation.FeedZWork;
            set { if (value.Equals(Operation.FeedZWork)) return; Operation.FeedZWork = value; OnPropertyChanged(); }
        }

        public double SafeZHeight
        {
            get => Operation.SafeZHeight;
            set { if (value.Equals(Operation.SafeZHeight)) return; Operation.SafeZHeight = value; OnPropertyChanged(); }
        }

        public double RetractHeight
        {
            get => Operation.RetractHeight;
            set { if (value.Equals(Operation.RetractHeight)) return; Operation.RetractHeight = value; OnPropertyChanged(); }
        }

        public int Decimals
        {
            get => Operation.Decimals;
            set { if (value == Operation.Decimals) return; Operation.Decimals = value; OnPropertyChanged(); }
        }

        private async Task ImportDxfFileAsync()
        {
            var title = _localizationManager?.GetString("DxfImportDialogTitle") ?? "DxfImportDialogTitle";
            var fileName = _dialogService?.ShowOpenDialog(title, "DXF files (*.dxf)|*.dxf|All files (*.*)|*.*", "dxf");
            if (fileName == null)
                return;

            try
            {
                var polylines = await Task.Run(() => _dxfImportService.ReadProfilePolylines(fileName));
                if (polylines.Count == 0)
                {
                    var msg = _localizationManager?.GetString("DxfImportNoLines") ?? "DxfImportNoLines";
                    _dialogService?.ShowInfo(msg, title);
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
            }
            catch (Exception ex)
            {
                var msg = _localizationManager?.GetString("DxfImportFailed") ?? "DxfImportFailed";
                _dialogService?.ShowError($"{msg} {ex.Message}", title);
            }
        }

    }
}
