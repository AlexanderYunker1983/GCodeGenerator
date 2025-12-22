namespace GCodeGenerator.Core.Models;

/// <summary>
/// Правильный многогранник (многоугольник) с заданным числом граней.
/// </summary>
public class PolygonPrimitive : PrimitiveItem
{
    public double CenterX { get; set; }
    public double CenterY { get; set; }

    /// <summary>
    /// Радиус описанной окружности.
    /// </summary>
    public double CircumscribedRadius { get; set; }

    /// <summary>
    /// Количество граней (вершин).
    /// </summary>
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


