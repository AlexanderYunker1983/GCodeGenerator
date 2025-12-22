using GCodeGenerator.Core.Attributes;

namespace GCodeGenerator.Core.Models;

/// <summary>
/// Примитив дуги окружности.
/// Углы задаются в градусах относительно оси X.
/// </summary>
public class ArcPrimitive : PrimitiveItem
{
    [PropertyEditor("Property_CenterX", Order = 10)]
    public double CenterX { get; set; }

    [PropertyEditor("Property_CenterY", Order = 20)]
    public double CenterY { get; set; }

    [PropertyEditor("Property_Radius", Order = 30)]
    public double Radius { get; set; }

    [PropertyEditor("Property_StartAngle", Order = 40)]
    public double StartAngle { get; set; }

    [PropertyEditor("Property_EndAngle", Order = 50)]
    public double EndAngle { get; set; }

    public ArcPrimitive(string name, double centerX, double centerY, double radius, double startAngle, double endAngle)
        : base(name)
    {
        CenterX = centerX;
        CenterY = centerY;
        Radius = radius;
        StartAngle = startAngle;
        EndAngle = endAngle;
    }
}


