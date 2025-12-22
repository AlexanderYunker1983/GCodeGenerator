using CommunityToolkit.Mvvm.ComponentModel;
using GCodeGenerator.Core.Interfaces;
using GCodeGenerator.Core.Localization;

namespace GCodeGenerator.Core.ViewModels.RightPanel.GCodeViewModel;

public partial class GCodeViewModel : ViewModelBase, IHasDisplayName
{
    public GCodeViewModel()
    {
        InitializeResources();
    }

    [ObservableProperty]
    private string _gCodeText = string.Empty;
}


