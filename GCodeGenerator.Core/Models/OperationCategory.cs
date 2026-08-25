#nullable enable
namespace GCodeGenerator.Models
{
    /// <summary>
    /// Категория операции для группировки в UI (пункт 7.2 плана):
    /// единственный источник истины <c>MainViewModel.AllOperations</c>
    /// фильтруется по категории в под-VM вкладки (сверление / профиль / карман).
    /// </summary>
    public enum OperationCategory
    {
        Drill,
        Profile,
        Pocket
    }
}
