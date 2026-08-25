using System;
using System.Collections.Generic;
using GCodeGenerator.Models;
using GCodeGenerator.Toolpath;

namespace GCodeGenerator.Preview
{
    /// <summary>
    /// Двумерная сцена по траектории инструмента: вид сверху на то, что
    /// действительно проделает станок.
    ///
    /// Прежний вид предпросмотра — контуры, построенные заново из моделей
    /// операций, — показывает замысел: где лежит окружность, каких размеров
    /// прямоугольник. Он не знает ни о компенсации радиуса инструмента,
    /// ни о выбранной стратегии выборки, ни о числе проходов, поэтому карман
    /// выглядит пустым овалом, а контур — линией по чертежу, а не по центру
    /// фрезы. Этот вид показывает саму траекторию: рабочие ходы отдельно
    /// от холостых.
    /// </summary>
    public static class ToolPathSceneProjection
    {
        /// <summary>
        /// Строит сцену из траектории. Пустая траектория даёт пустую сцену.
        /// </summary>
        public static OperationScene Build(ToolPath toolPath)
        {
            var shapes = new List<OperationShape>();
            if (toolPath == null)
                return new OperationScene(shapes);

            var position = (x: 0.0, y: 0.0, z: 0.0);

            foreach (var operation in toolPath.Operations)
            {
                var source = operation.Source as OperationBase;
                var current = new List<(double X, double Y)>();
                var currentIsRapid = false;

                foreach (var item in operation.Items)
                {
                    if (!(item is ToolMove move))
                        continue;

                    var target = (
                        x: move.X ?? position.x,
                        y: move.Y ?? position.y,
                        z: move.Z ?? position.z);

                    var isRapid = move.Kind == ToolMoveKind.Rapid;
                    var movesInPlane = Math.Abs(target.x - position.x) > 1e-9
                                       || Math.Abs(target.y - position.y) > 1e-9;

                    // Ход вглубь без движения в плане рисовать нечем: сверху
                    // он выглядит точкой.
                    if (!movesInPlane)
                    {
                        position = target;
                        continue;
                    }

                    if (current.Count > 0 && isRapid != currentIsRapid)
                    {
                        Flush(shapes, source, current, currentIsRapid);
                        current = new List<(double X, double Y)> { (position.x, position.y) };
                    }
                    else if (current.Count == 0)
                    {
                        current.Add((position.x, position.y));
                    }

                    currentIsRapid = isRapid;
                    AppendMove(current, position, target, move);
                    position = target;
                }

                Flush(shapes, source, current, currentIsRapid);
            }

            return new OperationScene(shapes);
        }

        /// <summary>Добавляет к ломаной конец перемещения; дуга раскладывается на точки.</summary>
        private static void AppendMove(
            List<(double X, double Y)> points,
            (double x, double y, double z) start,
            (double x, double y, double z) end,
            ToolMove move)
        {
            if (!move.IsArc || move.CenterOffsetX == null || move.CenterOffsetY == null)
            {
                points.Add((end.x, end.y));
                return;
            }

            var centerX = start.x + move.CenterOffsetX.Value;
            var centerY = start.y + move.CenterOffsetY.Value;
            var radius = Math.Sqrt(Math.Pow(start.x - centerX, 2) + Math.Pow(start.y - centerY, 2));

            var startAngle = Math.Atan2(start.y - centerY, start.x - centerX);
            var endAngle = Math.Atan2(end.y - centerY, end.x - centerX);

            if (move.Kind == ToolMoveKind.ArcClockwise)
            {
                if (endAngle >= startAngle) endAngle -= 2 * Math.PI;
            }
            else
            {
                if (endAngle <= startAngle) endAngle += 2 * Math.PI;
            }

            var segments = Math.Max((int)(Math.Abs(endAngle - startAngle) / (Math.PI / 16)), 4);
            for (int i = 1; i <= segments; i++)
            {
                var angle = startAngle + (endAngle - startAngle) * i / segments;
                points.Add((centerX + radius * Math.Cos(angle), centerY + radius * Math.Sin(angle)));
            }
        }

        private static void Flush(
            List<OperationShape> shapes,
            OperationBase source,
            List<(double X, double Y)> points,
            bool isRapid)
        {
            if (source == null || points.Count < 2)
                return;

            shapes.Add(new OperationShape(
                source,
                isRapid ? OperationShapeKind.RapidMove : OperationShapeKind.CuttingMove,
                points.ToArray(),
                isClosed: false,
                isFilled: false));
        }
    }
}
