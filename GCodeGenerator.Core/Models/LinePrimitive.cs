using GCodeGenerator.Core.Attributes;

namespace GCodeGenerator.Core.Models;

/// <summary>
/// Линейный примитив (отрезок).
/// </summary>
public class LinePrimitive : PrimitiveItem
{
    [PropertyEditor("Property_X1", Order = 10)]
    public double X1 { get; set; }

    [PropertyEditor("Property_Y1", Order = 20)]
    public double Y1 { get; set; }

    [PropertyEditor("Property_X2", Order = 30)]
    public double X2 { get; set; }

    [PropertyEditor("Property_Y2", Order = 40)]
    public double Y2 { get; set; }

    public LinePrimitive(string name, double x1, double y1, double x2, double y2)
        : base(name)
    {
        X1 = x1;
        Y1 = y1;
        X2 = x2;
        Y2 = y2;
    }
}


