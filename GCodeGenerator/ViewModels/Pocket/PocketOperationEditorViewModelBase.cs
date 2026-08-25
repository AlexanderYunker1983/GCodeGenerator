using System.ComponentModel;
using GCodeGenerator.Models;

namespace GCodeGenerator.ViewModels.Pocket
{
    /// <summary>
    /// Общая часть диалогов карманов.
    ///
    /// Параметры выборки — стратегия, шаг, уклон, припуск — окно правит прямо
    /// в операции, поэтому здесь остаётся только то, чего в операции нет:
    /// какие поля показывать для выбранной стратегии.
    /// </summary>
    public abstract class PocketOperationEditorViewModelBase<TOperation>
        : OperationEditorViewModelBase<TOperation>
        where TOperation : PocketOperationBase
    {
        /// <summary>Угол линий задаётся только для стратегии параллельных линий.</summary>
        public bool IsLinesStrategy => Operation?.PocketStrategy == PocketStrategy.Lines;

        /// <summary>Шаг между проходами задаётся для линейных стратегий.</summary>
        public bool IsLinesOrZigZagStrategy
            => Operation?.PocketStrategy == PocketStrategy.Lines
               || Operation?.PocketStrategy == PocketStrategy.ZigZag;

        protected override void OnOperationChanged(TOperation operation)
        {
            base.OnOperationChanged(operation);

            // Состав видимых полей зависит от выбранной стратегии, а её меняют
            // прямо в операции — значит и следить надо за операцией.
            operation.PropertyChanged += OnOperationPropertyChanged;
            RaiseStrategyDependentProperties();
        }

        private void OnOperationPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PocketOperationBase.PocketStrategy) || string.IsNullOrEmpty(e.PropertyName))
                RaiseStrategyDependentProperties();
        }

        private void RaiseStrategyDependentProperties()
        {
            OnPropertyChanged(nameof(IsLinesStrategy));
            OnPropertyChanged(nameof(IsLinesOrZigZagStrategy));
        }
    }
}
