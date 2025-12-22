namespace GCodeGenerator.Core.Models;

/// <summary>
/// Примитив дуги окружности.
/// Углы задаются в градусах относительно оси X.
/// </summary>
public class ArcPrimitive : PrimitiveItem
{
    public double CenterX { get; set; }
    public double CenterY { get; set; }
    public double Radius { get; set; }
    public double StartAngle { get; set; }
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


