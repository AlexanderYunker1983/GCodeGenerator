#nullable enable
using System.Threading;
using GCodeGenerator.Models;

using GCodeGenerator.Toolpath;

namespace GCodeGenerator.GCodeGenerators
{
    /// <summary>
    /// Generates the structured G-code blocks of a single operation
    /// (plan item 4.4). Presentation (line numbers, padding, comments) is
    /// applied by <see cref="GCodeFormatter"/>.
    /// </summary>
    public interface IOperationGenerator
    {
        /// <param name="operation">Операция документа.</param>
        /// <param name="builder">Построитель траектории.</param>
        /// <param name="settings">Настройки генерации.</param>
        /// <param name="cancellation">
        /// Отмена: проверяется между единицами работы операции — слоями и
        /// отверстиями. Одна операция может строиться заметное время
        /// (глубокий карман — это сотни слоёв), и прежде отменить её можно
        /// было только целиком.
        /// </param>
        void Generate(
            OperationBase operation,
            ToolPathBuilder builder,
            GCodeSettings settings,
            CancellationToken cancellation = default);
    }
}
