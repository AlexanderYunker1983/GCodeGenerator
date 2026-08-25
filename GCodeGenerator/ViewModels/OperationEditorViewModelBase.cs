using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using GCodeGenerator.Models;

namespace GCodeGenerator.ViewModels
{
    /// <summary>Result of a modal operation-editing session.</summary>
    public interface IOperationEditorSession
    {
        bool IsAccepted { get; }
    }

    /// <summary>
    /// Контракт диалога редактора операции, не зависящий от конкретного типа
    /// операции. Нужен фабрике диалогов: она выбирает view-модель по типу
    /// операции и должна передать ей операцию, ничего не зная об их типах.
    /// </summary>
    public interface IOperationEditorViewModel : IOperationEditorSession
    {
        /// <summary>Операция, которую редактирует диалог (рабочая копия).</summary>
        OperationBase EditedOperation { get; }

        /// <summary>
        /// Задаёт редактируемую операцию.
        /// </summary>
        /// <param name="operation">Операция того типа, который редактирует диалог.</param>
        /// <exception cref="System.InvalidCastException">
        /// Тип операции не совпадает с типом, который редактирует диалог.
        /// </exception>
        void SetOperation(OperationBase operation);
    }

    /// <summary>
    /// Базовый класс view-моделей диалогов редактора операций.
    ///
    /// Окно правит саму операцию — рабочую копию, которую даёт фабрика.
    /// Раньше каждый диалог заводил собственную копию всех параметров
    /// операции и переносил значения туда и обратно двумя методами: один
    /// параметр существовал в трёх местах — в модели, в диалоге и в двух
    /// списках переноса, — а забытый параметр не ломал сборку, он просто
    /// терялся при сохранении или подменялся значением по умолчанию при
    /// открытии.
    ///
    /// OK (<see cref="OkCommand"/>) проверяет параметры и закрывает окно;
    /// изменения уже в операции. При провале проверки окно остаётся открытым
    /// с пояснением (<see cref="HasValidationError"/>): операция принадлежит
    /// пользователю, и ошибка в поле — повод её исправить, а не потерять.
    /// Отмена (<see cref="CancelCommand"/>) и закрытие окна крестиком просто
    /// закрывают окно — рабочая копия выбрасывается вместе с правками.
    /// </summary>
    public abstract class OperationEditorViewModelBase<TOperation> : CloseableViewModel, IOperationEditorViewModel
        where TOperation : OperationBase
    {
        private TOperation _operation;
        private bool _hasValidationError;

        /// <inheritdoc />
        void IOperationEditorViewModel.SetOperation(OperationBase operation)
            => Operation = (TOperation)operation;

        /// <inheritdoc />
        OperationBase IOperationEditorViewModel.EditedOperation => Operation;

        /// <summary>
        /// Редактируемая операция — рабочая копия. Разметка окна привязана
        /// прямо к её параметрам.
        /// </summary>
        public TOperation Operation
        {
            get => _operation;
            set
            {
                if (Equals(value, _operation)) return;
                _operation = value;
                IsAccepted = false;
                HasValidationError = false;
                OnPropertyChanged();
                if (_operation != null)
                    OnOperationChanged(_operation);
            }
        }

        /// <summary>
        /// Вызывается, когда диалог получил операцию: место для подготовки
        /// состояния окна, которое не является параметром операции
        /// (предпросмотр отверстий, сведения об импорте).
        /// </summary>
        protected virtual void OnOperationChanged(TOperation operation)
        {
        }

        /// <summary>
        /// Проверка перед закрытием. <c>false</c> → окно остаётся открытым
        /// с пояснением (<see cref="HasValidationError"/>), операция остаётся
        /// в списке.
        /// </summary>
        protected virtual bool IsValid() => true;

        /// <summary>OK: проверка параметров и закрытие.</summary>
        public ICommand OkCommand { get; }

        /// <summary>Cancel: закрытие без изменений.</summary>
        public ICommand CancelCommand { get; }

        /// <summary>True only when the parameters passed validation.</summary>
        public bool IsAccepted { get; private set; }

        /// <summary>
        /// Параметры операции не прошли проверку при последнем нажатии OK.
        /// Окно показывает по этому признаку строку с пояснением рядом
        /// с кнопками; сам текст живёт в разметке, как остальные подписи.
        /// </summary>
        public bool HasValidationError
        {
            get => _hasValidationError;
            private set
            {
                if (value == _hasValidationError) return;
                _hasValidationError = value;
                OnPropertyChanged();
            }
        }

        protected OperationEditorViewModelBase()
        {
            OkCommand = new RelayCommand(OnOk);
            CancelCommand = new RelayCommand(OnCancel);
        }

        private void OnOk()
        {
            if (_operation == null) return;
            if (!IsValid())
            {
                // Окно остаётся открытым: пользователь видит, что параметры
                // неверны, и правит их. Операция не трогается.
                HasValidationError = true;
                return;
            }

            HasValidationError = false;
            BeforeAccept(_operation);
            IsAccepted = true;
            RequestClose();
        }

        /// <summary>
        /// Последний шаг перед принятием: место для того, что окно вычисляет
        /// само, а не берёт из полей (список отверстий шаблона сверления).
        /// </summary>
        protected virtual void BeforeAccept(TOperation operation)
        {
        }

        private void OnCancel()
        {
            RequestClose();
        }
    }
}
