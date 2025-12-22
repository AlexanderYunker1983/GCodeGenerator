namespace GCodeGenerator.Core.Models;

/// <summary>
/// Круг.
/// </summary>
public class CirclePrimitive : PrimitiveItem
{
    public double CenterX { get; set; }
    public double CenterY { get; set; }
    public double Radius { get; set; }

    public CirclePrimitive(string name, double centerX, double centerY, double radius)
        : base(name)
    {
        CenterX = centerX;
        CenterY = centerY;
        Radius = radius;
    }
}


