using GCodeGenerator.Core.Attributes;

namespace GCodeGenerator.Core.Models;

/// <summary>
/// Правильный многогранник (многоугольник) с заданным числом граней.
/// </summary>
public class PolygonPrimitive : PrimitiveItem
{
    [PropertyEditor("Property_CenterX", Order = 10)]
    public double CenterX { get; set; }

    [PropertyEditor("Property_CenterY", Order = 20)]
    public double CenterY { get; set; }

    /// <summary>
    /// Радиус описанной окружности.
    /// </summary>
    [PropertyEditor("Property_Radius", Order = 30)]
    public double CircumscribedRadius { get; set; }

    /// <summary>
    /// Количество граней (вершин).
    /// </summary>
    [PropertyEditor("Property_SidesCount", Order = 40)]
    public int SidesCount { get; set; }

    public PolygonPrimitive(string name, double centerX, double centerY, double circumscribedRadius, int sidesCount)
        : base(name)
    {
        CenterX = centerX;
        CenterY = centerY;
        CircumscribedRadius = circumscribedRadius;
        SidesCount = sidesCount;
    }
}


