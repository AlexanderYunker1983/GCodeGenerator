namespace GCodeGenerator.Core.Models;

/// <summary>
/// Линейный примитив (отрезок).
/// </summary>
public class LinePrimitive : PrimitiveItem
{
    public double X1 { get; set; }
    public double Y1 { get; set; }
    public double X2 { get; set; }
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


