#nullable enable
using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using GCodeGenerator.Localization;
using GCodeGenerator.Models;
using GCodeGenerator.Operations;
using GCodeGenerator.Services;

namespace GCodeGenerator.ViewModels
{
    /// <summary>
    /// Общее поведение вкладок с операциями: сверления, профиля и кармана.
    ///
    /// Кнопка «добавить» делает для любого типа одно и то же — создаёт
    /// операцию со значениями по умолчанию, даёт ей название на языке
    /// интерфейса, показывает диалог и добавляет её в документ только по
    /// подтверждению. Раньше это повторялось девятнадцатью почти одинаковыми
    /// методами по трём вкладкам, и каждый нёс собственный ключ перевода:
    /// опечатка в ключе давала операцию без названия, а забытая проверка
    /// подтверждения — операцию, добавленную по отмене.
    /// </summary>
    public abstract class OperationTabViewModelBase : ViewModelBase
    {
        private readonly ILocalizationManager? _localizationManager;
        private readonly IOperationEditorFactory _operationEditorFactory;
        private readonly ObservableCollection<OperationBase> _allOperations;

        protected OperationTabViewModelBase(
            ILocalizationManager? localizationManager,
            IOperationEditorFactory operationEditorFactory,
            ObservableCollection<OperationBase> allOperations)
        {
            _localizationManager = localizationManager;
            _operationEditorFactory = operationEditorFactory ?? throw new ArgumentNullException(nameof(operationEditorFactory));
            _allOperations = allOperations ?? throw new ArgumentNullException(nameof(allOperations));
        }

        /// <summary>
        /// Событие: пользователь добавил новую операцию через вкладку
        /// (MainViewModel выбирает её в общем списке).
        /// </summary>
        public event Action<OperationBase>? OperationAdded;

        /// <summary>
        /// Команда добавления операции указанного типа: название по умолчанию
        /// берётся из каталога, поэтому ключ перевода не переписывается на
        /// каждой вкладке заново.
        /// </summary>
        protected ICommand AddCommand(Type operationType)
        {
            var descriptor = OperationCatalog.ForType(operationType);
            return AddCommand(descriptor.Create, descriptor.NameKey);
        }

        /// <summary>
        /// Команда добавления операции, вид которой задан не типом, а
        /// параметром — как режим сверления.
        /// </summary>
        protected ICommand AddCommand(Func<OperationBase> create, string nameKey)
            => new RelayCommand(() => Add(create(), nameKey));

        private void Add(OperationBase operation, string nameKey)
        {
            var name = _localizationManager?.GetString(nameKey);
            if (!string.IsNullOrEmpty(name))
                operation.Name = name;

            if (_operationEditorFactory.CreateOperation(operation, _allOperations))
                OperationAdded?.Invoke(operation);
        }
    }
}
