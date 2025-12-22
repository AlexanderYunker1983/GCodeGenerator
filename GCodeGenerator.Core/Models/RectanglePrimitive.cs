using GCodeGenerator.Core.Attributes;

namespace GCodeGenerator.Core.Models;

/// <summary>
/// Прямоугольный примитив.
/// </summary>
public class RectanglePrimitive : PrimitiveItem
{
    [PropertyEditor("Property_CenterX", Order = 10)]
    public double CenterX { get; set; }

    [PropertyEditor("Property_CenterY", Order = 20)]
    public double CenterY { get; set; }

    [PropertyEditor("Property_Width", Order = 30)]
    public double Width { get; set; }

    [PropertyEditor("Property_Height", Order = 40)]
    public double Height { get; set; }

    /// <summary>
    /// Угол поворота в градусах относительно оси X.
    /// </summary>
    [PropertyEditor("Property_RotationAngle", Order = 50)]
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


