#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using GCodeGenerator.Geometry;
using GCodeGenerator.Models;

namespace GCodeGenerator.Trajectory
{
    /// <summary>
    /// Собирает сцену разбором готовой программы.
    ///
    /// Собственную программу так разбирать больше не нужно: предпросмотр
    /// строится прямо из траектории (<see cref="ToolPathSceneBuilder"/>),
    /// из которой она и сделана. Этот разбор остаётся ради чужих файлов —
    /// когда программа научится открывать G-код, написанный не ею, — и
    /// служит проверкой: сцены, полученные обоими путями, обязаны совпадать.
    ///
    /// Builds a <see cref="TrajectoryScene"/> from a structured
    /// <see cref="GCodeProgram"/> (plan items 6.1/6.2). Replaces the
    /// hand-written text parser that used to live in <c>PreviewViewModel</c>:
    /// the same modal semantics (G0/G1/G2/G3 persist until changed,
    /// G17/G18/G19 arc planes, I/J/K center-offset and R radius arcs,
    /// M30/M2 program end) but works on blocks instead of rendered text,
    /// so the preview consumes structure, not a re-parsed string.
    ///
    /// Deliberate difference from the old text parser: a G92 start
    /// position updates the tracked position WITHOUT creating a segment
    /// (the old parser drew a phantom move from the origin to the G92
    /// position).
    /// </summary>
    public static class SceneBuilder
    {
        /// <summary>
        /// Допуск сравнения номера команды G/M: номера приходят как double
        /// (G1, M30), поэтому сравниваются не на равенство. К геометрии
        /// отношения не имеет.
        /// </summary>
        private const double CodeMatchTolerance = 0.001;

        /// <summary>Position change below this (mm) is not a move.</summary>
        private const double PositionTolerance = GeometryTolerances.Position;

        /// <summary>
        /// Builds the trajectory scene for the given program. A null program
        /// yields an empty scene.
        /// </summary>
        public static TrajectoryScene Build(GCodeProgram program)
        {
            var segments = new List<TrajectorySegment>();
            if (program == null)
                return new TrajectoryScene(segments);

            var currentPos = Vec3.Zero;
            var currentMoveType = MoveType.Rapid; // modal, default rapid
            var plane = ArcPlane.XY;              // G17 default for arcs

            foreach (var block in program.Blocks)
            {
                if (block.Words.Count == 0)
                    continue; // comment-only line

                // Program end (M30 / M2) — the old parser skipped such lines.
                if (IsProgramEnd(block))
                    continue;

                // G92 (set start position): updates the tracked position,
                // no move segment (see class docs).
                if (ContainsCode(block.Words, 'G', 92))
                {
                    if (TryGetAxis(block.Words, 'X', out var gx)) currentPos = new Vec3(gx, currentPos.Y, currentPos.Z);
                    if (TryGetAxis(block.Words, 'Y', out var gy)) currentPos = new Vec3(currentPos.X, gy, currentPos.Z);
                    if (TryGetAxis(block.Words, 'Z', out var gz)) currentPos = new Vec3(currentPos.X, currentPos.Y, gz);
                    continue;
                }

                // Arc plane selection (G17/G18/G19) and motion command
                // (G0/G1/G2/G3): the last occurrence in the line wins,
                // matching the old parser's scan.
                foreach (var word in block.Words)
                {
                    if (!TryGetCode(word, out var letter, out var number) || letter != 'G')
                        continue;

                    if (number == 17) plane = ArcPlane.XY;
                    else if (number == 18) plane = ArcPlane.XZ;
                    else if (number == 19) plane = ArcPlane.YZ;
                    else if (number == 0) currentMoveType = MoveType.Rapid;
                    else if (number == 1) currentMoveType = MoveType.Linear;
                    else if (number == 2) currentMoveType = MoveType.ArcCW;
                    else if (number == 3) currentMoveType = MoveType.ArcCCW;
                }

                // Target coordinates: first occurrence of each axis wins.
                double x = currentPos.X, y = currentPos.Y, z = currentPos.Z;
                if (TryGetAxis(block.Words, 'X', out var xv)) x = xv;
                if (TryGetAxis(block.Words, 'Y', out var yv)) y = yv;
                if (TryGetAxis(block.Words, 'Z', out var zv)) z = zv;

                // Arc parameters (I, J, K center offsets; R radius).
                var hasI = TryGetAxis(block.Words, 'I', out var i);
                var hasJ = TryGetAxis(block.Words, 'J', out var j);
                var hasK = TryGetAxis(block.Words, 'K', out var k);
                var hasR = TryGetAxis(block.Words, 'R', out var r);

                var newPos = new Vec3(x, y, z);

                // Check if position changed.
                if (Math.Abs(newPos.X - currentPos.X) <= PositionTolerance &&
                    Math.Abs(newPos.Y - currentPos.Y) <= PositionTolerance &&
                    Math.Abs(newPos.Z - currentPos.Z) <= PositionTolerance)
                    continue;

                var segment = new TrajectorySegment
                {
                    Start = currentPos,
                    End = newPos,
                    MoveType = currentMoveType
                };

                // Handle arcs.
                if ((currentMoveType == MoveType.ArcCW || currentMoveType == MoveType.ArcCCW) &&
                    (hasI || hasJ || hasK || hasR))
                {
                    if (hasR)
                    {
                        // Radius format — calculate center.
                        segment.ArcRadius = Math.Abs(r);
                        segment.InterpolatedPoints = InterpolateArcByRadius(
                            currentPos, newPos, r, currentMoveType == MoveType.ArcCW, plane);
                    }
                    else
                    {
                        // Center offset format (I, J, K).
                        var center = new Vec3(
                            currentPos.X + (hasI ? i : 0),
                            currentPos.Y + (hasJ ? j : 0),
                            currentPos.Z + (hasK ? k : 0));
                        segment.ArcCenter = center;
                        segment.ArcRadius = Distance(currentPos, center);
                        segment.InterpolatedPoints = InterpolateArcByCenter(
                            currentPos, newPos, center, currentMoveType == MoveType.ArcCW, plane);
                    }
                }

                segments.Add(segment);
                currentPos = newPos;
            }

            return new TrajectoryScene(segments);
        }

        // ------------------------------------------------------------------
        // Word access
        // ------------------------------------------------------------------

        /// <summary>
        /// Extracts a G/M code from a word, including raw words
        /// ("G92", "M30" are rendered verbatim by the formatter).
        /// Returns false for axis words and non-code raw text
        /// (e.g. the "(Generated by GCodeGenerator)" header line).
        /// </summary>
        private static bool TryGetCode(GCodeWord word, out char letter, out double number)
        {
            letter = '\0';
            number = 0;

            if (word.Text != null)
            {
                if (word.Text.Length >= 2 && (word.Text[0] == 'G' || word.Text[0] == 'M'))
                {
                    if (double.TryParse(word.Text.Substring(1), NumberStyles.Float,
                            CultureInfo.InvariantCulture, out number))
                    {
                        letter = word.Text[0];
                        return true;
                    }
                }
                return false;
            }

            if (word.Letter == 'G' || word.Letter == 'M')
            {
                letter = word.Letter;
                number = word.Number;
                return true;
            }

            return false;
        }

        private static bool ContainsCode(IReadOnlyList<GCodeWord> words, char letter, double code)
        {
            foreach (var word in words)
                if (TryGetCode(word, out var l, out var n) && l == letter && Math.Abs(n - code) < CodeMatchTolerance)
                    return true;
            return false;
        }

        /// <summary>First occurrence of the axis word wins; false if absent.</summary>
        private static bool TryGetAxis(IReadOnlyList<GCodeWord> words, char axis, out double value)
        {
            value = 0;
            foreach (var word in words)
            {
                if (word.Text == null && char.ToUpperInvariant(word.Letter) == axis)
                {
                    value = word.Number;
                    return true;
                }
            }
            return false;
        }

        private static bool IsProgramEnd(GCodeBlock block)
        {
            var first = block.Words[0];
            return TryGetCode(first, out var letter, out var number) &&
                   letter == 'M' && (Math.Abs(number - 30) < CodeMatchTolerance || Math.Abs(number - 2) < CodeMatchTolerance);
        }

        // ------------------------------------------------------------------
        // Arc interpolation (moved from PreviewViewModel, Point3D → Vec3)
        // ------------------------------------------------------------------

        private enum ArcPlane
        {
            XY, // G17
            XZ, // G18
            YZ  // G19
        }

        /// <summary>
        /// Projects a point onto the arc plane axes: (A, B) are the two
        /// in-plane axes, C is the third axis (linear interpolation).
        /// </summary>
        private static (double A, double B, double C) PlaneAxes(Vec3 p, ArcPlane plane)
        {
            switch (plane)
            {
                case ArcPlane.XZ: return (p.X, p.Z, p.Y);
                case ArcPlane.YZ: return (p.Y, p.Z, p.X);
                default: return (p.X, p.Y, p.Z);
            }
        }

        private static Vec3 FromPlaneAxes(double a, double b, double c, ArcPlane plane)
        {
            switch (plane)
            {
                case ArcPlane.XZ: return new Vec3(a, c, b);
                case ArcPlane.YZ: return new Vec3(c, a, b);
                default: return new Vec3(a, b, c);
            }
        }

        private static List<Vec3> InterpolateArcByCenter(Vec3 start, Vec3 end, Vec3 center,
            bool clockwise, ArcPlane plane)
        {
            var points = new List<Vec3>();

            var (startA, startB, startC) = PlaneAxes(start, plane);
            var (endA, endB, endC) = PlaneAxes(end, plane);
            var (centerA, centerB, _) = PlaneAxes(center, plane);

            // Формула разбиения общая для всех предпросмотров
            // (ArcInterpolation); здесь остаётся только работа с плоскостью
            // дуги — разбор чужих программ обязан понимать G18/G19.
            foreach (var (a, b, t) in ArcInterpolation.Points(
                         startA, startB, endA, endB, centerA, centerB, clockwise, includeStart: true))
            {
                var c = startC + t * (endC - startC); // Linear interpolation for third axis
                points.Add(FromPlaneAxes(a, b, c, plane));
            }

            return points;
        }

        private static List<Vec3> InterpolateArcByRadius(Vec3 start, Vec3 end, double radius,
            bool clockwise, ArcPlane plane)
        {
            // Calculate center from radius. The sign of R selects which of
            // the two possible centers to use.
            var (startA, startB, _) = PlaneAxes(start, plane);
            var (endA, endB, _) = PlaneAxes(end, plane);

            // Midpoint between start and end
            var midA = (startA + endA) / 2;
            var midB = (startB + endB) / 2;

            // Distance from start to end
            var chordLength = Math.Sqrt(Math.Pow(endA - startA, 2) + Math.Pow(endB - startB, 2));

            // Check if radius is valid
            if (Math.Abs(radius) < chordLength / 2)
            {
                // Radius too small, just return linear segment
                return new List<Vec3> { start, end };
            }

            // Distance from midpoint to center
            var h = Math.Sqrt(radius * radius - chordLength * chordLength / 4);

            // Direction perpendicular to chord
            var dx = endA - startA;
            var dy = endB - startB;
            var perpX = -dy / chordLength;
            var perpY = dx / chordLength;

            // Choose center based on direction and sign of radius
            var sign = (clockwise ^ (radius < 0)) ? -1 : 1;
            var centerA = midA + sign * h * perpX;
            var centerB = midB + sign * h * perpY;

            Vec3 center;
            switch (plane)
            {
                case ArcPlane.XZ: center = new Vec3(centerA, start.Y, centerB); break;
                case ArcPlane.YZ: center = new Vec3(start.X, centerA, centerB); break;
                default: center = new Vec3(centerA, centerB, start.Z); break;
            }

            return InterpolateArcByCenter(start, end, center, clockwise, plane);
        }

        private static double Distance(Vec3 a, Vec3 b) =>
            Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2) + Math.Pow(a.Z - b.Z, 2));
    }
}
