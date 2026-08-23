using System.Collections.ObjectModel;
using GCodeGenerator.Models;

namespace GCodeGenerator.ViewModels.Drill
{
    /// <summary>
    /// Контракт диалоговых view-моделей сверления (пункт 3.4 плана):
    /// позволяет открывать нужный диалог по <see cref="DrillMode"/> операции,
    /// а не по её имени. Пункт 7.2: диалог работает с единой коллекцией
    /// операций (MainViewModel.AllOperations) вместо ссылки на под-VM.
    /// </summary>
    public interface IDrillDialogViewModel
    {
        ObservableCollection<OperationBase> Operations { get; set; }

        DrillPointsOperation Operation { get; set; }
    }
}
