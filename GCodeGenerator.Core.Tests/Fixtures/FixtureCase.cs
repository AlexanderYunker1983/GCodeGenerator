using System.Collections.Generic;
using GCodeGenerator.Models;

namespace GCodeGenerator.Tests.Fixtures
{
    /// <summary>
    /// Именованная фикстура: набор операций + настройки генератора.
    /// Используется в golden-тестах (пункт 0.4 плана) и дифференциальных тестах (фаза 4).
    /// </summary>
    public sealed class FixtureCase
    {
        public FixtureCase(string name, IList<OperationBase> operations, GCodeSettings settings)
        {
            Name = name;
            Operations = operations;
            Settings = settings;
        }

        /// <summary>Человекочитаемое имя, например "Drill.Line.Default".</summary>
        public string Name { get; }

        /// <summary>Операции программы (порядок важен).</summary>
        public IList<OperationBase> Operations { get; }

        /// <summary>Настройки генератора для этой фикстуры.</summary>
        public GCodeSettings Settings { get; }
    }
}
