#nullable enable
using System;
using System.Collections.Generic;
using GCodeGenerator.Models;
using GCodeGenerator.Trajectory;

namespace GCodeGenerator.Preview
{
    /// <summary>
    /// Вид сверху на фактически записанную программу. В отличие от проекции
    /// промежуточного ToolPath учитывает G92, округление слов и служебную
    /// парковку эпилога. Метаданные кадров сохраняют выбор операций; пролог
    /// и эпилог рисуются без кликабельного владельца.
    /// </summary>
    public static class ProgramSceneProjection
    {
        public static OperationScene Build(GCodeProgram program)
        {
            if (program == null)
                return OperationScene.Empty;

            var shapes = new List<OperationShape>();
            var current = new List<(double X, double Y)>();
            OperationBase? currentSource = null;
            OperationShapeKind? currentKind = null;

            void Flush()
            {
                if (current.Count >= 2 && currentKind.HasValue)
                {
                    shapes.Add(new OperationShape(currentSource, currentKind.Value,
                        current.ToArray(), isClosed: false, isFilled: false));
                }

                current = new List<(double X, double Y)>();
                currentKind = null;
                currentSource = null;
            }

            foreach (var segment in SceneBuilder.Build(program).Segments)
            {
                var points = Project(segment);
                if (points.Count < 2)
                    continue;

                var source = segment.Source as OperationBase;
                var kind = segment.MoveType == MoveType.Rapid
                    ? OperationShapeKind.RapidMove
                    : OperationShapeKind.CuttingMove;
                var canAppend = currentKind == kind
                                && ReferenceEquals(currentSource, source)
                                && current.Count > 0
                                && Same(current[current.Count - 1], points[0]);
                if (!canAppend)
                {
                    Flush();
                    currentSource = source;
                    currentKind = kind;
                    current.Add(points[0]);
                }

                for (var i = 1; i < points.Count; i++)
                    current.Add(points[i]);
            }

            Flush();
            return new OperationScene(shapes);
        }

        private static List<(double X, double Y)> Project(TrajectorySegment segment)
        {
            var points = new List<(double X, double Y)>();
            if (segment.InterpolatedPoints is { Count: > 0 } arc)
            {
                foreach (var point in arc)
                    points.Add((point.X, point.Y));
            }
            else if (Math.Abs(segment.Start.X - segment.End.X) > 1e-9
                     || Math.Abs(segment.Start.Y - segment.End.Y) > 1e-9)
            {
                points.Add((segment.Start.X, segment.Start.Y));
                points.Add((segment.End.X, segment.End.Y));
            }

            return points;
        }

        private static bool Same((double X, double Y) first, (double X, double Y) second)
            => Math.Abs(first.X - second.X) <= 1e-9
               && Math.Abs(first.Y - second.Y) <= 1e-9;
    }
}
