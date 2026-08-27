#nullable enable
namespace GCodeGenerator.Models
{
    /// <summary>Способ входа фрезы в очередной слой кармана.</summary>
    public enum PocketEntryMode
    {
        /// <summary>Вертикальное врезание в точке входа.</summary>
        Vertical,

        /// <summary>Винтовой спуск по окружности заданного диаметра.</summary>
        Helical
    }
}
