using System;
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

            // Обрабатываем черновую и чистовую обработку
            _helper.ProcessRoughingFinishing(
                pocketOp,
                roughOp => GenerateInternal(roughOp, geometry, addLine, g0, g1, settings),
                (finishOp, allowance) => GenerateWallsFinishing(finishOp, geometry, allowance, addLine, g0, g1, settings),
                CloneOperation,
                ApplyRoughingAllowance,
                IsOperationTooSmall,
                ApplyBottomFinishingAllowance,
                addLine,
                g0,
                g1,
                settings);
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

            // Генерируем траекторию в зависимости от стратегии
            switch (op.PocketStrategy)
            {
                case PocketStrategy.Concentric:
                    GenerateConcentricStrategy(op, geometry, toolRadius, taperOffset, step, addLine, g0, g1, fmt, culture, settings);
                    break;
                case PocketStrategy.Spiral:
                    // Spiral требует специфической логики для каждого типа кармана
                    // Пока используем Concentric как fallback
                    GenerateConcentricStrategy(op, geometry, toolRadius, taperOffset, step, addLine, g0, g1, fmt, culture, settings);
                    break;
                case PocketStrategy.Radial:
                case PocketStrategy.Lines:
                case PocketStrategy.ZigZag:
                    // Эти стратегии требуют специфической логики
                    // Пока используем Concentric как fallback
                    GenerateConcentricStrategy(op, geometry, toolRadius, taperOffset, step, addLine, g0, g1, fmt, culture, settings);
                    break;
                default:
                    GenerateConcentricStrategy(op, geometry, toolRadius, taperOffset, step, addLine, g0, g1, fmt, culture, settings);
                    break;
            }

            // Возврат в центр и подъем
            addLine($"{g1} X{center.x.ToString(fmt, culture)} Y{center.y.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
            addLine($"{g0} Z{op.SafeZHeight.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
        }

        /// <summary>
        /// Генерирует концентрическую стратегию обработки.
        /// </summary>
        private void GenerateConcentricStrategy(
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

            // Простая реализация: генерируем концентрические проходы
            // Начинаем с центра и движемся к краю
            double maxDistance = 0.0;
            foreach (var point in contourPoints)
            {
                double dx = point.x - center.x;
                double dy = point.y - center.y;
                double distance = Math.Sqrt(dx * dx + dy * dy);
                if (distance > maxDistance)
                    maxDistance = distance;
            }

            // Генерируем концентрические окружности
            for (double r = step; r <= maxDistance; r += step)
            {
                // Проверяем, что точка находится внутри контура
                double testX = center.x + r;
                double testY = center.y;
                if (!geometry.IsPointInside(testX, testY, toolRadius, taperOffset))
                    continue;

                // Генерируем окружность
                int segments = Math.Max(32, (int)Math.Ceiling(2 * Math.PI * r / (op.ToolDiameter * 0.5)));
                if (segments < 4) segments = 4;

                double angleStep = 2 * Math.PI / segments *
                                  ((op.Direction == MillingDirection.Clockwise) ? -1 : 1);

                double startX = center.x + r;
                double startY = center.y;
                addLine($"{g1} X{startX.ToString(fmt, culture)} Y{startY.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");

                for (int i = 1; i <= segments; i++)
                {
                    double ang = angleStep * i;
                    double x = center.x + r * Math.Cos(ang);
                    double y = center.y + r * Math.Sin(ang);
                    addLine($"{g1} X{x.ToString(fmt, culture)} Y{y.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
                }
            }

            // Обрабатываем внешний контур
            foreach (var point in contourPoints)
            {
                addLine($"{g1} X{point.x.ToString(fmt, culture)} Y{point.y.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
            }
        }

        /// <summary>
        /// Генерирует чистовую обработку стенок.
        /// </summary>
        private void GenerateWallsFinishing(
            IPocketOperation op,
            IPocketGeometry geometry,
            double radialAllowance,
            Action<string> addLine,
            string g0,
            string g1,
            GCodeSettings settings)
        {
            var fmt = $"0.{new string('0', op.Decimals)}";
            var culture = CultureInfo.InvariantCulture;

            double toolRadius = op.ToolDiameter / 2.0;
            double stepRadial = op.StepDepth;
            if (stepRadial <= 0)
                stepRadial = op.ToolDiameter * 0.25;

            double startZ = op.ContourHeight;
            double finalZ = op.ContourHeight - op.TotalDepth;

            double allowance = Math.Max(0.0, radialAllowance);
            int radialPasses = allowance > 1e-6
                ? Math.Max(1, (int)Math.Ceiling(allowance / stepRadial))
                : 1;
            double radialStep = (radialPasses > 0 && allowance > 1e-6) ? allowance / radialPasses : 0.0;

            double depthFromTop = op.ContourHeight - finalZ;
            double taperOffset = GCodeGenerationHelper.CalculateTaperOffset(depthFromTop, op.WallTaperAngleDeg);

            var contour = geometry.GetContour(toolRadius, taperOffset);
            if (contour == null)
                return;

            var contourPoints = contour.GetPoints().ToList();
            if (contourPoints.Count == 0)
                return;

            var startPoint = contourPoints[0];

            for (int i = 0; i < radialPasses; i++)
            {
                double remaining = allowance - (i + 1) * radialStep;
                if (remaining < 0) remaining = 0;

                if (settings.UseComments)
                    addLine($"(Finishing walls radial pass {i + 1}/{radialPasses}, stock {remaining.ToString(fmt, culture)}mm)");

                addLine($"{g0} Z{op.SafeZHeight.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
                addLine($"{g0} X{startPoint.x.ToString(fmt, culture)} Y{startPoint.y.ToString(fmt, culture)} F{op.FeedXYRapid.ToString(fmt, culture)}");
                addLine($"{g0} Z{startZ.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
                addLine($"{g1} Z{finalZ.ToString(fmt, culture)} F{op.FeedZWork.ToString(fmt, culture)}");

                // Обход стенки по контуру
                foreach (var point in contourPoints.Skip(1))
                {
                    addLine($"{g1} X{point.x.ToString(fmt, culture)} Y{point.y.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
                }
                addLine($"{g1} X{startPoint.x.ToString(fmt, culture)} Y{startPoint.y.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");

                var center = geometry.GetCenter();
                addLine($"{g1} X{center.x.ToString(fmt, culture)} Y{center.y.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
                addLine($"{g0} Z{op.SafeZHeight.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
            }
        }

        /// <summary>
        /// Клонирует операцию кармана.
        /// </summary>
        private T CloneOperation<T>(T source) where T : IPocketOperation
        {
            // Это упрощенная версия - в реальности нужно клонировать конкретный тип
            // Для полной реализации нужно использовать геометрию для получения параметров
            return source; // Временная заглушка
        }

        /// <summary>
        /// Применяет припуск для черновой обработки.
        /// </summary>
        private void ApplyRoughingAllowance<T>(T op, double depthAllowance) where T : IPocketOperation
        {
            // Применение припуска должно выполняться через геометрию
            // Это упрощенная версия
        }

        /// <summary>
        /// Проверяет, не стал ли карман слишком маленьким.
        /// </summary>
        private bool IsOperationTooSmall<T>(T op) where T : IPocketOperation
        {
            // Проверка должна выполняться через геометрию
            return false; // Временная заглушка
        }

        /// <summary>
        /// Применяет припуск для чистовой обработки дна.
        /// </summary>
        private void ApplyBottomFinishingAllowance<T>(T op, double allowance) where T : IPocketOperation
        {
            // Применение припуска должно выполняться через геометрию
            // Это упрощенная версия
        }
    }
}

