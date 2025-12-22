using GCodeGenerator.Core.Attributes;

namespace GCodeGenerator.Core.Models;

/// <summary>
/// Эллипс.
/// </summary>
public class EllipsePrimitive : PrimitiveItem
{
    [PropertyEditor("Property_CenterX", Order = 10)]
    public double CenterX { get; set; }

    [PropertyEditor("Property_CenterY", Order = 20)]
    public double CenterY { get; set; }

    [PropertyEditor("Property_Radius1", Order = 30)]
    public double Radius1 { get; set; }

    [PropertyEditor("Property_Radius2", Order = 40)]
    public double Radius2 { get; set; }

    /// <summary>
    /// Угол поворота в градусах относительно оси X.
    /// </summary>
    [PropertyEditor("Property_RotationAngle", Order = 50)]
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


