#nullable enable
using System.ComponentModel;
using GCodeGenerator.Models;

namespace GCodeGenerator.ViewModels.Pocket
{
    /// <summary>
    /// Общая часть диалогов карманов.
    ///
    /// Параметры выборки — подвод, стратегия, шаг, уклон, припуск — окно
    /// правит прямо в операции, поэтому здесь остаётся только то, чего в
    /// операции нет: какие поля показывать для выбранных режимов.
    /// </summary>
    public abstract class PocketOperationEditorViewModelBase<TOperation>
        : OperationEditorViewModelBase<TOperation>
        where TOperation : PocketOperationBase
    {
        /// <summary>Угол линий задаётся только для стратегии параллельных линий.</summary>
        public bool IsLinesStrategy => Operation?.PocketStrategy == PocketStrategy.Lines;

        /// <summary>Угол и диаметр подвода задаются только для винтового входа.</summary>
        public bool IsHelicalEntry => Operation?.EntryMode == PocketEntryMode.Helical;

        /// <summary>
        /// Параметры резания исполняются только у обычного кармана. У острова
        /// редактируется геометрия, а остальные блоки остаются видимыми, но
        /// недоступными, чтобы назначение режима было очевидно.
        /// </summary>
        public bool IsMachiningPocket => Operation?.PocketMode == PocketMode.Machining;

        /// <summary>Шаг между проходами задаётся для линейных стратегий.</summary>
        public bool IsLinesOrZigZagStrategy
            => Operation?.PocketStrategy == PocketStrategy.Lines
               || Operation?.PocketStrategy == PocketStrategy.ZigZag;

        /// <summary>
        /// Порядок «центр → край / край → центр» определён только у спирали
        /// и последовательности концентрических контуров.
        /// </summary>
        public bool IsSpiralOrConcentricStrategy
            => Operation?.PocketStrategy == PocketStrategy.Spiral
               || Operation?.PocketStrategy == PocketStrategy.Concentric;

        protected override void OnOperationChanged(TOperation operation)
        {
            base.OnOperationChanged(operation);

            RaiseStrategyDependentProperties();
        }

        // Состав видимых полей зависит от выбранной стратегии, а её меняют
        // прямо в операции — значит и следить надо за операцией.
        protected override void OnOperationPropertyChanged(
            TOperation operation, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PocketOperationBase.PocketStrategy) || string.IsNullOrEmpty(e.PropertyName))
                RaiseStrategyDependentProperties();
            if (e.PropertyName == nameof(PocketOperationBase.EntryMode) || string.IsNullOrEmpty(e.PropertyName))
                OnPropertyChanged(nameof(IsHelicalEntry));
            if (e.PropertyName == nameof(PocketOperationBase.PocketMode) || string.IsNullOrEmpty(e.PropertyName))
                OnPropertyChanged(nameof(IsMachiningPocket));
        }

        private void RaiseStrategyDependentProperties()
        {
            OnPropertyChanged(nameof(IsLinesStrategy));
            OnPropertyChanged(nameof(IsLinesOrZigZagStrategy));
            OnPropertyChanged(nameof(IsSpiralOrConcentricStrategy));
            OnPropertyChanged(nameof(IsHelicalEntry));
            OnPropertyChanged(nameof(IsMachiningPocket));
        }
    }
}
