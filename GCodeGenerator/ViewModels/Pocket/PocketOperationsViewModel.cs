#nullable enable
using System.Collections.ObjectModel;
using System.Windows.Input;
using GCodeGenerator.Localization;
using GCodeGenerator.Models;
using GCodeGenerator.Services;

namespace GCodeGenerator.ViewModels.Pocket
{
    /// <summary>
    /// View-модель вкладки «Карман» (пункт 7.2 плана): добавляет операции
    /// карманов в единую коллекцию MainViewModel.AllOperations и открывает
    /// диалоги операций через фабрику (пункт 7.3). Собственной коллекции
    /// операций нет: список отображает единую коллекцию.
    /// </summary>
    public class PocketOperationsViewModel : OperationTabViewModelBase
    {
        public PocketOperationsViewModel(ILocalizationManager? localizationManager, IOperationEditorFactory operationEditorFactory, ObservableCollection<OperationBase> allOperations)
            : base(localizationManager, operationEditorFactory, allOperations)
        {
            AddPocketRectangleCommand = AddCommand(typeof(PocketRectangleOperation));
            AddPocketCircleCommand = AddCommand(typeof(PocketCircleOperation));
            AddPocketEllipseCommand = AddCommand(typeof(PocketEllipseOperation));
            AddPocketDxfCommand = AddCommand(typeof(PocketDxfOperation));
        }

        public ICommand AddPocketRectangleCommand { get; }
        public ICommand AddPocketCircleCommand { get; }
        public ICommand AddPocketEllipseCommand { get; }
        public ICommand AddPocketDxfCommand { get; }
    }
}
