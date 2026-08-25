#nullable enable
namespace GCodeGenerator.ViewModels
{
    /// <summary>
    /// Интерфейс view-моделей с отображаемым именем (пункт 1.3 плана): замена
    /// <c>MugenMvvmToolkit.Interfaces.Models.IHasDisplayName</c>.
    /// </summary>
    public interface IHasDisplayName
    {
        string DisplayName { get; }
    }
}
