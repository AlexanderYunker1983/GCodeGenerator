using GCodeGenerator.Core.Attributes;
using GCodeGenerator.Core.Localization;

namespace GCodeGenerator.Core.ViewModels.RightPanel.OperationsListViewModel;

public partial class OperationsListViewModel
{
    [Localized]
    public string DisplayName => Resources.GetString("RightPanel_OperationsList_DisplayName");
}


