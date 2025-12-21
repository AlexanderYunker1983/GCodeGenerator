namespace GCodeGenerator.Core.Interfaces;

/// <summary>
/// Интерфейс для ViewModel, которые имеют отображаемое имя
/// </summary>
public interface IHasDisplayName
{
    /// <summary>
    /// Отображаемое имя, используемое в заголовке окна
    /// </summary>
    string DisplayName { get; }
}

