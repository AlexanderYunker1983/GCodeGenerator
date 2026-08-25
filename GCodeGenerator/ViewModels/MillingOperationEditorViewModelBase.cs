using CommunityToolkit.Mvvm.ComponentModel;
using GCodeGenerator.Models;

namespace GCodeGenerator.ViewModels
{
    /// <summary>
    /// Общая часть диалогов фрезерных операций: инструмент, подачи, раскладка
    /// по глубине, направление обхода и точность координат.
    ///
    /// Раньше эти пятнадцать свойств были выписаны вручную в каждом из десяти
    /// диалогов — с полем, проверкой на равенство и уведомлением об изменении,
    /// по восемь строк на свойство. Здесь они объявлены один раз, а тела
    /// свойств создаёт генератор исходного кода
    /// (<see cref="ObservablePropertyAttribute"/>): имена свойств для привязок
    /// интерфейса остаются прежними.
    ///
    /// Значения по умолчанию совпадают со значениями по умолчанию моделей
    /// (<see cref="MillingOperationBase"/>): диалог, открытый для новой
    /// операции, показывает ровно то, что в неё записано.
    /// </summary>
    public abstract partial class MillingOperationEditorViewModelBase<TOperation>
        : OperationEditorViewModelBase<TOperation>
        where TOperation : MillingOperationBase
    {
        [ObservableProperty]
        private MillingDirection _direction = MillingDirection.Clockwise;

        [ObservableProperty]
        private double _toolDiameter = 3.0;

        [ObservableProperty]
        private int _decimals = 3;

        [ObservableProperty]
        private double _contourHeight;

        [ObservableProperty]
        private double _totalDepth = 2.0;

        [ObservableProperty]
        private double _stepDepth = 1.0;

        [ObservableProperty]
        private double _safeZHeight = 1.0;

        [ObservableProperty]
        private double _retractHeight = 0.3;

        [ObservableProperty]
        private double _feedXYRapid = 1000.0;

        [ObservableProperty]
        private double _feedXYWork = 300.0;

        [ObservableProperty]
        private double _feedZRapid = 500.0;

        [ObservableProperty]
        private double _feedZWork = 200.0;

        /// <summary>Читает общие параметры резания из операции в диалог.</summary>
        protected void LoadCommonMillingParameters(TOperation operation)
        {
            Direction = operation.Direction;
            ToolDiameter = operation.ToolDiameter;
            Decimals = operation.Decimals;

            ContourHeight = operation.ContourHeight;
            TotalDepth = operation.TotalDepth;
            StepDepth = operation.StepDepth;
            SafeZHeight = operation.SafeZHeight;
            RetractHeight = operation.RetractHeight;

            FeedXYRapid = operation.FeedXYRapid;
            FeedXYWork = operation.FeedXYWork;
            FeedZRapid = operation.FeedZRapid;
            FeedZWork = operation.FeedZWork;
        }

        /// <summary>Сохраняет общие параметры резания из диалога в операцию.</summary>
        protected void ApplyCommonMillingParameters(TOperation operation)
        {
            operation.Direction = Direction;
            operation.ToolDiameter = ToolDiameter;
            operation.Decimals = Decimals;

            operation.ContourHeight = ContourHeight;
            operation.TotalDepth = TotalDepth;
            operation.StepDepth = StepDepth;
            operation.SafeZHeight = SafeZHeight;
            operation.RetractHeight = RetractHeight;

            operation.FeedXYRapid = FeedXYRapid;
            operation.FeedXYWork = FeedXYWork;
            operation.FeedZRapid = FeedZRapid;
            operation.FeedZWork = FeedZWork;
        }
    }
}
