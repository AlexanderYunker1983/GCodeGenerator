using GCodeGenerator.Infrastructure;
using GCodeGenerator.Models;
using GCodeGenerator.Services;
using MugenMvvmToolkit.Interfaces.Models;
using MugenMvvmToolkit.ViewModels;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows.Input;
using YLocalization;

namespace GCodeGenerator.ViewModels
{
    public class MainViewModel : ViewModelBase, IHasDisplayName
    {
        private readonly IGCodeGenerator _generator;
        private readonly GCodeSettings _settings = Models.GCodeSettingsStore.Current;
        private readonly ILocalizationManager _localizationManager;

        public MainViewModel(ILocalizationManager localizationManager)
        {
            _localizationManager = localizationManager;
            _generator = new SimpleGCodeGenerator();

            Operations = new ObservableCollection<OperationBase>();
            AddDrillPointsCommand = new RelayCommand(AddDrillPoints);
            GenerateGCodeCommand = new RelayCommand(GenerateGCode, () => Operations.Count > 0);
            OpenSettingsCommand = new RelayCommand(OpenSettings);

            var title = _localizationManager?.GetString("MainTitle");
            _displayName = string.IsNullOrEmpty(title) ? "Генератор G-кода" : title;
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

        public ObservableCollection<OperationBase> Operations { get; }

        private string _gCodePreview;

        public string GCodePreview
        {
            get => _gCodePreview;
            set
            {
                if (Equals(value, _gCodePreview)) return;
                _gCodePreview = value;
                OnPropertyChanged();
            }
        }

        public ICommand AddDrillPointsCommand { get; }

        public ICommand GenerateGCodeCommand { get; }

        public ICommand OpenSettingsCommand { get; }

        private void AddDrillPoints()
        {
            var op = new DrillPointsOperation();
            var name = _localizationManager?.GetString("DrillPointsName");
            if (!string.IsNullOrEmpty(name))
                op.Name = name;
            // For now add a couple of demo points; later they will be edited in a dedicated window.
            op.Points.Add(new System.Windows.Point(0, 0));
            op.Points.Add(new System.Windows.Point(10, 0));
            op.Points.Add(new System.Windows.Point(10, 10));

            Operations.Add(op);
            ((RelayCommand)GenerateGCodeCommand).RaiseCanExecuteChanged();
        }

        private void GenerateGCode()
        {
            var program = _generator.Generate(new System.Collections.Generic.List<OperationBase>(Operations), _settings);
            var sb = new StringBuilder();
            foreach (var line in program.Lines)
                sb.AppendLine(line);
            GCodePreview = sb.ToString();
        }

        private void OpenSettings()
        {
            using (var vm = GetViewModel<SettingsViewModel>())
            {
                vm.ShowAsync();
            }
        }
    }
}