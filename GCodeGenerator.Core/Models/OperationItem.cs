using GCodeGenerator.Core.Attributes;

namespace GCodeGenerator.Core.Models;

/// <summary>
/// Модель операции для списка операций.
/// </summary>
public class OperationItem
{
    [PropertyEditor("Property_Name", Order = 10)]
    public string Name { get; set; }

    /// <summary>
    /// Быстрое включение/отключение операции.
    /// </summary>
    [PropertyEditor("Property_IsEnabled", Order = 20)]
    public bool IsEnabled { get; set; } = true;

    public OperationItem(string name)
    {
        Name = name;
    }
}


