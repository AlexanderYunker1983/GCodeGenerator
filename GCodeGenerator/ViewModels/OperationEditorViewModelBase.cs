#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
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
        OperationBase? EditedOperation { get; }

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
        private TOperation? _operation;
        private bool _isListeningToOperation;
        private bool _hasValidationError;
        private string _validationSummary = string.Empty;

        /// <inheritdoc />
        void IOperationEditorViewModel.SetOperation(OperationBase operation)
        {
            Operation = (TOperation)operation;

            // Повторный показ того же диалога с той же операцией не проходит
            // через сеттер (значение не изменилось), а слушать её после
            // прошлого закрытия окно перестало.
            StartListening();
        }

        /// <inheritdoc />
        OperationBase? IOperationEditorViewModel.EditedOperation => Operation;

        /// <summary>
        /// Редактируемая операция — рабочая копия. Разметка окна привязана
        /// прямо к её параметрам.
        /// </summary>
        public TOperation? Operation
        {
            get => _operation;
            set
            {
                if (Equals(value, _operation)) return;
                StopListening();
                _operation = value;
                IsAccepted = false;
                HasValidationError = false;
                OnPropertyChanged();
                if (_operation != null)
                {
                    StartListening();
                    OnOperationChanged(_operation);
                }
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
        /// Вызывается при изменении параметра операции, пока окно открыто.
        /// Здесь окно обновляет то, что зависит от параметров, но само
        /// параметром не является: видимость полей для выбранного способа
        /// врезания, предпросмотр расстановки отверстий.
        ///
        /// Наследники переопределяют этот метод, а не подписываются на
        /// событие операции сами. Прежде подписывался каждый из них, а
        /// отписаться не мог никто: у диалога не было места, где он узнаёт
        /// о закрытии. Новая операция правится диалогом напрямую и после
        /// подтверждения уходит в документ, унося с собой живую ссылку на
        /// view-модель закрытого окна — и та продолжала работать на каждое
        /// изменение операции, пересчитывая, например, всю расстановку
        /// отверстий в невидимом окне.
        /// </summary>
        /// <param name="operation">Правимая операция.</param>
        /// <param name="e">Что изменилось; пустое имя означает «всё сразу».</param>
        protected virtual void OnOperationPropertyChanged(
            TOperation operation, System.ComponentModel.PropertyChangedEventArgs e)
        {
        }

        /// <summary>
        /// Диалог закрыт: окно перестаёт слушать операцию. Вызывается хостом
        /// диалогов и при сбое внутри окна, поэтому подписка не переживает
        /// закрытие ни при каком исходе.
        /// </summary>
        public override void OnClosed()
        {
            base.OnClosed();
            StopListening();
        }

        private void StartListening()
        {
            if (_operation == null || _isListeningToOperation)
                return;

            _operation.PropertyChanged += OnOperationPropertyChangedCore;
            _isListeningToOperation = true;
        }

        private void StopListening()
        {
            if (_operation == null || !_isListeningToOperation)
                return;

            _operation.PropertyChanged -= OnOperationPropertyChangedCore;
            _isListeningToOperation = false;
        }

        private void OnOperationPropertyChangedCore(
            object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (_operation != null)
                OnOperationPropertyChanged(_operation, e);
        }

        /// <summary>
        /// Проверка перед закрытием. <c>false</c> → окно остаётся открытым
        /// с пояснением (<see cref="HasValidationError"/>), операция остаётся
        /// в списке.
        /// </summary>
        /// <summary>
        /// Годятся ли введённые параметры. Операция передаётся сюда, а не
        /// берётся из поля: к моменту проверки она заведомо есть, и окну
        /// не приходится проверять это второй раз.
        ///
        /// По умолчанию окно спрашивает саму операцию — ту же проверку
        /// выполняет генерация. Прежде окно проверяло два-три поля, поэтому
        /// принимало параметры, на которых генерация потом отказывалась
        /// строить программу: пользователь узнавал об ошибке не там, где её
        /// допустил, и не понимал, какое окно открывать заново.
        /// </summary>
        /// <param name="operation">Правимая операция.</param>
        protected virtual bool IsValid(TOperation operation)
            => Problems(operation).Count == 0;

        /// <summary>Проблемы операции; пустой список — параметры годятся.</summary>
        /// <param name="operation">Правимая операция.</param>
        private static IReadOnlyList<ValidationIssue> Problems(TOperation operation)
            => operation is IValidatable validatable
                ? validatable.Validate()
                : System.Array.Empty<ValidationIssue>();

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

        /// <summary>
        /// Что именно не так с параметрами: перечень проблем, по одной в
        /// строке. Прежде окно сообщало лишь о самом факте — «параметры
        /// неверны», — и пользователю приходилось искать виновное поле
        /// самому.
        /// </summary>
        public string ValidationSummary
        {
            get => _validationSummary;
            private set
            {
                if (value == _validationSummary) return;
                _validationSummary = value;
                OnPropertyChanged();
            }
        }

        /// <summary>Проблемы одной строкой на каждую, на языке интерфейса.</summary>
        /// <param name="problems">Найденные проблемы параметров.</param>
        private static string Describe(IReadOnlyList<ValidationIssue> problems)
            => string.Join(
                Environment.NewLine,
                problems.Select(problem => $"{problem.Property}: {ValidationMessages.Describe(problem)}"));

        protected OperationEditorViewModelBase()
        {
            OkCommand = new RelayCommand(OnOk);
            CancelCommand = new RelayCommand(OnCancel);
        }

        private void OnOk()
        {
            if (_operation == null) return;
            if (!IsValid(_operation))
            {
                // Окно остаётся открытым: пользователь видит, что именно
                // неверно, и правит. Операция не трогается.
                ValidationSummary = Describe(Problems(_operation));
                HasValidationError = true;
                return;
            }

            HasValidationError = false;
            ValidationSummary = string.Empty;
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
