namespace GCodeGenerator.Core.Models;

/// <summary>
/// Точечный примитив.
/// </summary>
public class PointPrimitive : PrimitiveItem
{
    public double X { get; set; }
    public double Y { get; set; }

    public PointPrimitive(string name, double x, double y)
        : base(name)
    {
        X = x;
        Y = y;
    }
}


