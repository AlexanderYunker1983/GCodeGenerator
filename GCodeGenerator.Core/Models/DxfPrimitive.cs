using System.Collections.Generic;
using GCodeGenerator.Core.Attributes;

namespace GCodeGenerator.Core.Models;

/// <summary>
/// DXF-объект, содержащий геометрические примитивы.
/// </summary>
public class DxfPrimitive : PrimitiveItem
{
    [PropertyEditor("Property_InsertX", Order = 10)]
    public double InsertX { get; set; }

    [PropertyEditor("Property_InsertY", Order = 20)]
    public double InsertY { get; set; }

    /// <summary>
    /// Путь к исходному DXF-файлу.
    /// </summary>
    [PropertyEditor("Property_SourceFilePath", Order = 30)]
    public string SourceFilePath { get; set; }

    /// <summary>
    /// Коллекция вложенных геометрических примитивов.
    /// </summary>
    public IList<PrimitiveItem> Children { get; } = new List<PrimitiveItem>();

    /// <summary>
    /// Угол поворота в градусах относительно оси X.
    /// </summary>
    [PropertyEditor("Property_RotationAngle", Order = 40)]
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


