namespace GCodeGenerator.Core.Models;

/// <summary>
/// Базовый класс для всех геометрических примитивов.
/// </summary>
public abstract class PrimitiveItem
{
    /// <summary>
    /// Пользовательское название примитива.
    /// </summary>
    public string Name { get; set; }

    protected PrimitiveItem(string name)
    {
        Name = name;
    }
}
