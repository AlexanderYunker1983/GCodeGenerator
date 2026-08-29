#nullable enable
using System;
using System.Collections.Generic;
using GCodeGenerator.Geometry;
using GCodeGenerator.GCodeGenerators.Geometry;
using GCodeGenerator.Models;
using GCodeGenerator.Toolpath;

namespace GCodeGenerator.GCodeGenerators
{
    /// <summary>
    /// Подвод инструмента к одному слою кармана. Вертикальный режим сохраняет
    /// прежнюю колонну врезания, винтовой строит окружность центра фрезы с
    /// одновременным равномерным спуском по Z.
    /// </summary>
    internal static class PocketEntryGenerator
    {
        /// <summary>
        /// Наибольшая дуга одного кадра. Полуокружности однозначно понимаются
        /// стойками и не зависят от поддержки полного круга с совпавшими
        /// начальной и конечной точками.
        /// </summary>
        private const double MaxArcSweep = Math.PI;

        /// <summary>
        /// Выполняет подход и вход на рабочую глубину слоя. По завершении
        /// инструмент находится в <paramref name="center"/> на
        /// <paramref name="nextZ"/>, как ожидают все стратегии кармана.
        /// </summary>
        public static void Generate(
            PocketOperationBase op,
            IPocketGeometry geometry,
            double contourOffset,
            double taperOffset,
            IReadOnlyList<(double x, double y)> contourPoints,
            (double x, double y) center,
            double currentZ,
            double nextZ,
            bool moveToSafeZ,
            ToolPathBuilder builder,
            GCodeSettings settings)
        {
            if (moveToSafeZ)
                builder.RapidTo(z: op.SafeZHeight, feed: op.FeedZRapid);

            if (op.EntryMode == PocketEntryMode.Vertical)
            {
                builder.RapidTo(x: center.x, y: center.y, feed: op.FeedXYRapid);
                builder.RapidTo(z: currentZ, feed: op.FeedZRapid);
                builder.LinearTo(z: nextZ, feed: op.FeedZWork);
                return;
            }

            var radius = op.HelicalEntryDiameter / 2.0;
            EnsureFits(
                op, geometry, contourOffset, taperOffset,
                contourPoints, center, radius);

            // Начало окружности лежит справа от её центра и выше верха
            // текущего слоя на высоту отвода. Общая SafeZ нужна для быстрого
            // перемещения к точке, а винтовой вход начинается ниже неё —
            // иначе каждый глубокий слой повторно проходил бы по воздуху всю
            // уже выбранную глубину кармана.
            var startX = center.x + radius;
            var startY = center.y;
            var entryStartZ = currentZ + op.RetractHeight;
            builder.RapidTo(x: startX, y: startY, feed: op.FeedXYRapid);
            builder.RapidTo(z: entryStartZ, feed: op.FeedZRapid);

            var angleRad = op.EntryAngle * Math.PI / 180.0;
            var depth = entryStartZ - nextZ;
            var totalSweep = depth / (Math.Tan(angleRad) * radius);
            var feed = HelicalFeed(op, angleRad);

            if (settings.Format.AllowArcs)
                EmitArcs(op, center, startX, startY, entryStartZ, nextZ, totalSweep, feed, builder);
            else
                EmitSegments(op, center, radius, entryStartZ, nextZ, totalSweep, feed, builder);

            // Стратегии кармана начинают в центре. Радиальный рабочий ход
            // после спирали одновременно выбирает материал внутри окружности.
            builder.LinearTo(x: center.x, y: center.y, feed: op.FeedXYWork);
        }

        /// <summary>
        /// Подача вдоль пространственной кривой ограничена обеими заданными
        /// составляющими: проекция на XY не быстрее FeedXYWork, на Z —
        /// не быстрее FeedZWork.
        /// </summary>
        private static double HelicalFeed(PocketOperationBase op, double angleRad)
        {
            var byXY = op.FeedXYWork / Math.Cos(angleRad);
            var byZ = op.FeedZWork / Math.Sin(angleRad);
            return Math.Min(byXY, byZ);
        }

        private static void EmitArcs(
            PocketOperationBase op,
            (double x, double y) center,
            double startX,
            double startY,
            double currentZ,
            double nextZ,
            double totalSweep,
            double feed,
            ToolPathBuilder builder)
        {
            var clockwise = op.Direction == MillingDirection.Clockwise;
            var direction = clockwise ? -1.0 : 1.0;
            var radius = op.HelicalEntryDiameter / 2.0;
            var segments = SegmentCount(totalSweep, MaxArcSweep);
            var currentX = startX;
            var currentY = startY;

            for (int segment = 1; segment <= segments; segment++)
            {
                var progress = (double)segment / segments;
                var sweep = totalSweep * progress;
                var angle = direction * sweep;
                var x = center.x + radius * Math.Cos(angle);
                var y = center.y + radius * Math.Sin(angle);
                var z = currentZ + (nextZ - currentZ) * progress;
                var i = center.x - currentX;
                var j = center.y - currentY;

                // После округления для постпроцессора очень короткая дуга
                // может иметь одинаковые начало и конец. Кадр G2/G3 с I/J
                // и совпавшими XY некоторые стойки исполняют как полный круг,
                // хотя здесь требовалось почти вертикальное заглубление.
                // G1 сохраняет конечные XYZ и не имеет такой семантики.
                if (SameFormattedPoint(currentX, currentY, x, y, op.Decimals))
                    builder.LinearTo(x: x, y: y, z: z, feed: feed);
                else if (clockwise)
                    builder.ArcCW(x, y, i, j, feed, z);
                else
                    builder.ArcCCW(x, y, i, j, feed, z);

                currentX = x;
                currentY = y;
            }
        }

        private static bool SameFormattedPoint(
            double firstX,
            double firstY,
            double secondX,
            double secondY,
            int decimals)
        {
            return Math.Round(firstX, decimals) == Math.Round(secondX, decimals)
                && Math.Round(firstY, decimals) == Math.Round(secondY, decimals);
        }

        private static void EmitSegments(
            PocketOperationBase op,
            (double x, double y) center,
            double radius,
            double currentZ,
            double nextZ,
            double totalSweep,
            double feed,
            ToolPathBuilder builder)
        {
            var direction = op.Direction == MillingDirection.Clockwise ? -1.0 : 1.0;
            var segments = SegmentCount(
                totalSweep,
                GCodeGenerator.Trajectory.ArcInterpolation.AngleStep);

            for (int segment = 1; segment <= segments; segment++)
            {
                var progress = (double)segment / segments;
                var angle = direction * totalSweep * progress;
                builder.LinearTo(
                    x: center.x + radius * Math.Cos(angle),
                    y: center.y + radius * Math.Sin(angle),
                    z: currentZ + (nextZ - currentZ) * progress,
                    feed: feed);
            }
        }

        private static int SegmentCount(double totalSweep, double maximumSweep)
        {
            var count = Math.Ceiling(totalSweep / maximumSweep);
            if (!double.IsFinite(count) || count > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(totalSweep),
                    "Helical entry requires too many segments. Validate the entry angle and diameter first.");
            }

            return Math.Max(1, (int)count);
        }

        private static void EnsureFits(
            PocketOperationBase op,
            IPocketGeometry geometry,
            double contourOffset,
            double taperOffset,
            IReadOnlyList<(double x, double y)> contourPoints,
            (double x, double y) center,
            double radius)
        {
            var inside = geometry.IsPointInside(center.x, center.y, contourOffset, taperOffset);
            var clearance = 0.0;
            if (inside)
            {
                if (geometry is IMultiContourPocketGeometry)
                {
                    clearance = double.MaxValue;
                    foreach (var contour in PocketGeometryContours.Get(
                                 geometry, contourOffset, taperOffset))
                    {
                        clearance = Math.Min(
                            clearance,
                            PocketEntryPoint.ClearanceToContour(
                                center,
                                new List<(double x, double y)>(contour.GetPoints())));
                    }
                    if (clearance == double.MaxValue)
                        clearance = 0.0;
                }
                else
                {
                    clearance = PocketEntryPoint.ClearanceToContour(center, contourPoints);
                }
            }

            if (inside && radius <= clearance + GeometryTolerances.Containment)
                return;

            throw new CoreException(
                CoreErrorCodes.HelicalEntryDoesNotFit,
                "The helical entry diameter {0:0.###} mm does not fit in this pocket area; "
                + "the available diameter at the entry point is {1:0.###} mm.",
                op.HelicalEntryDiameter,
                Math.Max(0.0, clearance * 2.0));
        }
    }
}
