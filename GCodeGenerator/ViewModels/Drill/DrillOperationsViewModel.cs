#nullable enable
using System.Collections.ObjectModel;
using System.Windows.Input;
using GCodeGenerator.Localization;
using GCodeGenerator.Models;
using GCodeGenerator.Services;

namespace GCodeGenerator.ViewModels.Drill
{
    /// <summary>
    /// View-модель вкладки «Сверление» (пункт 7.2 плана): добавляет операции
    /// сверления в единую коллекцию MainViewModel.AllOperations и открывает
    /// диалоги операций через фабрику (пункт 7.3). Собственной коллекции
    /// операций нет: список отображает единую коллекцию.
    ///
    /// Восемь из девяти кнопок отличаются только режимом расстановки
    /// отверстий: тип операции у них общий, поэтому команда строится по
    /// <see cref="DrillMode"/>, а не отдельным методом на каждый режим.
    /// </summary>
    public class DrillOperationsViewModel : OperationTabViewModelBase
    {
        public DrillOperationsViewModel(ILocalizationManager? localizationManager, IOperationEditorFactory operationEditorFactory, ObservableCollection<OperationBase> allOperations)
            : base(localizationManager, operationEditorFactory, allOperations)
        {
            AddDrillPointsCommand = AddCommand(typeof(DrillPointsOperation));
            AddDrillLineCommand = AddDrillCommand(DrillMode.Line);
            AddDrillArrayCommand = AddDrillCommand(DrillMode.Array);
            AddDrillRectCommand = AddDrillCommand(DrillMode.Rect);
            AddDrillCircleCommand = AddDrillCommand(DrillMode.Circle);
            AddDrillArcCommand = AddDrillCommand(DrillMode.Arc);
            AddDrillPolygonCommand = AddDrillCommand(DrillMode.Polygon);
            AddDrillEllipseCommand = AddDrillCommand(DrillMode.Ellipse);
            AddDrillPackageCommand = AddDrillCommand(DrillMode.Package);
        }

        public ICommand AddDrillPointsCommand { get; }
        public ICommand AddDrillLineCommand { get; }
        public ICommand AddDrillArrayCommand { get; }
        public ICommand AddDrillRectCommand { get; }
        public ICommand AddDrillCircleCommand { get; }
        public ICommand AddDrillArcCommand { get; }
        public ICommand AddDrillPolygonCommand { get; }
        public ICommand AddDrillEllipseCommand { get; }
        public ICommand AddDrillPackageCommand { get; }

        /// <summary>
        /// Команда добавления сверления в заданном режиме. Название операции
        /// по умолчанию совпадает с надписью кнопки — тот же ключ перевода.
        /// </summary>
        private ICommand AddDrillCommand(DrillMode mode)
            => AddCommand(
                () => DrillPointsOperation.CreateNew(mode),
                "AddDrill" + mode.ToString());
    }
}
