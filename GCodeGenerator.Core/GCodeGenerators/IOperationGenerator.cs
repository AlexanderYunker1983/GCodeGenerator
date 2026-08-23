using GCodeGenerator.Models;

namespace GCodeGenerator.GCodeGenerators
{
    /// <summary>
    /// Generates the structured G-code blocks of a single operation
    /// (plan item 4.4). Presentation (line numbers, padding, comments) is
    /// applied by <see cref="GCodeFormatter"/>.
    /// </summary>
    public interface IOperationGenerator
    {
        void Generate(OperationBase operation, ProgramBuilder builder, GCodeSettings settings);
    }
}
