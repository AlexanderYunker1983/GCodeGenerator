#nullable enable
namespace GCodeGenerator.Models
{
    /// <summary>
    /// Верхние границы размера одного задания. Они не описывают возможности
    /// станка; это пределы памяти и времени настольного приложения, чтобы
    /// повреждённый или враждебный проект не мог породить миллиарды точек.
    /// </summary>
    public static class GenerationLimits
    {
        public const int MaxOperations = 1000;
        public const int MaxHolesPerOperation = 10000;
        public const int MaxImportedContoursPerOperation = 10000;
        public const int MaxImportedPointsPerOperation = 200000;
        public const int MaxToolPathItems = 250000;
    }
}
