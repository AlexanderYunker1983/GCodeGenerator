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

            DrillOperations = new DrillOperationsViewModel(localizationManager);
            DrillOperations.Operations.CollectionChanged += (s, e) => 
                ((RelayCommand)GenerateGCodeCommand)?.RaiseCanExecuteChanged();
            
            GenerateGCodeCommand = new RelayCommand(GenerateGCode, () => DrillOperations.Operations.Count > 0);
            SaveGCodeCommand = new RelayCommand(SaveGCode, () => !string.IsNullOrEmpty(GCodePreview));
            PreviewGCodeCommand = new RelayCommand(PreviewGCode, () => !string.IsNullOrEmpty(GCodePreview));
            OpenSettingsCommand = new RelayCommand(OpenSettings);

            var title = _localizationManager?.GetString("MainTitle");
            var baseTitle = string.IsNullOrEmpty(title) ? "Генератор G-кода" : title;
            var version = PlatformVariables.ProgramVersion;
            _displayName = string.IsNullOrEmpty(version) ? baseTitle : $"{baseTitle} v.{version}";
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

        public DrillOperationsViewModel DrillOperations { get; }

        private string _gCodePreview;

        public string GCodePreview
        {
            get => _gCodePreview;
            set
            {
                if (Equals(value, _gCodePreview)) return;
                _gCodePreview = value;
                OnPropertyChanged();
                ((RelayCommand)SaveGCodeCommand)?.RaiseCanExecuteChanged();
                ((RelayCommand)PreviewGCodeCommand)?.RaiseCanExecuteChanged();
            }
        }

        public ICommand GenerateGCodeCommand { get; }

        public ICommand SaveGCodeCommand { get; }

        public ICommand PreviewGCodeCommand { get; }

        public ICommand OpenSettingsCommand { get; }


        private void GenerateGCode()
        {
            var program = _generator.Generate(new System.Collections.Generic.List<OperationBase>(DrillOperations.Operations), _settings);
            var sb = new StringBuilder();
            foreach (var line in program.Lines)
                sb.AppendLine(line);
            GCodePreview = sb.ToString();
            ((RelayCommand)SaveGCodeCommand).RaiseCanExecuteChanged();
        }

        private void SaveGCode()
        {
            if (string.IsNullOrEmpty(GCodePreview))
                return;

            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "G-code files (*.nc)|*.nc|All files (*.*)|*.*",
                DefaultExt = "nc",
                FileName = "program.nc"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    System.IO.File.WriteAllText(saveDialog.FileName, GCodePreview, System.Text.Encoding.UTF8);
                }
                catch (System.Exception ex)
                {
                    System.Windows.MessageBox.Show(
                        $"Ошибка при сохранении файла:\n{ex.Message}",
                        "Ошибка",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }
            }
        }

        private void PreviewGCode()
        {
            if (string.IsNullOrEmpty(GCodePreview))
                return;

            using (var vm = GetViewModel<PreviewViewModel>())
            {
                vm.GCodeText = GCodePreview;
                vm.ShowAsync();
            }
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