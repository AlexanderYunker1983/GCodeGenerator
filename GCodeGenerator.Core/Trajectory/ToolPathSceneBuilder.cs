using System;
using System.Collections.Generic;
using GCodeGenerator.Geometry;
using GCodeGenerator.Toolpath;

namespace GCodeGenerator.Trajectory
{
    /// <summary>
    /// Строит сцену предпросмотра прямо из траектории инструмента.
    ///
    /// Раньше сцена собиралась разбором уже готовой программы: предпросмотр
    /// читал G-слова, восстанавливал по ним модальные состояния — какой
    /// командой идёт движение, в какой плоскости лежит дуга, где ноль детали —
    /// и только потом получал то, что генератор и так знал. Программа
    /// интерпретировала собственный вывод.
    ///
    /// Теперь показывается ровно та траектория, из которой сделана программа:
    /// разойтись им негде. Разбор G-кода остался только в
    /// <see cref="SceneBuilder"/> — он понадобится, когда программа научится
    /// открывать чужие файлы.
    /// </summary>
    public static class ToolPathSceneBuilder
    {
        /// <summary>Перемещение короче этого не считается движением, мм.</summary>
        private const double PositionTolerance = GeometryTolerances.Position;

        /// <summary>
        /// Собирает сцену. Пустая траектория даёт пустую сцену.
        /// </summary>
        public static TrajectoryScene Build(ToolPath toolPath)
        {
            var segments = new List<TrajectorySegment>();
            if (toolPath == null)
                return new TrajectoryScene(segments);

            var position = Vec3.Zero;

            foreach (var move in toolPath.Moves())
            {
                var target = Apply(position, move);

                if (move.IsArc)
                {
                    var segment = BuildArc(position, target, move);
                    if (segment != null)
                    {
                        segments.Add(segment);
                        position = target;
                    }
                    continue;
                }

                if (Distance(position, target) <= PositionTolerance)
                    continue;

                segments.Add(new TrajectorySegment
                {
                    Start = position,
                    End = target,
                    MoveType = move.Kind == ToolMoveKind.Rapid ? MoveType.Rapid : MoveType.Linear
                });
                position = target;
            }

            return new TrajectoryScene(segments);
        }

        /// <summary>Куда придёт инструмент: незаданная ось не меняется.</summary>
        private static Vec3 Apply(Vec3 position, ToolMove move)
            => new Vec3(
                move.X ?? position.X,
                move.Y ?? position.Y,
                move.Z ?? position.Z);

        /// <summary>
        /// Дуга задаётся смещением центра от начальной точки — так же, как
        /// в программе словами I и J.
        /// </summary>
        private static TrajectorySegment BuildArc(Vec3 start, Vec3 end, ToolMove move)
        {
            if (move.CenterOffsetX == null || move.CenterOffsetY == null)
                return null;

            var center = new Vec3(
                start.X + move.CenterOffsetX.Value,
                start.Y + move.CenterOffsetY.Value,
                start.Z);

            var clockwise = move.Kind == ToolMoveKind.ArcClockwise;
            var points = InterpolateArc(start, end, center, clockwise);
            var radius = Math.Sqrt(
                Math.Pow(start.X - center.X, 2) + Math.Pow(start.Y - center.Y, 2));

            return new TrajectorySegment
            {
                Start = start,
                End = end,
                MoveType = clockwise ? MoveType.ArcCW : MoveType.ArcCCW,
                ArcCenter = center,
                ArcRadius = radius,
                InterpolatedPoints = points
            };
        }

        /// <summary>
        /// Точки дуги в плоскости XY: другие плоскости генератор не выводит,
        /// а разбор чужих программ живёт в <see cref="SceneBuilder"/>.
        /// </summary>
        private static List<Vec3> InterpolateArc(Vec3 start, Vec3 end, Vec3 center, bool clockwise)
        {
            var points = new List<Vec3>();

            var startAngle = Math.Atan2(start.Y - center.Y, start.X - center.X);
            var endAngle = Math.Atan2(end.Y - center.Y, end.X - center.X);
            var radius = Math.Sqrt(Math.Pow(start.X - center.X, 2) + Math.Pow(start.Y - center.Y, 2));

            if (clockwise)
            {
                if (endAngle >= startAngle) endAngle -= 2 * Math.PI;
            }
            else
            {
                if (endAngle <= startAngle) endAngle += 2 * Math.PI;
            }

            var totalAngle = Math.Abs(endAngle - startAngle);
            var segments = Math.Max((int)(totalAngle / (Math.PI / 16)), 4);

            for (int i = 0; i <= segments; i++)
            {
                var t = (double)i / segments;
                var angle = startAngle + t * (endAngle - startAngle);
                points.Add(new Vec3(
                    center.X + radius * Math.Cos(angle),
                    center.Y + radius * Math.Sin(angle),
                    start.Z + t * (end.Z - start.Z)));
            }

            return points;
        }

        private static double Distance(Vec3 a, Vec3 b)
            => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2) + Math.Pow(a.Z - b.Z, 2));
    }
}
