#nullable enable
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
    /// Реализации: <see cref="GenericPostProcessor"/> (повторяет прежний
    /// вывод байт в байт) и <see cref="GrblPostProcessor"/>; выбирается по
    /// <see cref="GCodeFormatSettings.PostProcessorName"/> через
    /// <see cref="IPostProcessorRegistry"/>.
    /// </summary>
    public interface IPostProcessor
    {
        /// <summary>
        /// Ключ в настройках и файле проекта: короткий, латиницей, стабильный.
        /// Человеку показывается <see cref="Name"/>, ключ же не переводится
        /// и не меняется — по нему проект, открытый через годы, находит ту же
        /// стойку.
        /// </summary>
        string Key { get; }

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
