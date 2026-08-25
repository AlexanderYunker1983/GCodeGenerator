using System.Collections.ObjectModel;
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
    /// операции и должна передать ей операцию и общий список, ничего не зная
    /// об их типах. Прежде фабрика перебирала одиннадцать типов view-моделей
    /// в switch, где все ветки делали одно и то же.
    ///
    /// Реализуется базовым классом <see cref="OperationEditorViewModelBase{TOperation}"/>,
    /// который приводит операцию к своему типу.
    /// </summary>
    public interface IOperationEditorViewModel : IOperationEditorSession
    {
        /// <summary>Единая коллекция операций (MainViewModel.AllOperations).</summary>
        ObservableCollection<OperationBase> Operations { set; }

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
    /// Базовый класс view-моделей диалогов редактора операций (пункт 7.3 плана):
    /// явная семантика OK/Cancel.
    ///
    /// OK (<see cref="OkCommand"/>): валидация (<see cref="IsValid"/>),
    /// сохранение VM→операция (<see cref="ApplyToOperation"/>), закрытие.
    /// При провале валидации окно остаётся открытым и показывает пояснение
    /// (<see cref="HasValidationError"/>): операция принадлежит пользователю,
    /// и ошибка в поле — повод её исправить, а не потерять. Прежде такое
    /// нажатие OK закрывало окно и удаляло операцию из списка
    /// (legacy-поведение «remove if invalid»). Cancel (<see cref="CancelCommand"/>)
    /// и закрытие окна крестиком — без изменений.
    ///
    /// <see cref="CloseableViewModel.OnClosed"/> больше не сохраняет (пункт 7.3):
    /// изменения применяются только по OK.
    ///
    /// Сеттер <see cref="Operation"/> читает значения операции в свойства VM
    /// (<see cref="LoadFromOperation"/>). Диалоговые VM мигрируют на этот базовый
    /// класс в пункте 7.4 плана (по одному диалогу на коммит).
    /// </summary>
    public abstract class OperationEditorViewModelBase<TOperation> : CloseableViewModel, IOperationEditorViewModel
        where TOperation : OperationBase
    {
        private TOperation _operation;
        private bool _hasValidationError;

        /// <summary>
        /// Единая коллекция операций (MainViewModel.AllOperations) — для удаления
        /// невалидной операции по OK.
        /// </summary>
        public ObservableCollection<OperationBase> Operations { get; set; }

        /// <inheritdoc />
        void IOperationEditorViewModel.SetOperation(OperationBase operation)
            => Operation = (TOperation)operation;

        /// <inheritdoc />
        OperationBase IOperationEditorViewModel.EditedOperation => Operation;

        /// <summary>Редактируемая операция. Сеттер читает значения в свойства VM.</summary>
        public TOperation Operation
        {
            get => _operation;
            set
            {
                if (Equals(value, _operation)) return;
                _operation = value;
                IsAccepted = false;
                HasValidationError = false;
                if (_operation != null)
                    LoadFromOperation(_operation);
            }
        }

        /// <summary>
        /// Читает значения операции в свойства VM (вызывается из сеттера
        /// <see cref="Operation"/>).
        /// </summary>
        protected abstract void LoadFromOperation(TOperation operation);

        /// <summary>Сохраняет значения свойств VM в операцию (вызывается по OK).</summary>
        protected abstract void ApplyToOperation();

        /// <summary>
        /// Валидация перед сохранением. <c>false</c> → окно остаётся открытым
        /// с пояснением (<see cref="HasValidationError"/>), изменения не
        /// применяются, операция остаётся в списке.
        /// </summary>
        protected virtual bool IsValid() => true;

        /// <summary>OK: валидация + сохранение + закрытие (пункт 7.3 плана).</summary>
        public ICommand OkCommand { get; }

        /// <summary>Cancel: закрытие без изменений (пункт 7.3 плана).</summary>
        public ICommand CancelCommand { get; }

        /// <summary>True only when validation and ApplyToOperation completed successfully.</summary>
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
            ApplyToOperation();
            // Пункт 7.2 плана: сохранение из диалога перерисовывает 2D-превью.
            // Геометрия — авто-свойства (без PropertyChanged), поэтому
            // уведомление явное (иначе сцена обновится только при следующем
            // изменении коллекции операций).
            _operation.NotifyContentChanged();
            IsAccepted = true;
            RequestClose();
        }

        private void OnCancel()
        {
            RequestClose();
        }
    }
}
