using GCodeGenerator.Models;

namespace GCodeGenerator.ViewModels.Drill
{
    /// <summary>
    /// Контракт диалоговых view-моделей сверления (пункт 3.4 плана):
    /// позволяет <see cref="DrillOperationsViewModel.EditSelectedOperation"/>
    /// открывать нужный диалог по <see cref="DrillMode"/> операции,
    /// а не по её имени.
    /// </summary>
    public interface IDrillDialogViewModel
    {
        DrillOperationsViewModel MainViewModel { get; set; }

        DrillPointsOperation Operation { get; set; }
    }
}
