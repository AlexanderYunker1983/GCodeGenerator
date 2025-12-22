using GCodeGenerator.Core.Attributes;

namespace GCodeGenerator.Core.Models;

/// <summary>
/// Круг.
/// </summary>
public class CirclePrimitive : PrimitiveItem
{
    [PropertyEditor("Property_CenterX", Order = 10)]
    public double CenterX { get; set; }

    [PropertyEditor("Property_CenterY", Order = 20)]
    public double CenterY { get; set; }

    [PropertyEditor("Property_Radius", Order = 30)]
    public double Radius { get; set; }

    public CirclePrimitive(string name, double centerX, double centerY, double radius)
        : base(name)
    {
        CenterX = centerX;
        CenterY = centerY;
        Radius = radius;
    }
}


