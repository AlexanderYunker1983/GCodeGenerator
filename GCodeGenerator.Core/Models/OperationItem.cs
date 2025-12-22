namespace GCodeGenerator.Core.Models;

/// <summary>
/// Модель операции для списка операций.
/// </summary>
public class OperationItem
{
    public string Name { get; set; }

    /// <summary>
    /// Быстрое включение/отключение операции.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    public OperationItem(string name)
    {
        Name = name;
    }
}


