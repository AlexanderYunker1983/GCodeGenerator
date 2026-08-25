using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using GCodeGenerator.Models;

namespace GCodeGenerator.ViewModels.Drill
{
    /// <summary>
    /// Общая часть диалогов сверления: предпросмотр отверстий, рассчитанных
    /// по параметрам шаблона.
    ///
    /// Сами параметры — подачи, безопасная высота, глубина, шаг — окно правит
    /// прямо в операции. Отверстия пересчитываются при любом её изменении,
    /// поэтому добавленный параметр невозможно забыть подключить: прежде для
    /// этого нужно было вписать его в диалог, в чтение, в сохранение и в
    /// пересчёт по отдельности.
    ///
    /// Сам расчёт выполняет ядро (<see cref="DrillPatternBuilder"/>).
    /// </summary>
    public abstract partial class DrillPatternEditorViewModelBase
        : OperationEditorViewModelBase<DrillPointsOperation>
    {
        [ObservableProperty]
        private string _displayName;

        protected DrillPatternEditorViewModelBase()
        {
            PreviewHoles = new ObservableCollection<DrillHole>();
        }

        /// <summary>Отверстия шаблона для предпросмотра и сохранения в операцию.</summary>
        public ObservableCollection<DrillHole> PreviewHoles { get; }

        /// <summary>Режим шаблона, который описывает этот диалог.</summary>
        protected abstract DrillMode Mode { get; }

        protected override void OnOperationChanged(DrillPointsOperation operation)
        {
            base.OnOperationChanged(operation);

            // Диалог знает, какой шаблон он редактирует: операция, открытая
            // в нём, описывает именно этот шаблон.
            operation.DrillMode = Mode;
            operation.PropertyChanged += OnOperationPropertyChanged;
            RebuildHoles();
        }

        /// <summary>
        /// По OK в операцию уходит рассчитанный список отверстий: генератор
        /// сверлит именно его.
        /// </summary>
        protected override void BeforeAccept(DrillPointsOperation operation)
        {
            base.BeforeAccept(operation);

            operation.Holes.Clear();
            foreach (var hole in PreviewHoles)
                operation.Holes.Add(hole);
        }

        /// <summary>
        /// Любое изменение параметра делает предпросмотр устаревшим, поэтому
        /// отверстия пересчитываются здесь, а не в каждом поле окна.
        /// Список отверстий на шаблон не влияет — он его результат.
        /// </summary>
        private void OnOperationPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DrillPointsOperation.Holes))
                return;

            RebuildHoles();
        }

        /// <summary>Пересчитывает отверстия шаблона для предпросмотра.</summary>
        protected void RebuildHoles()
        {
            PreviewHoles.Clear();
            if (Operation == null)
                return;

            foreach (var hole in DrillPatternBuilder.Build(Operation))
                PreviewHoles.Add(hole);
        }
    }
}
