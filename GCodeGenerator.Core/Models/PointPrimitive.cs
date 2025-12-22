using GCodeGenerator.Core.Attributes;

namespace GCodeGenerator.Core.Models;

/// <summary>
/// Точечный примитив.
/// </summary>
public class PointPrimitive : PrimitiveItem
{
    [PropertyEditor("Property_CenterX", Order = 10)]
    public double X { get; set; }

    [PropertyEditor("Property_CenterY", Order = 20)]
    public double Y { get; set; }

    public PointPrimitive(string name, double x, double y)
        : base(name)
    {
        X = x;
        Y = y;
    }
}


