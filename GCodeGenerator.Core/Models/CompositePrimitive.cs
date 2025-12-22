using System.Collections.Generic;

namespace GCodeGenerator.Core.Models;

/// <summary>
/// Составной объект, представляющий собой набор геометрических примитивов.
/// </summary>
public class CompositePrimitive : PrimitiveItem
{
    public double InsertX { get; set; }
    public double InsertY { get; set; }

    /// <summary>
    /// Коллекция вложенных геометрических примитивов.
    /// </summary>
    public IList<PrimitiveItem> Children { get; } = new List<PrimitiveItem>();

    /// <summary>
    /// Угол поворота в градусах относительно оси X.
    /// </summary>
    public double RotationAngle { get; set; }

    public CompositePrimitive(
        string name,
        double insertX,
        double insertY,
        double rotationAngle)
        : base(name)
    {
        InsertX = insertX;
        InsertY = insertY;
        RotationAngle = rotationAngle;
    }
}


