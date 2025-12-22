namespace GCodeGenerator.Core.Models;

/// <summary>
/// Прямоугольный примитив.
/// </summary>
public class RectanglePrimitive : PrimitiveItem
{
    public double CenterX { get; set; }
    public double CenterY { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }

    /// <summary>
    /// Угол поворота в градусах относительно оси X.
    /// </summary>
    public double RotationAngle { get; set; }

    public RectanglePrimitive(
        string name,
        double centerX,
        double centerY,
        double width,
        double height,
        double rotationAngle)
        : base(name)
    {
        CenterX = centerX;
        CenterY = centerY;
        Width = width;
        Height = height;
        RotationAngle = rotationAngle;
    }
}


