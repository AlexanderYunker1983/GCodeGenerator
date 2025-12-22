namespace GCodeGenerator.Core.Models;

/// <summary>
/// Модель примитива для списка примитивов.
/// </summary>
public class PrimitiveItem
{
    public string Name { get; set; }

    public PrimitiveItem(string name)
    {
        Name = name;
    }
}


