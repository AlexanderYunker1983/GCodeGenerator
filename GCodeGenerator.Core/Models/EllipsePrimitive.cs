namespace GCodeGenerator.Core.Models;

/// <summary>
/// Эллипс.
/// </summary>
public class EllipsePrimitive : PrimitiveItem
{
    public double CenterX { get; set; }
    public double CenterY { get; set; }
    public double Radius1 { get; set; }
    public double Radius2 { get; set; }

    /// <summary>
    /// Угол поворота в градусах относительно оси X.
    /// </summary>
    public double RotationAngle { get; set; }

    public EllipsePrimitive(
        string name,
        double centerX,
        double centerY,
        double radius1,
        double radius2,
        double rotationAngle)
        : base(name)
    {
        CenterX = centerX;
        CenterY = centerY;
        Radius1 = radius1;
        Radius2 = radius2;
        RotationAngle = rotationAngle;
    }
}


