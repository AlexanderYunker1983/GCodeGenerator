using System.Collections.ObjectModel;
using System.Windows.Input;
using GCodeGenerator.Localization;
using GCodeGenerator.Models;
using GCodeGenerator.Services;

namespace GCodeGenerator.ViewModels.PocketMill
{
    /// <summary>
    /// View-модель вкладки «Профиль» (пункт 7.2 плана): добавляет операции
    /// профиля в единую коллекцию MainViewModel.AllOperations и открывает
    /// диалоги операций через фабрику (пункт 7.3). Собственной коллекции
    /// операций нет: список отображает единую коллекцию.
    /// </summary>
    public class ProfileMillingOperationsViewModel : OperationTabViewModelBase
    {
        public ProfileMillingOperationsViewModel(ILocalizationManager localizationManager, IOperationEditorFactory operationEditorFactory, ObservableCollection<OperationBase> allOperations)
            : base(localizationManager, operationEditorFactory, allOperations)
        {
            AddProfileRectangleCommand = AddCommand(typeof(ProfileRectangleOperation));
            AddProfileRoundedRectangleCommand = AddCommand(typeof(ProfileRoundedRectangleOperation));
            AddProfileCircleCommand = AddCommand(typeof(ProfileCircleOperation));
            AddProfileEllipseCommand = AddCommand(typeof(ProfileEllipseOperation));
            AddProfilePolygonCommand = AddCommand(typeof(ProfilePolygonOperation));
            AddProfileDxfCommand = AddCommand(typeof(ProfileDxfOperation));
        }

        public ICommand AddProfileRectangleCommand { get; }
        public ICommand AddProfileRoundedRectangleCommand { get; }
        public ICommand AddProfileCircleCommand { get; }
        public ICommand AddProfileEllipseCommand { get; }
        public ICommand AddProfilePolygonCommand { get; }
        public ICommand AddProfileDxfCommand { get; }
    }
}
