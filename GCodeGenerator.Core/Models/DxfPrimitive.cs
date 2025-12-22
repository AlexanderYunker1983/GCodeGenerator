using System.Collections.Generic;

namespace GCodeGenerator.Core.Models;

/// <summary>
/// DXF-объект, содержащий геометрические примитивы.
/// </summary>
public class DxfPrimitive : PrimitiveItem
{
    public double InsertX { get; set; }
    public double InsertY { get; set; }

    /// <summary>
    /// Путь к исходному DXF-файлу.
    /// </summary>
    public string SourceFilePath { get; set; }

    /// <summary>
    /// Коллекция вложенных геометрических примитивов.
    /// </summary>
    public IList<PrimitiveItem> Children { get; } = new List<PrimitiveItem>();

    /// <summary>
    /// Угол поворота в градусах относительно оси X.
    /// </summary>
    public double RotationAngle { get; set; }

    public DxfPrimitive(
        string name,
        double insertX,
        double insertY,
        string sourceFilePath,
        double rotationAngle)
        : base(name)
    {
        InsertX = insertX;
        InsertY = insertY;
        SourceFilePath = sourceFilePath;
        RotationAngle = rotationAngle;
    }
}


