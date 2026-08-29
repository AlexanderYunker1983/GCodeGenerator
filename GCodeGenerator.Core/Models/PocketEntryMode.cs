#nullable enable
namespace GCodeGenerator.Models
{
    /// <summary>Способ входа фрезы в очередной слой кармана.</summary>
    public enum PocketEntryMode
    {
        /// <summary>Вертикальное врезание в точке входа.</summary>
        Vertical = 0,

        /// <summary>Винтовой спуск по окружности заданного диаметра.</summary>
        Helical = 1
    }
}
