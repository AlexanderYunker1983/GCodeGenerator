using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using GCodeGenerator.Models;

namespace GCodeGenerator.ViewModels
{
    /// <summary>
    /// Фильтрованное представление единой коллекции операций по категории
    /// (пункт 7.2 плана). Чистый класс без WPF-типов (DoD фазы 7).
    ///
    /// Источник истины — <see cref="_source"/> (MainViewModel.AllOperations);
    /// представление только читает и передаёт удаление в источник.
    /// Порядок элементов следует порядку исходной коллекции.
    /// </summary>
    public sealed class FilteredOperationsView : IReadOnlyList<OperationBase>
    {
        private readonly ObservableCollection<OperationBase> _source;
        private readonly OperationCategory _category;
        private List<OperationBase> _items;

        public FilteredOperationsView(ObservableCollection<OperationBase> source, OperationCategory category)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _category = category;
            _items = Filter();
            _source.CollectionChanged += (s, e) => _items = Filter();
        }

        /// <summary>Категория, по которой фильтруется коллекция.</summary>
        public OperationCategory Category => _category;

        public int Count => _items.Count;

        public OperationBase this[int index] => _items[index];

        public bool Contains(OperationBase operation)
            => operation != null && operation.Category == _category && _source.Contains(operation);

        public bool Remove(OperationBase operation)
        {
            // Удаление выполняется в исходной коллекции (единый источник истины);
            // представление обновляется через CollectionChanged источника.
            if (operation == null || operation.Category != _category)
                return false;
            return _source.Remove(operation);
        }

        public IEnumerator<OperationBase> GetEnumerator() => _items.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        private List<OperationBase> Filter()
        {
            var result = new List<OperationBase>(_source.Count);
            foreach (var op in _source)
            {
                if (op.Category == _category)
                    result.Add(op);
            }
            return result;
        }
    }
}
