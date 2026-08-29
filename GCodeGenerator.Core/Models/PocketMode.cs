#nullable enable
namespace GCodeGenerator.Models
{
    /// <summary>
    /// Назначение геометрии операции кармана.
    /// </summary>
    public enum PocketMode
    {
        /// <summary>Область выбирается инструментом.</summary>
        Machining = 0,

        /// <summary>
        /// Область остаётся необработанной и вычитается из всех обычных
        /// операций карманов проекта.
        /// </summary>
        Island = 1,
    }
}
