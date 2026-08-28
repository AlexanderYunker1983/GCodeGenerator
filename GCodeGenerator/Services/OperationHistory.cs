#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using GCodeGenerator.Models;

namespace GCodeGenerator.Services
{
    /// <summary>
    /// Отмена и повтор изменений списка операций.
    ///
    /// Шаги записываются на существующих границах изменений: добавление,
    /// удаление и перестановка приходят из самой коллекции, правка — из
    /// транзакции диалога (<see cref="BeginEdit"/>): операция сообщает о
    /// каждом параметре по отдельности, и без явной границы одна правка
    /// рассыпалась бы на десятки шагов.
    ///
    /// Состояние операции хранит <see cref="OperationMemento"/> — тем же
    /// сериализатором, что файл проекта и слепок генерации. По идентификатору
    /// операции шаг находит её в документе, сколько бы других изменений
    /// ни легло поверх.
    ///
    /// Замена документа — создание и открытие проекта — историей не
    /// является: на её время запись приостанавливается, а история очищается
    /// (<see cref="SuspendAndClear"/>): отменять «открытие файла» в
    /// документ, которого больше нет, было бы неверно и опасно.
    /// </summary>
    public sealed class OperationHistory
    {
        /// <summary>
        /// Сколько шагов правки хранится.
        ///
        /// Предел нужен из-за размера шага, а не их числа: состояние операции
        /// хранится сериализованным, и у операции с контуром из чертежа это
        /// сотни килобайт. Без предела за долгий сеанс история занимала бы
        /// больше самого документа, причём молча.
        ///
        /// Сто шагов — заведомо больше, чем человек отменяет подряд, и при
        /// самых тяжёлых операциях это десятки мегабайт, а не сотни.
        /// </summary>
        public const int MaxSteps = 100;

        private readonly ObservableCollection<OperationBase> _operations;

        /// <summary>
        /// Отменяемые шаги, от самого раннего к последнему. Список с двумя
        /// концами, а не стек: при переполнении отбрасывается самый ранний
        /// шаг, а до дна стека не дотянуться.
        /// </summary>
        private readonly LinkedList<IUndoStep> _undo = new LinkedList<IUndoStep>();

        /// <summary>
        /// Отменённые шаги. Предел им не нужен: сюда попадает только то, что
        /// вынуто из отменяемых, а новая правка очищает их совсем.
        /// </summary>
        private readonly Stack<IUndoStep> _redo = new Stack<IUndoStep>();

        /// <summary>Идёт выполнение шага: его мутации новых шагов не пишут.</summary>
        private bool _isRestoring;

        /// <summary>Идёт замена документа: изменения не записываются.</summary>
        private bool _isSuspended;

        public OperationHistory(ObservableCollection<OperationBase> operations)
        {
            _operations = operations ?? throw new ArgumentNullException(nameof(operations));
            _operations.CollectionChanged += OnCollectionChanged;
        }

        /// <summary>Есть что отменять или повторять — для доступности команд.</summary>
        public event EventHandler? StateChanged;

        public bool CanUndo => _undo.Count > 0;

        public bool CanRedo => _redo.Count > 0;

        /// <summary>Сколько шагов сейчас можно отменить.</summary>
        public int UndoCount => _undo.Count;

        public void Undo()
        {
            if (_undo.Count == 0)
                return;

            var step = _undo.Last!.Value;
            _undo.RemoveLast();
            Restore(() => step.Undo(_operations));
            _redo.Push(step);
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Redo()
        {
            if (_redo.Count == 0)
                return;

            var step = _redo.Pop();
            Restore(() => step.Redo(_operations));
            _undo.AddLast(step);
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Граница правки операции: слепок до, слепок после закрытия,
        /// и один шаг, если они различаются, — отмена диалога или OK без
        /// изменений шага не оставляют.
        /// </summary>
        /// <param name="operation">Операция, которую редактирует диалог.</param>
        public IDisposable BeginEdit(OperationBase operation)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            return new EditScope(this, operation);
        }

        /// <summary>
        /// Приостанавливает запись на время замены документа; по завершении
        /// история очищается — она принадлежала прежнему документу.
        /// </summary>
        public IDisposable SuspendAndClear()
        {
            _isSuspended = true;
            return new Suspension(this);
        }

        private void EndSuspension()
        {
            _isSuspended = false;
            _undo.Clear();
            _redo.Clear();
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        private void Record(IUndoStep step)
        {
            _undo.AddLast(step);

            // Переполнение стоит самого раннего шага: до него всё равно
            // не дошли бы, а держать его — значит хранить слепок операции,
            // которого никто уже не увидит.
            while (_undo.Count > MaxSteps)
                _undo.RemoveFirst();

            // Новая правка делает «повторить» бессмысленным: повторялась бы
            // ветка истории, которой больше нет.
            _redo.Clear();
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        private void Restore(Action apply)
        {
            _isRestoring = true;
            try
            {
                apply();
            }
            finally
            {
                _isRestoring = false;
            }
        }

        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (_isRestoring || _isSuspended || e == null)
                return;

            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add when e.NewItems is { Count: 1 }
                    && e.NewItems[0] is OperationBase added:
                    Record(new AddStep(e.NewStartingIndex, OperationMemento.Of(added)));
                    break;

                case NotifyCollectionChangedAction.Remove when e.OldItems is { Count: 1 }
                    && e.OldItems[0] is OperationBase removed:
                    Record(new RemoveStep(e.OldStartingIndex, OperationMemento.Of(removed)));
                    break;

                case NotifyCollectionChangedAction.Move:
                    Record(new MoveStep(e.OldStartingIndex, e.NewStartingIndex));
                    break;

                default:
                    // Замена целиком или массовое изменение приходят только от
                    // самой программы; история таких шагов не представляет.
                    _undo.Clear();
                    _redo.Clear();
                    StateChanged?.Invoke(this, EventArgs.Empty);
                    break;
            }
        }

        /// <summary>Индекс операции документа по идентификатору; -1 — нет.</summary>
        private static int IndexOfId(ObservableCollection<OperationBase> operations, Guid id)
        {
            for (var i = 0; i < operations.Count; i++)
            {
                if (operations[i]?.Id == id)
                    return i;
            }

            return -1;
        }

        private interface IUndoStep
        {
            void Undo(ObservableCollection<OperationBase> operations);

            void Redo(ObservableCollection<OperationBase> operations);
        }

        /// <summary>Операция добавлена: отмена убирает её, повтор возвращает копию.</summary>
        private sealed class AddStep : IUndoStep
        {
            private readonly int _index;
            private readonly OperationMemento _memento;

            public AddStep(int index, OperationMemento memento)
            {
                _index = index;
                _memento = memento;
            }

            public void Undo(ObservableCollection<OperationBase> operations)
            {
                var index = IndexOfId(operations, _memento.Id);
                if (index >= 0)
                    operations.RemoveAt(index);
            }

            public void Redo(ObservableCollection<OperationBase> operations)
                => operations.Insert(Math.Min(_index, operations.Count), _memento.Restore());
        }

        /// <summary>Операция удалена: отмена возвращает копию на прежнее место.</summary>
        private sealed class RemoveStep : IUndoStep
        {
            private readonly int _index;
            private readonly OperationMemento _memento;

            public RemoveStep(int index, OperationMemento memento)
            {
                _index = index;
                _memento = memento;
            }

            public void Undo(ObservableCollection<OperationBase> operations)
                => operations.Insert(Math.Min(_index, operations.Count), _memento.Restore());

            public void Redo(ObservableCollection<OperationBase> operations)
            {
                var index = IndexOfId(operations, _memento.Id);
                if (index >= 0)
                    operations.RemoveAt(index);
            }
        }

        /// <summary>Операция переставлена: отмена и повтор двигают её обратно и вперёд.</summary>
        private sealed class MoveStep : IUndoStep
        {
            private readonly int _from;
            private readonly int _to;

            public MoveStep(int from, int to)
            {
                _from = from;
                _to = to;
            }

            public void Undo(ObservableCollection<OperationBase> operations)
            {
                if (_to < operations.Count && _from < operations.Count)
                    operations.Move(_to, _from);
            }

            public void Redo(ObservableCollection<OperationBase> operations)
            {
                if (_to < operations.Count && _from < operations.Count)
                    operations.Move(_from, _to);
            }
        }

        /// <summary>
        /// Операция изменена диалогом: отмена и повтор подставляют копию
        /// состояния до и после. Подмена экземпляра — как открытие проекта:
        /// все привязки и подписки обновляются уведомлением коллекции.
        /// </summary>
        private sealed class EditStep : IUndoStep
        {
            private readonly OperationMemento _before;
            private readonly OperationMemento _after;

            public EditStep(OperationMemento before, OperationMemento after)
            {
                _before = before;
                _after = after;
            }

            public void Undo(ObservableCollection<OperationBase> operations)
                => Replace(operations, _before);

            public void Redo(ObservableCollection<OperationBase> operations)
                => Replace(operations, _after);

            private static void Replace(ObservableCollection<OperationBase> operations, OperationMemento memento)
            {
                var index = IndexOfId(operations, memento.Id);
                if (index >= 0)
                    operations[index] = memento.Restore();
            }
        }

        /// <summary>Открытая правка: снимает слепки и пишет шаг при различии.</summary>
        private sealed class EditScope : IDisposable
        {
            private readonly OperationHistory _service;
            private readonly OperationBase _operation;
            private readonly OperationMemento _before;
            private bool _closed;

            public EditScope(OperationHistory service, OperationBase operation)
            {
                _service = service;
                _operation = operation;
                _before = OperationMemento.Of(operation);
            }

            public void Dispose()
            {
                if (_closed)
                    return;

                _closed = true;
                if (_service._isSuspended)
                    return;

                var after = OperationMemento.Of(_operation);
                if (after.Json == _before.Json)
                    return; // отмена диалога или OK без изменений — не шаг

                _service.Record(new EditStep(_before, after));
            }
        }

        /// <summary>Приостановка на время замены документа.</summary>
        private sealed class Suspension : IDisposable
        {
            private readonly OperationHistory _service;
            private bool _closed;

            public Suspension(OperationHistory service)
            {
                _service = service;
            }

            public void Dispose()
            {
                if (_closed)
                    return;

                _closed = true;
                _service.EndSuspension();
            }
        }
    }
}
