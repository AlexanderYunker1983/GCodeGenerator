using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using GCodeGenerator.Models;

namespace GCodeGenerator.Services
{
    /// <summary>Разбирает поддерживаемые DXF-сущности в независимые полилинии.</summary>
    internal static class DxfEntityReader
    {
        private const double ClosedContourTolerance = 0.001;

        internal static List<DxfPolyline> Read(string path, bool includePolylineEntities)
        {
            var allPolylines = new List<DxfPolyline>();
            var lines = File.ReadAllLines(path);
            int i = 0;

            double Parse(string v)
            {
                if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                    return d;
                return 0;
            }

            // Парсим все полилинии из DXF
            while (i < lines.Length)
            {
                var code = lines[i].Trim();
                i++;

                if (string.Equals(code, "LINE", StringComparison.OrdinalIgnoreCase))
                {
                    double? x1 = null, y1 = null, x2 = null, y2 = null;
                    while (i + 1 < lines.Length)
                    {
                        var groupCode = lines[i].Trim();
                        var value = lines[i + 1].Trim();
                        i += 2;

                        switch (groupCode)
                        {
                            case "10": x1 = Parse(value); break;
                            case "20": y1 = Parse(value); break;
                            case "11": x2 = Parse(value); break;
                            case "21": y2 = Parse(value); break;
                            case "39": break; // Thickness - игнорируем
                            case "0": i -= 2; goto EndLine;
                        }
                    }
                EndLine:
                    if (x1.HasValue && y1.HasValue && x2.HasValue && y2.HasValue)
                    {
                        allPolylines.Add(new DxfPolyline
                        {
                            Points = new List<DxfPoint>
                            {
                                new DxfPoint { X = x1.Value, Y = y1.Value },
                                new DxfPoint { X = x2.Value, Y = y2.Value }
                            }
                        });
                    }
                    continue;
                }
                else if (string.Equals(code, "CIRCLE", StringComparison.OrdinalIgnoreCase))
                {
                    double? cx = null, cy = null, radius = null;
                    while (i + 1 < lines.Length)
                    {
                        var groupCode = lines[i].Trim();
                        var value = lines[i + 1].Trim();
                        i += 2;

                        switch (groupCode)
                        {
                            case "10": cx = Parse(value); break;
                            case "20": cy = Parse(value); break;
                            case "40": radius = Parse(value); break;
                            case "0": i -= 2; goto EndCircle;
                        }
                    }
                EndCircle:
                    if (cx.HasValue && cy.HasValue && radius.HasValue && radius.Value > 0)
                    {
                        var circlePoints = ApproximateCircle(cx.Value, cy.Value, radius.Value);
                        allPolylines.Add(new DxfPolyline { Points = circlePoints });
                    }
                    continue;
                }
                else if (string.Equals(code, "ARC", StringComparison.OrdinalIgnoreCase))
                {
                    double? cx = null, cy = null, radius = null, startAngle = null, endAngle = null;
                    while (i + 1 < lines.Length)
                    {
                        var groupCode = lines[i].Trim();
                        var value = lines[i + 1].Trim();
                        i += 2;

                        switch (groupCode)
                        {
                            case "10": cx = Parse(value); break;
                            case "20": cy = Parse(value); break;
                            case "40": radius = Parse(value); break;
                            case "50": startAngle = Parse(value); break;
                            case "51": endAngle = Parse(value); break;
                            case "0": i -= 2; goto EndArc;
                        }
                    }
                EndArc:
                    // Дуги могут быть частью замкнутого контура из нескольких сегментов
                    // Добавляем их как сегменты для последующего соединения
                    if (cx.HasValue && cy.HasValue && radius.HasValue && radius.Value > 0 && 
                        startAngle.HasValue && endAngle.HasValue)
                    {
                        var arcPoints = ApproximateArc(cx.Value, cy.Value, radius.Value, 
                            startAngle.Value, endAngle.Value);
                        allPolylines.Add(new DxfPolyline { Points = arcPoints });
                    }
                    continue;
                }
                else if (string.Equals(code, "ELLIPSE", StringComparison.OrdinalIgnoreCase))
                {
                    double? centerX = null, centerY = null;
                    double? majorEndX = null, majorEndY = null;
                    double? ratio = null;
                    double? startParam = null, endParam = null;
                    while (i + 1 < lines.Length)
                    {
                        var groupCode = lines[i].Trim();
                        var value = lines[i + 1].Trim();
                        i += 2;

                        switch (groupCode)
                        {
                            case "10": centerX = Parse(value); break;
                            case "20": centerY = Parse(value); break;
                            case "11": majorEndX = Parse(value); break;
                            case "21": majorEndY = Parse(value); break;
                            case "40": ratio = Parse(value); break;
                            case "41": startParam = Parse(value); break;
                            case "42": endParam = Parse(value); break;
                            case "0": i -= 2; goto EndEllipse;
                        }
                    }
                EndEllipse:
                    if (centerX.HasValue && centerY.HasValue && majorEndX.HasValue && majorEndY.HasValue && 
                        ratio.HasValue && ratio.Value > 0 && startParam.HasValue && endParam.HasValue)
                    {
                        var ellipsePoints = ApproximateEllipse(centerX.Value, centerY.Value,
                            majorEndX.Value, majorEndY.Value, ratio.Value,
                            startParam.Value, endParam.Value);
                        allPolylines.Add(new DxfPolyline { Points = ellipsePoints });
                    }
                    continue;
                }
                else if (includePolylineEntities &&
                         (string.Equals(code, "LWPOLYLINE", StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(code, "POLYLINE", StringComparison.OrdinalIgnoreCase)))
                {
                    var polylinePoints = new List<DxfPoint>();
                    bool isClosed = false;
                    while (i + 1 < lines.Length)
                    {
                        var groupCode = lines[i].Trim();
                        var value = lines[i + 1].Trim();
                        i += 2;

                        switch (groupCode)
                        {
                            case "70": // Flags
                                isClosed = (int.Parse(value) & 1) != 0; // Bit 0 = closed
                                break;
                            case "10": // X coordinate
                                var x = Parse(value);
                                var y = 0.0;
                                if (i < lines.Length && lines[i].Trim() == "20")
                                {
                                    i++;
                                    if (i < lines.Length)
                                        y = Parse(lines[i].Trim());
                                }
                                polylinePoints.Add(new DxfPoint { X = x, Y = y });
                                break;
                            case "0":
                                if (string.Equals(value, "VERTEX", StringComparison.OrdinalIgnoreCase))
                                {
                                    // Читаем вершину
                                    double? vx = null, vy = null;
                                    while (i + 1 < lines.Length)
                                    {
                                        var vGroupCode = lines[i].Trim();
                                        var vValue = lines[i + 1].Trim();
                                        i += 2;

                                        switch (vGroupCode)
                                        {
                                            case "10": vx = Parse(vValue); break;
                                            case "20": vy = Parse(vValue); break;
                                            case "0":
                                                i -= 2;
                                                goto EndVertex;
                                        }
                                    }
                                EndVertex:
                                    if (vx.HasValue && vy.HasValue)
                                        polylinePoints.Add(new DxfPoint { X = vx.Value, Y = vy.Value });
                                }
                                else
                                {
                                    i -= 2;
                                    goto EndPolyline;
                                }
                                break;
                        }
                    }
                EndPolyline:
                    if (polylinePoints.Count >= 3)
                    {
                        // Если полилиния помечена как замкнутая, добавляем первую точку в конец
                        if (isClosed && polylinePoints.Count > 0)
                        {
                            var firstPoint = polylinePoints[0];
                            var lastPoint = polylinePoints[polylinePoints.Count - 1];
                            if (Math.Abs(firstPoint.X - lastPoint.X) > ClosedContourTolerance ||
                                Math.Abs(firstPoint.Y - lastPoint.Y) > ClosedContourTolerance)
                            {
                                polylinePoints.Add(new DxfPoint { X = firstPoint.X, Y = firstPoint.Y });
                            }
                        }
                        allPolylines.Add(new DxfPolyline { Points = polylinePoints });
                    }
                    continue;
                }
            }

            return allPolylines;
        }

        private static List<DxfPoint> ApproximateCircle(double centerX, double centerY, double radius)
        {
            const int segments = 32;
            var points = new List<DxfPoint>();
            for (int i = 0; i <= segments; i++)
            {
                var angle = 2.0 * Math.PI * i / segments;
                points.Add(new DxfPoint
                {
                    X = centerX + radius * Math.Cos(angle),
                    Y = centerY + radius * Math.Sin(angle)
                });
            }
            return points;
        }

        private static List<DxfPoint> ApproximateArc(double centerX, double centerY, double radius,
            double startAngleDeg, double endAngleDeg)
        {
            const int minSegments = 8;
            var startAngle = startAngleDeg * Math.PI / 180.0;
            var endAngle = endAngleDeg * Math.PI / 180.0;

            while (endAngle < startAngle)
                endAngle += 2.0 * Math.PI;

            var angleSpan = endAngle - startAngle;
            var segments = Math.Max(minSegments, (int)(angleSpan / (Math.PI / 16.0)));

            var points = new List<DxfPoint>();
            for (int i = 0; i <= segments; i++)
            {
                var angle = startAngle + angleSpan * i / segments;
                points.Add(new DxfPoint
                {
                    X = centerX + radius * Math.Cos(angle),
                    Y = centerY + radius * Math.Sin(angle)
                });
            }
            return points;
        }

        private static List<DxfPoint> ApproximateEllipse(double centerX, double centerY,
            double majorEndX, double majorEndY, double ratio,
            double startParam, double endParam)
        {
            // В DXF: (11, 21) - это конечная точка большой оси ОТНОСИТЕЛЬНО ЦЕНТРА (вектор от центра)
            // Это стандарт для DXF ELLIPSE - координаты задаются относительно центра
            // Используем (11, 21) напрямую как вектор от центра
            double majorRadius = Math.Sqrt(majorEndX * majorEndX + majorEndY * majorEndY);
            
            // Проверяем, что радиус не нулевой
            if (majorRadius < 1e-9)
                return new List<DxfPoint>();
            
            // Малая полуось = большая полуось * соотношение
            double minorRadius = majorRadius * ratio;

            // Вычисляем угол поворота большой оси (направление вектора)
            double rotationAngle = Math.Atan2(majorEndY, majorEndX);

            // Нормализуем параметры (в DXF параметры заданы в радианах)
            double normalizedStartParam = startParam;
            double normalizedEndParam = endParam;
            while (normalizedEndParam < normalizedStartParam)
                normalizedEndParam += 2.0 * Math.PI;

            const int minSegments = 32;
            var paramSpan = normalizedEndParam - normalizedStartParam;
            var segments = Math.Max(minSegments, (int)(paramSpan / (Math.PI / 16.0)));

            var points = new List<DxfPoint>();
            double cosRot = Math.Cos(rotationAngle);
            double sinRot = Math.Sin(rotationAngle);
            
            for (int i = 0; i <= segments; i++)
            {
                var param = normalizedStartParam + paramSpan * i / segments;
                // Параметрическое уравнение эллипса в локальной системе координат
                // где большая ось направлена по оси X, малая по оси Y
                // x = a * cos(t), y = b * sin(t), где a = majorRadius, b = minorRadius
                double xLocal = majorRadius * Math.Cos(param);
                double yLocal = minorRadius * Math.Sin(param);
                
                // Поворачиваем на угол rotationAngle (чтобы совместить локальную ось X с направлением большой оси)
                // и переносим в центр
                double rotatedX = xLocal * cosRot - yLocal * sinRot;
                double rotatedY = xLocal * sinRot + yLocal * cosRot;
                
                points.Add(new DxfPoint
                {
                    X = centerX + rotatedX,
                    Y = centerY + rotatedY
                });
            }
            return points;
        }

    }
}
