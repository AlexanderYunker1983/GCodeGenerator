using System;
using System.Collections.Generic;
using Autofac.Features.Indexed;
using GCodeGenerator.Models;
using GCodeGenerator.ViewModels;

namespace GCodeGenerator.Tests.Fixtures
{
    /// <summary>
    /// Диалоги операций в тестах: вместо контейнера — словарь «тип
    /// view-модели → как её создать». По умолчанию отдаёт заглушку, которая
    /// запоминает переданную операцию и ничего не подтверждает.
    /// </summary>
    public sealed class FakeEditorIndex : IIndex<Type, IOperationEditorViewModel>
    {
        private readonly Dictionary<Type, IOperationEditorViewModel> _created =
            new Dictionary<Type, IOperationEditorViewModel>();

        /// <summary>Чем подменить диалог конкретного типа; null — заглушка.</summary>
        public Func<Type, IOperationEditorViewModel> Factory { get; set; }

        /// <summary>Тип последнего запрошенного диалога.</summary>
        public Type RequestedType { get; private set; }

        public IOperationEditorViewModel this[Type key]
        {
            get
            {
                if (TryGetValue(key, out var value))
                    return value;
                throw new KeyNotFoundException(key?.FullName);
            }
        }

        public bool TryGetValue(Type key, out IOperationEditorViewModel value)
        {
            RequestedType = key;
            if (!_created.TryGetValue(key, out value))
            {
                value = Factory?.Invoke(key) ?? new StubEditorViewModel();
                _created[key] = value;
            }
            return true;
        }

        /// <summary>Диалог, ранее выданный для указанного типа (null — не запрашивался).</summary>
        public IOperationEditorViewModel Created(Type key)
            => _created.TryGetValue(key, out var value) ? value : null;
    }

    /// <summary>Диалог, который ничего не показывает и ничего не подтверждает.</summary>
    public sealed class StubEditorViewModel : IOperationEditorViewModel
    {
        public OperationBase EditedOperation { get; private set; }

        public bool IsAccepted => false;

        public void SetOperation(OperationBase operation) => EditedOperation = operation;
    }
}
