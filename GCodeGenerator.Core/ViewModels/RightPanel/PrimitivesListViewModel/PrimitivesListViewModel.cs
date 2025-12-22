using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using GCodeGenerator.Core.Interfaces;
using Preview2DViewModelNamespace = GCodeGenerator.Core.ViewModels.Preview2DViewModel;
using GCodeGenerator.Core.Models;

namespace GCodeGenerator.Core.ViewModels.RightPanel.PrimitivesListViewModel;

public partial class PrimitivesListViewModel : ViewModelBase, IHasDisplayName
{
    private readonly Preview2DViewModelNamespace.Preview2DViewModel _preview2DViewModel;

    public ObservableCollection<PrimitiveItem> Primitives => _preview2DViewModel.Primitives;

    [ObservableProperty]
    private PrimitiveItem? _selectedPrimitive;

    public PrimitivesListViewModel(Preview2DViewModelNamespace.Preview2DViewModel preview2DViewModel)
    {
        InitializeResources();
        _preview2DViewModel = preview2DViewModel;

        // Временный список примитивов с реальными данными недалеко от начала координат.
        Primitives.Add(new PointPrimitive("Точка (0,0)", 0, 0));
        Primitives.Add(new LinePrimitive("Линия (0,0)-(10,0)", 0, 0, 10, 0));
        Primitives.Add(new CirclePrimitive("Круг (0,0), R=5", 0, 0, 5));
        Primitives.Add(new RectanglePrimitive("Прямоугольник (0,0) 10x5", 0, 0, 10, 5, 0));
        Primitives.Add(new EllipsePrimitive("Эллипс (0,0) 8x4", 0, 0, 8, 4, 0));
        Primitives.Add(new ArcPrimitive("Дуга (0,0), R=5, 0-90°", 0, 0, 5, 0, 90));
        Primitives.Add(new PolygonPrimitive("Многогранник (0,0), R=5, 6 граней", 0, 0, 5, 6));

        var dxf = new DxfPrimitive("DXF-объект (0,0)", 0, 0, "demo.dxf", 0);
        dxf.Children.Add(new LinePrimitive("DXF-линия", -5, -5, 5, 5));
        Primitives.Add(dxf);

        var composite = new CompositePrimitive("Составной объект (5,5)", 5, 5, 0);
        composite.Children.Add(new CirclePrimitive("Вложенный круг", 0, 0, 2));
        composite.Children.Add(new RectanglePrimitive("Вложенный прямоугольник", 0, 0, 4, 2, 45));
        Primitives.Add(composite);
    }

    partial void OnSelectedPrimitiveChanged(PrimitiveItem? value)
    {
        _preview2DViewModel.SelectedPrimitive = value;
    }
}

