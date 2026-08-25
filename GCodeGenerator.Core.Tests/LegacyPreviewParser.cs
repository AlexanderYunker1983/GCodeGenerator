using System;
using System.Collections.Generic;
using System.Globalization;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Copy of the old hand-written G-code TEXT parser that used to live in
    /// PreviewViewModel (removed in plan item 6.1). Kept as the reference
    /// implementation for the differential test (plan item 6.4): it parses
    /// the rendered text lines, while <c>SceneBuilder</c> parses the
    /// structured blocks of the same program.
    ///
    /// Deliberate differences from the original (documented in Plan.md):
    /// 1) G92 updates the tracked position WITHOUT creating a segment (the
    ///    original drew a phantom move from the origin to the G92 position);
    /// 2) ArcRadius is filled in (the original left it 0);
    /// 3) Comment lines with an N prefix ("N40 (comment)") are skipped after
    ///    the N-strip. The original only checked for a leading "(" BEFORE the
    ///    N-strip, so it parsed coordinates out of comment text (e.g. "40x20mm"
    ///    in "Rectangle 40x20mm" produced a phantom X20 move).
    /// </summary>
    internal static class LegacyPreviewParser
    {
        public enum LegacyMoveType
        {
            Rapid,
            Linear,
            ArcCW,
            ArcCCW
        }

        /// <summary>One movement segment (test-local, tuple-based).</summary>
        public sealed class LegacySegment
        {
            public (double X, double Y, double Z) Start;
            public (double X, double Y, double Z) End;
            public LegacyMoveType MoveType;
            public (double X, double Y, double Z)? ArcCenter;
            public double ArcRadius;
            public List<(double X, double Y, double Z)> InterpolatedPoints;
        }

        public static List<LegacySegment> Parse(IEnumerable<string> lines)
        {
            var segments = new List<LegacySegment>();
            var currentPos = (X: 0.0, Y: 0.0, Z: 0.0);

            // Modal state - G-codes persist until changed
            var currentMoveType = LegacyMoveType.Rapid; // Default to rapid
            var currentPlane = "G17"; // XY plane default for arcs

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("(") || trimmed.StartsWith(";"))
                    continue;

                // Remove line numbers (N10, N20, etc.)
                var codeLine = trimmed;
                if (codeLine.StartsWith("N", StringComparison.OrdinalIgnoreCase))
                {
                    var spaceIndex = codeLine.IndexOf(' ');
                    if (spaceIndex > 0)
                        codeLine = codeLine.Substring(spaceIndex + 1).Trim();
                    else
                        continue;
                }

                // Comment lines with an N prefix lose their leading "(" after
                // the N-strip — skip them (deliberate fix #3, see class docs).
                if (codeLine.StartsWith("(") || codeLine.StartsWith(";"))
                    continue;

                // Skip program end commands
                if (codeLine.StartsWith("M30", StringComparison.OrdinalIgnoreCase) ||
                    codeLine.StartsWith("M2", StringComparison.OrdinalIgnoreCase))
                    continue;

                // G92 (set start position): updates the tracked position,
                // no move segment (deliberate fix, see class docs).
                if (ContainsGCode(codeLine, "G92"))
                {
                    var gx = ParseCoordinate(codeLine, 'X', currentPos.X);
                    var gy = ParseCoordinate(codeLine, 'Y', currentPos.Y);
                    var gz = ParseCoordinate(codeLine, 'Z', currentPos.Z);
                    currentPos = (X: gx, Y: gy, Z: gz);
                    continue;
                }

                // Parse plane selection (for arcs)
                if (ContainsGCode(codeLine, "G17")) currentPlane = "G17"; // XY
                if (ContainsGCode(codeLine, "G18")) currentPlane = "G18"; // XZ
                if (ContainsGCode(codeLine, "G19")) currentPlane = "G19"; // YZ

                // Parse G-codes - check for move type changes
                var newMoveType = ParseMoveType(codeLine);
                if (newMoveType.HasValue)
                {
                    currentMoveType = newMoveType.Value;
                }

                // Parse coordinates
                var x = ParseCoordinate(codeLine, 'X', currentPos.X);
                var y = ParseCoordinate(codeLine, 'Y', currentPos.Y);
                var z = ParseCoordinate(codeLine, 'Z', currentPos.Z);

                // Parse arc parameters (I, J, K for center offset, R for radius)
                var hasI = TryParseCoordinate(codeLine, 'I', out var i);
                var hasJ = TryParseCoordinate(codeLine, 'J', out var j);
                var hasK = TryParseCoordinate(codeLine, 'K', out var k);
                var hasR = TryParseCoordinate(codeLine, 'R', out var r);

                var newPos = (X: x, Y: y, Z: z);

                // Check if position changed
                if (Math.Abs(newPos.X - currentPos.X) > 0.0001 ||
                    Math.Abs(newPos.Y - currentPos.Y) > 0.0001 ||
                    Math.Abs(newPos.Z - currentPos.Z) > 0.0001)
                {
                    var segment = new LegacySegment
                    {
                        Start = currentPos,
                        End = newPos,
                        MoveType = currentMoveType
                    };

                    // Handle arcs
                    if ((currentMoveType == LegacyMoveType.ArcCW || currentMoveType == LegacyMoveType.ArcCCW) &&
                        (hasI || hasJ || hasK || hasR))
                    {
                        if (hasR)
                        {
                            // Radius format - calculate center
                            segment.ArcRadius = Math.Abs(r);
                            segment.InterpolatedPoints = InterpolateArcByRadius(
                                currentPos, newPos, r, currentMoveType == LegacyMoveType.ArcCW, currentPlane);
                        }
                        else
                        {
                            // Center offset format (I, J, K)
                            var center = (
                                currentPos.X + (hasI ? i : 0),
                                currentPos.Y + (hasJ ? j : 0),
                                currentPos.Z + (hasK ? k : 0));
                            segment.ArcCenter = center;
                            segment.ArcRadius = Distance(currentPos, center);
                            segment.InterpolatedPoints = InterpolateArcByCenter(
                                currentPos, newPos, center, currentMoveType == LegacyMoveType.ArcCW, currentPlane);
                        }
                    }

                    segments.Add(segment);
                    currentPos = newPos;
                }
            }

            return segments;
        }

        private static LegacyMoveType? ParseMoveType(string codeLine)
        {
            // Find all G codes in the line and return the last motion command
            LegacyMoveType? result = null;
            var upperLine = codeLine.ToUpperInvariant();

            int idx = 0;
            while (idx < upperLine.Length)
            {
                var gIndex = upperLine.IndexOf('G', idx);
                if (gIndex < 0) break;

                // Extract the number after G
                var numStart = gIndex + 1;
                var numEnd = numStart;
                while (numEnd < upperLine.Length && (char.IsDigit(upperLine[numEnd]) || upperLine[numEnd] == '.'))
                {
                    numEnd++;
                }

                if (numEnd > numStart)
                {
                    var numStr = upperLine.Substring(numStart, numEnd - numStart);
                    if (double.TryParse(numStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var gNum))
                    {
                        // Check for motion commands
                        if (Math.Abs(gNum - 0) < 0.001) result = LegacyMoveType.Rapid;
                        else if (Math.Abs(gNum - 1) < 0.001) result = LegacyMoveType.Linear;
                        else if (Math.Abs(gNum - 2) < 0.001) result = LegacyMoveType.ArcCW;
                        else if (Math.Abs(gNum - 3) < 0.001) result = LegacyMoveType.ArcCCW;
                    }
                }

                idx = numEnd;
            }

            return result;
        }

        private static bool ContainsGCode(string line, string gCode)
        {
            var upper = line.ToUpperInvariant();
            var code = gCode.ToUpperInvariant();
            return upper.Contains(code);
        }

        private static double ParseCoordinate(string line, char axis, double defaultValue)
        {
            if (TryParseCoordinate(line, axis, out var value))
                return value;
            return defaultValue;
        }

        private static bool TryParseCoordinate(string line, char axis, out double value)
        {
            value = 0;
            var upperLine = line.ToUpperInvariant();
            var axisChar = char.ToUpperInvariant(axis);

            // Find the axis letter, but make sure it's not part of another word
            // (e.g., 'X' in "NEXT" should not match)
            int index = -1;
            for (int i = 0; i < upperLine.Length; i++)
            {
                if (upperLine[i] == axisChar)
                {
                    // Check that previous char is not a letter (to avoid matching in words)
                    if (i == 0 || !char.IsLetter(upperLine[i - 1]))
                    {
                        // Check that next char is a digit, sign, or decimal point
                        if (i + 1 < upperLine.Length)
                        {
                            var nextChar = upperLine[i + 1];
                            if (char.IsDigit(nextChar) || nextChar == '-' || nextChar == '+' || nextChar == '.')
                            {
                                index = i;
                                break;
                            }
                        }
                    }
                }
            }

            if (index < 0) return false;

            var start = index + 1;
            var end = start;

            // Handle optional sign
            if (end < line.Length && (line[end] == '-' || line[end] == '+'))
                end++;

            // Parse digits and decimal point
            bool hasDigit = false;
            while (end < line.Length && (char.IsDigit(line[end]) || line[end] == '.'))
            {
                if (char.IsDigit(line[end]))
                    hasDigit = true;
                end++;
            }

            if (end > start && hasDigit)
            {
                var valueStr = line.Substring(start, end - start);
                return double.TryParse(valueStr, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
            }

            return false;
        }

        private static List<(double X, double Y, double Z)> InterpolateArcByCenter(
            (double X, double Y, double Z) start, (double X, double Y, double Z) end,
            (double X, double Y, double Z) center, bool clockwise, string plane)
        {
            var points = new List<(double X, double Y, double Z)>();

            // Determine which axes to use based on plane
            double startA, startB, endA, endB, centerA, centerB;
            double startC, endC; // The third axis (linear interpolation)

            switch (plane)
            {
                case "G18": // XZ plane
                    startA = start.X; startB = start.Z; startC = start.Y;
                    endA = end.X; endB = end.Z; endC = end.Y;
                    centerA = center.X; centerB = center.Z;
                    break;
                case "G19": // YZ plane
                    startA = start.Y; startB = start.Z; startC = start.X;
                    endA = end.Y; endB = end.Z; endC = end.X;
                    centerA = center.Y; centerB = center.Z;
                    break;
                default: // G17 - XY plane
                    startA = start.X; startB = start.Y; startC = start.Z;
                    endA = end.X; endB = end.Y; endC = end.Z;
                    centerA = center.X; centerB = center.Y;
                    break;
            }

            // Calculate start and end angles
            var startAngle = Math.Atan2(startB - centerB, startA - centerA);
            var endAngle = Math.Atan2(endB - centerB, endA - centerA);
            var radius = Math.Sqrt(Math.Pow(startA - centerA, 2) + Math.Pow(startB - centerB, 2));

            // Adjust angles for direction
            if (clockwise)
            {
                if (endAngle >= startAngle) endAngle -= 2 * Math.PI;
            }
            else
            {
                if (endAngle <= startAngle) endAngle += 2 * Math.PI;
            }

            var totalAngle = Math.Abs(endAngle - startAngle);
            var segments = Math.Max((int)(totalAngle / (Math.PI / 16)), 4); // At least 4 segments

            for (int i = 0; i <= segments; i++)
            {
                var t = (double)i / segments;
                var angle = startAngle + t * (endAngle - startAngle);
                var a = centerA + radius * Math.Cos(angle);
                var b = centerB + radius * Math.Sin(angle);
                var c = startC + t * (endC - startC); // Linear interpolation for third axis

                (double X, double Y, double Z) point;
                switch (plane)
                {
                    case "G18": point = (a, c, b); break;
                    case "G19": point = (c, a, b); break;
                    default: point = (a, b, c); break;
                }
                points.Add(point);
            }

            return points;
        }

        private static List<(double X, double Y, double Z)> InterpolateArcByRadius(
            (double X, double Y, double Z) start, (double X, double Y, double Z) end,
            double radius, bool clockwise, string plane)
        {
            // Calculate center from radius
            double startA, startB, endA, endB;

            switch (plane)
            {
                case "G18":
                    startA = start.X; startB = start.Z;
                    endA = end.X; endB = end.Z;
                    break;
                case "G19":
                    startA = start.Y; startB = start.Z;
                    endA = end.Y; endB = end.Z;
                    break;
                default:
                    startA = start.X; startB = start.Y;
                    endA = end.X; endB = end.Y;
                    break;
            }

            // Midpoint between start and end
            var midA = (startA + endA) / 2;
            var midB = (startB + endB) / 2;

            // Distance from start to end
            var chordLength = Math.Sqrt(Math.Pow(endA - startA, 2) + Math.Pow(endB - startB, 2));

            // Check if radius is valid
            if (Math.Abs(radius) < chordLength / 2)
            {
                // Radius too small, just return linear segment
                return new List<(double X, double Y, double Z)> { start, end };
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

            (double X, double Y, double Z) center;
            switch (plane)
            {
                case "G18": center = (centerA, start.Y, centerB); break;
                case "G19": center = (start.X, centerA, centerB); break;
                default: center = (centerA, centerB, start.Z); break;
            }

            return InterpolateArcByCenter(start, end, center, clockwise, plane);
        }

        private static double Distance((double X, double Y, double Z) a, (double X, double Y, double Z) b) =>
            Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2) + Math.Pow(a.Z - b.Z, 2));
    }
}
