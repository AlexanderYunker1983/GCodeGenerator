using GCodeGenerator.Core.Attributes;
using GCodeGenerator.Core.Localization;

namespace GCodeGenerator.Core.ViewModels.RightPanel.PrimitivesListViewModel;

public partial class PrimitivesListViewModel
{
    [Localized]
    public string DisplayName => Resources.GetString("RightPanel_PrimitivesList_DisplayName");
}


