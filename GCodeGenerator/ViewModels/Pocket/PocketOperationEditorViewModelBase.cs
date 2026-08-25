using System;
using CommunityToolkit.Mvvm.ComponentModel;
using GCodeGenerator.Models;

namespace GCodeGenerator.ViewModels.Pocket
{
    /// <summary>
    /// Общая часть диалогов карманов: стратегия выборки, шаг обработки, уклон
    /// стенок и параметры черновой и чистовой обработки. Дополняет общие
    /// параметры фрезерования из <see cref="MillingOperationEditorViewModelBase{TOperation}"/>.
    /// </summary>
    public abstract partial class PocketOperationEditorViewModelBase<TOperation>
        : MillingOperationEditorViewModelBase<TOperation>
        where TOperation : PocketOperationBase
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsLinesStrategy))]
        [NotifyPropertyChangedFor(nameof(IsLinesOrZigZagStrategy))]
        private PocketStrategy _pocketStrategy = PocketStrategy.Spiral;

        [ObservableProperty]
        private double _stepPercentOfTool = 40.0;

        [ObservableProperty]
        private double _lineAngleDeg;

        [ObservableProperty]
        private double _wallTaperAngleDeg;

        [ObservableProperty]
        private bool _isRoughingEnabled;

        [ObservableProperty]
        private bool _isFinishingEnabled;

        [ObservableProperty]
        private double _finishAllowance;

        [ObservableProperty]
        private PocketFinishingMode _finishingMode = PocketFinishingMode.All;

        /// <summary>Угол линий задаётся только для стратегии параллельных линий.</summary>
        public bool IsLinesStrategy => PocketStrategy == PocketStrategy.Lines;

        /// <summary>Шаг между проходами задаётся для линейных стратегий.</summary>
        public bool IsLinesOrZigZagStrategy
            => PocketStrategy == PocketStrategy.Lines || PocketStrategy == PocketStrategy.ZigZag;

        /// <summary>
        /// Уклон стенок ограничен диапазоном [0; 90): при 90 градусах стенка
        /// становится горизонтальной, и смещение контура обращается
        /// в бесконечность. Значение вне диапазона заменяется ближайшим
        /// допустимым — повторное присваивание с тем же значением уже
        /// не вызывает обработчик.
        /// </summary>
        partial void OnWallTaperAngleDegChanged(double value)
        {
            var clamped = Math.Max(0, Math.Min(89.999999, value));
            if (!clamped.Equals(value))
                WallTaperAngleDeg = clamped;
        }

        /// <summary>
        /// Черновая и чистовая обработка взаимоисключающие: припуск либо
        /// оставляется, либо снимается.
        /// </summary>
        partial void OnIsRoughingEnabledChanged(bool value)
        {
            if (value && IsFinishingEnabled)
                IsFinishingEnabled = false;
        }

        /// <summary>
        /// Черновая и чистовая обработка взаимоисключающие: припуск либо
        /// оставляется, либо снимается.
        /// </summary>
        partial void OnIsFinishingEnabledChanged(bool value)
        {
            if (value && IsRoughingEnabled)
                IsRoughingEnabled = false;
        }

        /// <summary>Читает общие параметры кармана из операции в диалог.</summary>
        protected void LoadCommonPocketParameters(TOperation operation)
        {
            LoadCommonMillingParameters(operation);

            PocketStrategy = operation.PocketStrategy;
            StepPercentOfTool = operation.StepPercentOfTool;
            LineAngleDeg = operation.LineAngleDeg;
            WallTaperAngleDeg = Math.Max(0, operation.WallTaperAngleDeg);

            IsRoughingEnabled = operation.IsRoughingEnabled;
            IsFinishingEnabled = operation.IsFinishingEnabled;
            FinishAllowance = operation.FinishAllowance;
            FinishingMode = operation.FinishingMode;
        }

        /// <summary>Сохраняет общие параметры кармана из диалога в операцию.</summary>
        protected void ApplyCommonPocketParameters(TOperation operation)
        {
            ApplyCommonMillingParameters(operation);

            operation.PocketStrategy = PocketStrategy;
            operation.StepPercentOfTool = StepPercentOfTool;
            operation.LineAngleDeg = LineAngleDeg;
            operation.WallTaperAngleDeg = WallTaperAngleDeg;

            operation.IsRoughingEnabled = IsRoughingEnabled;
            operation.IsFinishingEnabled = IsFinishingEnabled;
            operation.FinishAllowance = FinishAllowance;
            operation.FinishingMode = FinishingMode;
        }
    }
}
