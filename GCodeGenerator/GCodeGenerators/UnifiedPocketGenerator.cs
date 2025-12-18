using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GCodeGenerator.GCodeGenerators.Geometry;
using GCodeGenerator.GCodeGenerators.Helpers;
using GCodeGenerator.GCodeGenerators.Interfaces;
using GCodeGenerator.Models;

namespace GCodeGenerator.GCodeGenerators
{
    /// <summary>
    /// Единый генератор для всех типов карманов.
    /// Использует интерфейсы геометрии и классы-помощники для унификации логики.
    /// </summary>
    public class UnifiedPocketGenerator : IOperationGenerator
    {
        private readonly PocketGenerationHelper _helper;

        public UnifiedPocketGenerator()
        {
            _helper = new PocketGenerationHelper();
        }

        public void Generate(
            OperationBase operation,
            Action<string> addLine,
            string g0,
            string g1,
            GCodeSettings settings)
        {
            // Проверяем, что операция является карманом
            if (!(operation is IPocketOperation pocketOp))
                return;

            // Создаем геометрию кармана
            var geometry = PocketGeometryFactory.Create(operation);
            if (geometry == null)
                return;

            // Временно: генерируем только основную обработку без roughing/finishing
            GenerateInternal(pocketOp, geometry, addLine, g0, g1, settings);
        }

        /// <summary>
        /// Генерирует внутреннюю обработку кармана (без учета rough/finish).
        /// </summary>
        private void GenerateInternal(
            IPocketOperation op,
            IPocketGeometry geometry,
            Action<string> addLine,
            string g0,
            string g1,
            GCodeSettings settings)
        {
            var fmt = $"0.{new string('0', op.Decimals)}";
            var culture = CultureInfo.InvariantCulture;

            double toolRadius = op.ToolDiameter / 2.0;
            double stepPercent = (op.StepPercentOfTool <= 0) ? 40 : op.StepPercentOfTool;
            double step = GCodeGenerationHelper.CalculateStep(op.ToolDiameter, stepPercent);

            // Генерируем цикл по слоям
            _helper.GenerateLayerLoop(
                op,
                (currentZ, nextZ, passNumber) => GenerateLayer(
                    op,
                    geometry,
                    toolRadius,
                    step,
                    currentZ,
                    nextZ,
                    addLine,
                    g0,
                    g1,
                    settings),
                addLine,
                g0,
                g1,
                settings);
        }

        /// <summary>
        /// Генерирует один слой кармана.
        /// </summary>
        private void GenerateLayer(
            IPocketOperation op,
            IPocketGeometry geometry,
            double toolRadius,
            double step,
            double currentZ,
            double nextZ,
            Action<string> addLine,
            string g0,
            string g1,
            GCodeSettings settings)
        {
            var fmt = $"0.{new string('0', op.Decimals)}";
            var culture = CultureInfo.InvariantCulture;

            double depthFromTop = op.ContourHeight - nextZ;
            double taperOffset = GCodeGenerationHelper.CalculateTaperOffset(depthFromTop, op.WallTaperAngleDeg);

            // Получаем контур кармана
            var contour = geometry.GetContour(toolRadius, taperOffset);
            if (contour == null)
                return;

            var center = geometry.GetCenter();
            var contourPoints = contour.GetPoints().ToList();
            if (contourPoints.Count == 0)
                return;

            // Перемещаемся к центру кармана
            addLine($"{g0} Z{op.SafeZHeight.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
            addLine($"{g0} X{center.x.ToString(fmt, culture)} Y{center.y.ToString(fmt, culture)} F{op.FeedXYRapid.ToString(fmt, culture)}");
            addLine($"{g0} Z{currentZ.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
            addLine($"{g1} Z{nextZ.ToString(fmt, culture)} F{op.FeedZWork.ToString(fmt, culture)}");

            // Генерируем спиральную стратегию (временно - только Spiral)
            GenerateSpiralStrategy(op, geometry, toolRadius, taperOffset, step, addLine, g0, g1, fmt, culture, settings);

            // Возврат в центр и подъем
            addLine($"{g1} X{center.x.ToString(fmt, culture)} Y{center.y.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
            addLine($"{g0} Z{op.SafeZHeight.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
        }

        /// <summary>
        /// Генерирует спиральную стратегию обработки.
        /// </summary>
        private void GenerateSpiralStrategy(
            IPocketOperation op,
            IPocketGeometry geometry,
            double toolRadius,
            double taperOffset,
            double step,
            Action<string> addLine,
            string g0,
            string g1,
            string fmt,
            CultureInfo culture,
            GCodeSettings settings)
        {
            var contour = geometry.GetContour(toolRadius, taperOffset);
            if (contour == null)
                return;

            var center = geometry.GetCenter();
            var contourPoints = contour.GetPoints().ToList();
            if (contourPoints.Count == 0)
                return;

            // Находим максимальное расстояние от центра до контура
            double maxDistance = 0.0;
            foreach (var point in contourPoints)
            {
                double dx = point.x - center.x;
                double dy = point.y - center.y;
                double distance = Math.Sqrt(dx * dx + dy * dy);
                if (distance > maxDistance)
                    maxDistance = distance;
            }

            if (maxDistance <= 0)
                return;

            // Параметры спирали: r = a + b*θ
            // a = 0 (начинаем с центра)
            // b = step / (2*π) (шаг спирали)
            double a = 0.0;
            double b = step / (2.0 * Math.PI);

            // Направление спирали
            double dirSign = op.Direction == MillingDirection.Clockwise ? -1.0 : 1.0;

            // Максимальный угол для достижения внешнего радиуса
            double θMax = maxDistance / b;

            // Количество точек на оборот для плавности
            int pointsPerRevolution = 128;
            double stepAngle = 2.0 * Math.PI / pointsPerRevolution;

            // Начинаем с центра
            addLine($"{g1} X{center.x.ToString(fmt, culture)} Y{center.y.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");

            // Генерируем спираль до достижения внешнего радиуса
            for (double θ = stepAngle; θ <= θMax; θ += stepAngle)
            {
                double r = a + b * θ;
                
                // Проверяем, что точка находится внутри контура
                double testX = center.x + r;
                double testY = center.y;
                if (!geometry.IsPointInside(testX, testY, toolRadius, taperOffset))
                    continue;

                double ang = θ * dirSign;
                double x = center.x + r * Math.Cos(ang);
                double y = center.y + r * Math.Sin(ang);
                addLine($"{g1} X{x.ToString(fmt, culture)} Y{y.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
            }

            // Доводим до внешнего контура
            double finalR = a + b * θMax;
            double finalAng = θMax * dirSign;
            double finalX = center.x + finalR * Math.Cos(finalAng);
            double finalY = center.y + finalR * Math.Sin(finalAng);
            addLine($"{g1} X{finalX.ToString(fmt, culture)} Y{finalY.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");

            // Обрабатываем внешний контур - находим ближайшую точку контура и идем по нему
            if (contourPoints.Count > 0)
            {
                // Находим ближайшую точку контура к текущей позиции
                int closestIndex = 0;
                double minDist = double.MaxValue;
                for (int i = 0; i < contourPoints.Count; i++)
                {
                    double dx = contourPoints[i].x - finalX;
                    double dy = contourPoints[i].y - finalY;
                    double dist = Math.Sqrt(dx * dx + dy * dy);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        closestIndex = i;
                    }
                }

                // Идем по контуру, начиная с ближайшей точки
                for (int i = 0; i < contourPoints.Count; i++)
                {
                    int idx = (closestIndex + i) % contourPoints.Count;
                    var point = contourPoints[idx];
                    addLine($"{g1} X{point.x.ToString(fmt, culture)} Y{point.y.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
                }

                // Замыкаем контур
                if (closestIndex > 0)
                {
                    var firstPoint = contourPoints[closestIndex];
                    addLine($"{g1} X{firstPoint.x.ToString(fmt, culture)} Y{firstPoint.y.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
                }
            }
        }

        // Временно удалено: GenerateWallsFinishing - будет реализовано заново

        /// <summary>
        /// Клонирует операцию кармана.
        /// </summary>
        private T CloneOperation<T>(T source) where T : IPocketOperation
        {
            if (source == null)
                return default(T);

            // Клонируем в зависимости от конкретного типа
            if (source is PocketCircleOperation circleOp)
            {
                return (T)(IPocketOperation)new PocketCircleOperation
                {
                    Name = circleOp.Name,
                    IsEnabled = circleOp.IsEnabled,
                    PocketStrategy = circleOp.PocketStrategy,
                    Direction = circleOp.Direction,
                    CenterX = circleOp.CenterX,
                    CenterY = circleOp.CenterY,
                    Radius = circleOp.Radius,
                    TotalDepth = circleOp.TotalDepth,
                    StepDepth = circleOp.StepDepth,
                    ToolDiameter = circleOp.ToolDiameter,
                    ContourHeight = circleOp.ContourHeight,
                    FeedXYRapid = circleOp.FeedXYRapid,
                    FeedXYWork = circleOp.FeedXYWork,
                    FeedZRapid = circleOp.FeedZRapid,
                    FeedZWork = circleOp.FeedZWork,
                    SafeZHeight = circleOp.SafeZHeight,
                    RetractHeight = circleOp.RetractHeight,
                    StepPercentOfTool = circleOp.StepPercentOfTool,
                    Decimals = circleOp.Decimals,
                    LineAngleDeg = circleOp.LineAngleDeg,
                    WallTaperAngleDeg = circleOp.WallTaperAngleDeg,
                    IsRoughingEnabled = circleOp.IsRoughingEnabled,
                    IsFinishingEnabled = circleOp.IsFinishingEnabled,
                    FinishAllowance = circleOp.FinishAllowance,
                    FinishingMode = circleOp.FinishingMode,
                    Metadata = circleOp.Metadata != null ? new Dictionary<string, object>(circleOp.Metadata) : new Dictionary<string, object>()
                };
            }

            if (source is PocketRectangleOperation rectOp)
            {
                return (T)(IPocketOperation)new PocketRectangleOperation
                {
                    Name = rectOp.Name,
                    IsEnabled = rectOp.IsEnabled,
                    PocketStrategy = rectOp.PocketStrategy,
                    Direction = rectOp.Direction,
                    Width = rectOp.Width,
                    Height = rectOp.Height,
                    RotationAngle = rectOp.RotationAngle,
                    TotalDepth = rectOp.TotalDepth,
                    StepDepth = rectOp.StepDepth,
                    ToolDiameter = rectOp.ToolDiameter,
                    ContourHeight = rectOp.ContourHeight,
                    FeedXYRapid = rectOp.FeedXYRapid,
                    FeedXYWork = rectOp.FeedXYWork,
                    FeedZRapid = rectOp.FeedZRapid,
                    FeedZWork = rectOp.FeedZWork,
                    SafeZHeight = rectOp.SafeZHeight,
                    RetractHeight = rectOp.RetractHeight,
                    ReferencePointX = rectOp.ReferencePointX,
                    ReferencePointY = rectOp.ReferencePointY,
                    ReferencePointType = rectOp.ReferencePointType,
                    StepPercentOfTool = rectOp.StepPercentOfTool,
                    Decimals = rectOp.Decimals,
                    LineAngleDeg = rectOp.LineAngleDeg,
                    WallTaperAngleDeg = rectOp.WallTaperAngleDeg,
                    IsRoughingEnabled = rectOp.IsRoughingEnabled,
                    IsFinishingEnabled = rectOp.IsFinishingEnabled,
                    FinishAllowance = rectOp.FinishAllowance,
                    FinishingMode = rectOp.FinishingMode,
                    Metadata = rectOp.Metadata != null ? new Dictionary<string, object>(rectOp.Metadata) : new Dictionary<string, object>()
                };
            }

            if (source is PocketEllipseOperation ellipseOp)
            {
                return (T)(IPocketOperation)new PocketEllipseOperation
                {
                    Name = ellipseOp.Name,
                    IsEnabled = ellipseOp.IsEnabled,
                    PocketStrategy = ellipseOp.PocketStrategy,
                    Direction = ellipseOp.Direction,
                    CenterX = ellipseOp.CenterX,
                    CenterY = ellipseOp.CenterY,
                    RadiusX = ellipseOp.RadiusX,
                    RadiusY = ellipseOp.RadiusY,
                    RotationAngle = ellipseOp.RotationAngle,
                    TotalDepth = ellipseOp.TotalDepth,
                    StepDepth = ellipseOp.StepDepth,
                    ToolDiameter = ellipseOp.ToolDiameter,
                    ContourHeight = ellipseOp.ContourHeight,
                    FeedXYRapid = ellipseOp.FeedXYRapid,
                    FeedXYWork = ellipseOp.FeedXYWork,
                    FeedZRapid = ellipseOp.FeedZRapid,
                    FeedZWork = ellipseOp.FeedZWork,
                    SafeZHeight = ellipseOp.SafeZHeight,
                    RetractHeight = ellipseOp.RetractHeight,
                    StepPercentOfTool = ellipseOp.StepPercentOfTool,
                    Decimals = ellipseOp.Decimals,
                    LineAngleDeg = ellipseOp.LineAngleDeg,
                    WallTaperAngleDeg = ellipseOp.WallTaperAngleDeg,
                    IsRoughingEnabled = ellipseOp.IsRoughingEnabled,
                    IsFinishingEnabled = ellipseOp.IsFinishingEnabled,
                    FinishAllowance = ellipseOp.FinishAllowance,
                    FinishingMode = ellipseOp.FinishingMode,
                    Metadata = ellipseOp.Metadata != null ? new Dictionary<string, object>(ellipseOp.Metadata) : new Dictionary<string, object>()
                };
            }

            if (source is PocketDxfOperation dxfOp)
            {
                var cloned = new PocketDxfOperation
                {
                    Name = dxfOp.Name,
                    IsEnabled = dxfOp.IsEnabled,
                    PocketStrategy = dxfOp.PocketStrategy,
                    Direction = dxfOp.Direction,
                    TotalDepth = dxfOp.TotalDepth,
                    StepDepth = dxfOp.StepDepth,
                    ToolDiameter = dxfOp.ToolDiameter,
                    ContourHeight = dxfOp.ContourHeight,
                    FeedXYRapid = dxfOp.FeedXYRapid,
                    FeedXYWork = dxfOp.FeedXYWork,
                    FeedZRapid = dxfOp.FeedZRapid,
                    FeedZWork = dxfOp.FeedZWork,
                    SafeZHeight = dxfOp.SafeZHeight,
                    RetractHeight = dxfOp.RetractHeight,
                    StepPercentOfTool = dxfOp.StepPercentOfTool,
                    Decimals = dxfOp.Decimals,
                    LineAngleDeg = dxfOp.LineAngleDeg,
                    WallTaperAngleDeg = dxfOp.WallTaperAngleDeg,
                    IsRoughingEnabled = dxfOp.IsRoughingEnabled,
                    IsFinishingEnabled = dxfOp.IsFinishingEnabled,
                    FinishAllowance = dxfOp.FinishAllowance,
                    FinishingMode = dxfOp.FinishingMode,
                    DxfFilePath = dxfOp.DxfFilePath,
                    Metadata = dxfOp.Metadata != null ? new Dictionary<string, object>(dxfOp.Metadata) : new Dictionary<string, object>()
                };

                // Клонируем контуры
                if (dxfOp.ClosedContours != null)
                {
                    cloned.ClosedContours = new List<DxfPolyline>();
                    foreach (var contour in dxfOp.ClosedContours)
                    {
                        if (contour?.Points != null)
                        {
                            var clonedContour = new DxfPolyline
                            {
                                Points = new List<DxfPoint>()
                            };
                            foreach (var point in contour.Points)
                            {
                                clonedContour.Points.Add(new DxfPoint { X = point.X, Y = point.Y });
                            }
                            cloned.ClosedContours.Add(clonedContour);
                        }
                    }
                }

                return (T)(IPocketOperation)cloned;
            }

            throw new NotSupportedException($"Unsupported pocket operation type: {source.GetType().Name}");
        }

        // Временно удалено: ApplyRoughingAllowance - будет реализовано заново

        /// <summary>
        /// Проверяет, не стал ли карман слишком маленьким.
        /// </summary>
        private bool IsOperationTooSmall<T>(T op) where T : IPocketOperation
        {
            if (op == null)
                return true;

            double toolRadius = op.ToolDiameter / 2.0;
            double minSize = toolRadius * 2.1; // Минимальный размер должен быть больше диаметра инструмента

            if (op is PocketCircleOperation circleOp)
            {
                return circleOp.Radius <= minSize;
            }
            else if (op is PocketRectangleOperation rectOp)
            {
                return rectOp.Width <= minSize || rectOp.Height <= minSize;
            }
            else if (op is PocketEllipseOperation ellipseOp)
            {
                return ellipseOp.RadiusX <= minSize || ellipseOp.RadiusY <= minSize;
            }
            else if (op is PocketDxfOperation dxfOp)
            {
                // Для DXF проверяем, что есть хотя бы один контур
                return dxfOp.ClosedContours == null || dxfOp.ClosedContours.Count == 0;
            }

            return false;
        }

        // Временно удалено: ApplyBottomFinishingAllowance - будет реализовано заново
    }
}

