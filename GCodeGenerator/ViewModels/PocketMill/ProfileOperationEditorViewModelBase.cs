using CommunityToolkit.Mvvm.ComponentModel;
using GCodeGenerator.Models;

namespace GCodeGenerator.ViewModels.PocketMill
{
    /// <summary>
    /// Общая часть диалогов профильной обработки: сторона обхода контура,
    /// способ врезания и точность аппроксимации дуг. Дополняет общие параметры
    /// фрезерования из <see cref="MillingOperationEditorViewModelBase{TOperation}"/>.
    /// </summary>
    public abstract partial class ProfileOperationEditorViewModelBase<TOperation>
        : MillingOperationEditorViewModelBase<TOperation>
        where TOperation : ProfileOperationBase
    {
        /// <summary>С какой стороны контура идёт инструмент.</summary>
        [ObservableProperty]
        private ToolPathMode _toolPathMode = ToolPathMode.OnLine;

        /// <summary>Врезание вертикально или по наклонной.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsAngledEntry))]
        private EntryMode _entryMode = EntryMode.Vertical;

        /// <summary>Угол наклонного врезания, градусы.</summary>
        [ObservableProperty]
        private double _entryAngle = 5.0;

        /// <summary>Безопасное расстояние между проходами при наклонном врезании.</summary>
        [ObservableProperty]
        private double _safeDistanceBetweenPasses = 1.0;

        /// <summary>
        /// Наибольшая длина отрезка при замене дуги ломаной — задаёт точность
        /// контура, когда вывод дуг отключён в настройках.
        /// </summary>
        [ObservableProperty]
        private double _maxSegmentLength = 0.5;

        /// <summary>
        /// Угол и безопасное расстояние задаются только при наклонном
        /// врезании: диалог скрывает эти поля для вертикального.
        /// </summary>
        public bool IsAngledEntry => EntryMode == EntryMode.Angled;

        /// <summary>Читает общие параметры профиля из операции в диалог.</summary>
        protected void LoadCommonProfileParameters(TOperation operation)
        {
            LoadCommonMillingParameters(operation);

            ToolPathMode = operation.ToolPathMode;
            EntryMode = operation.EntryMode;
            EntryAngle = operation.EntryAngle;
            SafeDistanceBetweenPasses = operation.SafeDistanceBetweenPasses;
            MaxSegmentLength = operation.MaxSegmentLength;
        }

        /// <summary>Сохраняет общие параметры профиля из диалога в операцию.</summary>
        protected void ApplyCommonProfileParameters(TOperation operation)
        {
            ApplyCommonMillingParameters(operation);

            operation.ToolPathMode = ToolPathMode;
            operation.EntryMode = EntryMode;
            operation.EntryAngle = EntryAngle;
            operation.SafeDistanceBetweenPasses = SafeDistanceBetweenPasses;
            operation.MaxSegmentLength = MaxSegmentLength;
        }
    }
}
