#nullable enable
using System;
using System.Collections.Generic;
using GCodeGenerator.Models;
using GCodeGenerator.Toolpath;

namespace GCodeGenerator.Preview
{
    /// <summary>
    /// Двумерная сцена промежуточной траектории инструмента. Рабочая сцена
    /// готовой программы строится <see cref="ProgramSceneProjection"/>;
    /// этот вариант нужен редактору и тестам до запуска постпроцессора.
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
            // Тип ArcMove гарантирует величины дуги: пустоту проверять не нужно.
            if (!(move is ArcMove arc))
            {
                points.Add((end.x, end.y));
                return;
            }

            var centerX = start.x + arc.ArcCenterOffsetX;
            var centerY = start.y + arc.ArcCenterOffsetY;

            // Формула разбиения общая для всех предпросмотров
            // (ArcInterpolation): начальная точка уже лежит в ломаной.
            foreach (var (a, b, _) in Trajectory.ArcInterpolation.Points(
                         start.x, start.y, end.x, end.y, centerX, centerY,
                         move.Kind == ToolMoveKind.ArcClockwise, includeStart: false))
            {
                points.Add((a, b));
            }
        }

        private static void Flush(
            List<OperationShape> shapes,
            OperationBase? source,
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
