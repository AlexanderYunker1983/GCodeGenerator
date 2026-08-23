using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using GCodeGenerator.Models;

namespace GCodeGenerator.ViewModels
{
    /// <summary>
    /// Базовый класс view-моделей диалогов редактора операций (пункт 7.3 плана):
    /// явная семантика OK/Cancel.
    ///
    /// OK (<see cref="OkCommand"/>): валидация (<see cref="IsValid"/>),
    /// сохранение VM→операция (<see cref="ApplyToOperation"/>), закрытие.
    /// При провале валидации операция удаляется из коллекции (legacy-поведение
    /// «remove if invalid»). Cancel (<see cref="CancelCommand"/>) и закрытие окна
    /// крестиком — без изменений.
    ///
    /// <see cref="CloseableViewModel.OnClosed"/> больше не сохраняет (пункт 7.3):
    /// изменения применяются только по OK.
    ///
    /// Сеттер <see cref="Operation"/> читает значения операции в свойства VM
    /// (<see cref="LoadFromOperation"/>). Диалоговые VM мигрируют на этот базовый
    /// класс в пункте 7.4 плана (по одному диалогу на коммит).
    /// </summary>
    public abstract class OperationEditorViewModelBase<TOperation> : CloseableViewModel
        where TOperation : OperationBase
    {
        private TOperation _operation;

        /// <summary>
        /// Единая коллекция операций (MainViewModel.AllOperations) — для удаления
        /// невалидной операции по OK.
        /// </summary>
        public ObservableCollection<OperationBase> Operations { get; set; }

        /// <summary>Редактируемая операция. Сеттер читает значения в свойства VM.</summary>
        public TOperation Operation
        {
            get => _operation;
            set
            {
                if (Equals(value, _operation)) return;
                _operation = value;
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
        /// Валидация перед сохранением. <c>false</c> → операция удаляется из
        /// коллекции (legacy-поведение «remove if invalid», пункт 7.3 плана).
        /// </summary>
        protected virtual bool IsValid() => true;

        /// <summary>OK: валидация + сохранение + закрытие (пункт 7.3 плана).</summary>
        public ICommand OkCommand { get; }

        /// <summary>Cancel: закрытие без изменений (пункт 7.3 плана).</summary>
        public ICommand CancelCommand { get; }

        protected OperationEditorViewModelBase()
        {
            OkCommand = new RelayCommand(OnOk);
            CancelCommand = new RelayCommand(OnCancel);
        }

        private void OnOk()
        {
            if (_operation == null) return;
            if (IsValid())
                ApplyToOperation();
            else
                RemoveOperation();
            RequestClose();
        }

        private void OnCancel()
        {
            RequestClose();
        }

        /// <summary>
        /// Удаляет операцию из единой коллекции (legacy «remove if invalid»).
        /// </summary>
        protected void RemoveOperation()
        {
            Operations?.Remove(_operation);
        }
    }
}
