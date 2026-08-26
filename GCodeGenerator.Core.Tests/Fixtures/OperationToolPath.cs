using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.Models;
using GCodeGenerator.Toolpath;

namespace GCodeGenerator.Tests.Fixtures
{
    /// <summary>
    /// Запуск генератора одной операции для тестов, которым нужен готовый
    /// текст программы.
    ///
    /// Генераторы описывают траекторию, а программу из неё делает
    /// постпроцессор, поэтому проверка вывода складывается из двух шагов —
    /// здесь они собраны в один, чтобы тесты остались о том, что проверяют.
    /// </summary>
    internal static class OperationToolPath
    {
        /// <summary>Траектория одной операции.</summary>
        public static ToolPath Build(IOperationGenerator generator, OperationBase operation, GCodeSettings settings)
        {
            var decimals = operation is MillingOperationBase milling
                ? milling.Decimals
                : (operation as DrillPointsOperation)?.Decimals ?? 3;

            var pathOperation = new ToolPathOperation(operation.Name, operation.GetDescription(), decimals);
            generator.Generate(operation, new ToolPathBuilder(pathOperation), settings);

            var toolPath = new ToolPath();
            toolPath.AddOperation(pathOperation);
            return toolPath;
        }

        /// <summary>Программа одной операции: траектория плюс постпроцессор.</summary>
        public static GCodeProgram Program(
            IOperationGenerator generator,
            OperationBase operation,
            GCodeSettings settings,
            GCodeSettings renderSettings = null)
        {
            var toolPath = Build(generator, operation, settings);
            return new GenericPostProcessor().Build(toolPath, renderSettings ?? settings);
        }
    }
}
