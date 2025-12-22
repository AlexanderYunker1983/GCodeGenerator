using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using GCodeGenerator.Core.Interfaces;
using GCodeGenerator.Core.Models;

namespace GCodeGenerator.Core.ViewModels.RightPanel.PrimitivesListViewModel;

public partial class PrimitivesListViewModel : ViewModelBase, IHasDisplayName
{
    public ObservableCollection<PrimitiveItem> Primitives { get; } = new();

    [ObservableProperty]
    private PrimitiveItem? _selectedPrimitive;

    public PrimitivesListViewModel()
    {
        InitializeResources();

        // Временный список примитивов.
        Primitives.Add(new PrimitiveItem("Точка"));
        Primitives.Add(new PrimitiveItem("Линия"));
        Primitives.Add(new PrimitiveItem("Эллипс"));
        Primitives.Add(new PrimitiveItem("Квадрат"));
        Primitives.Add(new PrimitiveItem("Импорт DXF"));
    }
}


