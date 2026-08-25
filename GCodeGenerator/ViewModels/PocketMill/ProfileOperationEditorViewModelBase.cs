#nullable enable
using System.ComponentModel;
using GCodeGenerator.Models;

namespace GCodeGenerator.ViewModels.PocketMill
{
    /// <summary>
    /// Общая часть диалогов профильной обработки.
    ///
    /// Сторона обхода, способ врезания и точность аппроксимации правятся
    /// прямо в операции, поэтому здесь остаётся только то, чего в операции
    /// нет: какие поля показывать для выбранного способа врезания.
    /// </summary>
    public abstract class ProfileOperationEditorViewModelBase<TOperation>
        : OperationEditorViewModelBase<TOperation>
        where TOperation : ProfileOperationBase
    {
        /// <summary>
        /// Угол и безопасное расстояние задаются только при наклонном
        /// врезании: диалог скрывает эти поля для вертикального.
        /// </summary>
        public bool IsAngledEntry => Operation?.EntryMode == EntryMode.Angled;

        protected override void OnOperationChanged(TOperation operation)
        {
            base.OnOperationChanged(operation);

            operation.PropertyChanged += OnOperationPropertyChanged;
            OnPropertyChanged(nameof(IsAngledEntry));
        }

        private void OnOperationPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ProfileOperationBase.EntryMode) || string.IsNullOrEmpty(e.PropertyName))
                OnPropertyChanged(nameof(IsAngledEntry));
        }
    }
}
