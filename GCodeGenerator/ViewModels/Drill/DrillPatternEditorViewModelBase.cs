using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using GCodeGenerator.Models;

namespace GCodeGenerator.ViewModels.Drill
{
    /// <summary>
    /// Общая часть диалогов сверления: подачи, безопасная высота между
    /// отверстиями, параметры глубины, применяемые ко всем отверстиям шаблона,
    /// и предпросмотр рассчитанных отверстий.
    ///
    /// Раньше все девять диалогов повторяли эти свойства вручную, каждый со
    /// своей копией пересчёта отверстий. Здесь пересчёт запускается при любом
    /// изменении параметра диалога, поэтому добавленный параметр невозможно
    /// забыть подключить — прежде для этого нужно было вписать вызов
    /// в каждый сеттер.
    ///
    /// Сам расчёт выполняет ядро (<see cref="DrillPatternBuilder"/>): диалог
    /// только заполняет параметры операции.
    /// </summary>
    public abstract partial class DrillPatternEditorViewModelBase
        : OperationEditorViewModelBase<DrillPointsOperation>
    {
        private bool _suspendRebuild;

        [ObservableProperty]
        private string _displayName;

        [ObservableProperty]
        private double _feedXYRapid = 1000.0;

        [ObservableProperty]
        private double _feedXYWork = 300.0;

        [ObservableProperty]
        private double _safeZBetweenHoles = 1.0;

        [ObservableProperty]
        private int _decimals = 3;

        [ObservableProperty]
        private double _totalDepth = 2.0;

        [ObservableProperty]
        private double _stepDepth = 1.0;

        [ObservableProperty]
        private double _feedZRapid = 500.0;

        [ObservableProperty]
        private double _feedZWork = 200.0;

        [ObservableProperty]
        private double _retractHeight = 0.3;

        protected DrillPatternEditorViewModelBase()
        {
            PreviewHoles = new ObservableCollection<DrillHole>();
        }

        /// <summary>Отверстия шаблона для предпросмотра и сохранения в операцию.</summary>
        public ObservableCollection<DrillHole> PreviewHoles { get; }

        /// <summary>Режим шаблона, который описывает этот диалог.</summary>
        protected abstract DrillMode Mode { get; }

        /// <summary>Переносит в операцию параметры, специфичные для шаблона.</summary>
        protected abstract void ApplyPatternSpecificParameters(DrillPointsOperation target);

        /// <summary>Читает специфичные параметры шаблона из операции в диалог.</summary>
        protected abstract void LoadPatternSpecificParameters(DrillPointsOperation operation);

        protected override void LoadFromOperation(DrillPointsOperation operation)
        {
            // Пока читаются все параметры, пересчитывать отверстия на каждом
            // присваивании незачем — достаточно одного пересчёта в конце.
            _suspendRebuild = true;
            try
            {
                FeedXYRapid = operation.FeedXYRapid;
                FeedXYWork = operation.FeedXYWork;
                SafeZBetweenHoles = operation.SafeZBetweenHoles;
                Decimals = operation.Decimals;

                TotalDepth = operation.TotalDepth;
                StepDepth = operation.StepDepth;
                FeedZRapid = operation.FeedZRapid;
                FeedZWork = operation.FeedZWork;
                RetractHeight = operation.RetractHeight;

                LoadPatternSpecificParameters(operation);
            }
            finally
            {
                _suspendRebuild = false;
            }

            RebuildHoles();
        }

        protected override void ApplyToOperation()
        {
            ApplyPatternParameters(Operation);

            Operation.Holes.Clear();
            foreach (var hole in PreviewHoles)
                Operation.Holes.Add(hole);
        }

        /// <summary>
        /// Переносит параметры шаблона из диалога в операцию. Используется
        /// и при сохранении, и при пересчёте отверстий, поэтому диалог и файл
        /// проекта описывают шаблон одинаково.
        /// </summary>
        protected void ApplyPatternParameters(DrillPointsOperation target)
        {
            target.DrillMode = Mode;

            target.FeedXYRapid = FeedXYRapid;
            target.FeedXYWork = FeedXYWork;
            target.SafeZBetweenHoles = SafeZBetweenHoles;
            target.Decimals = Decimals;

            target.TotalDepth = TotalDepth;
            target.StepDepth = StepDepth;
            target.FeedZRapid = FeedZRapid;
            target.FeedZWork = FeedZWork;
            target.RetractHeight = RetractHeight;

            ApplyPatternSpecificParameters(target);
        }

        /// <summary>
        /// Пересчитывает отверстия шаблона для предпросмотра.
        /// </summary>
        protected void RebuildHoles()
        {
            PreviewHoles.Clear();

            var pattern = new DrillPointsOperation();
            ApplyPatternParameters(pattern);
            foreach (var hole in DrillPatternBuilder.Build(pattern))
                PreviewHoles.Add(hole);
        }

        /// <summary>
        /// Любое изменение параметра диалога делает предпросмотр устаревшим,
        /// поэтому отверстия пересчитываются здесь, а не в каждом сеттере.
        /// Название диалога к шаблону не относится.
        /// </summary>
        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);

            if (_suspendRebuild || e.PropertyName == nameof(DisplayName))
                return;

            RebuildHoles();
        }
    }
}
