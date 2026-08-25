using GCodeGenerator.Models;
using GCodeGenerator.Toolpath;

namespace GCodeGenerator.GCodeGenerators
{
    /// <summary>
    /// Превращает траекторию инструмента в программу для конкретной стойки.
    ///
    /// Здесь собрано всё, что зависит от станка, а не от детали: единицы
    /// и режимы в начале программы, вид команд шпинделя и охлаждения, единица
    /// аргумента паузы, завершение программы. Раньше это было размазано по
    /// генератору и построителю программы, поэтому вывод годился ровно для
    /// одной стойки, а какой именно — нигде не было записано.
    ///
    /// Первая реализация — <see cref="GenericPostProcessor"/>: она повторяет
    /// прежний вывод байт в байт.
    /// </summary>
    public interface IPostProcessor
    {
        /// <summary>Название стойки или семейства, для которого годится вывод.</summary>
        string Name { get; }

        /// <summary>
        /// Строит программу по траектории и настройкам генерации.
        /// </summary>
        /// <param name="toolPath">Траектория инструмента.</param>
        /// <param name="settings">Настройки генерации.</param>
        GCodeProgram Build(ToolPath toolPath, GCodeSettings settings);
    }
}
