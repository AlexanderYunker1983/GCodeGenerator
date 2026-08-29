#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using GCodeGenerator.Models;
using GCodeGenerator.Toolpath;

namespace GCodeGenerator.GCodeGenerators
{
    /// <summary>
    /// Проверяет уже построенную траекторию по локальному паспорту станка.
    /// Проверка после построения видит компенсацию радиуса инструмента,
    /// врезания и экстремумы дуг, которых нет среди исходных размеров.
    /// </summary>
    internal static class MachineProfileValidator
    {
        private const double AngleTolerance = 1e-12;

        public static IReadOnlyList<OperationValidationFailure> Validate(
            ToolPath toolPath,
            MachineProfileSettings? profile,
            CancellationToken cancellation)
        {
            if (toolPath == null)
                throw new ArgumentNullException(nameof(toolPath));
            if (profile?.Enabled != true)
                return Array.Empty<OperationValidationFailure>();

            var failures = new List<OperationValidationFailure>();
            double? currentX = null;
            double? currentY = null;

            foreach (var operation in toolPath.Operations)
            {
                cancellation.ThrowIfCancellationRequested();
                var issues = new List<ValidationIssue>();
                var reported = new HashSet<string>(StringComparer.Ordinal);

                foreach (var item in operation.Items)
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (item is not ToolMove move)
                        continue;

                    if (move is ArcMove arc && currentX.HasValue && currentY.HasValue)
                    {
                        ValidateArcExtrema(
                            arc, currentX.Value, currentY.Value, profile, issues, reported);
                    }

                    AddCoordinateIssue("Machine.X", move.X, profile.MinX, profile.MaxX, issues, reported);
                    AddCoordinateIssue("Machine.Y", move.Y, profile.MinY, profile.MaxY, issues, reported);
                    AddCoordinateIssue("Machine.Z", move.Z, profile.MinZ, profile.MaxZ, issues, reported);

                    if (move.Feed.HasValue)
                    {
                        var property = move.Kind == ToolMoveKind.Rapid
                            ? "Machine.RapidFeed"
                            : "Machine.WorkFeed";
                        var limit = move.Kind == ToolMoveKind.Rapid
                            ? profile.MaxRapidFeed
                            : profile.MaxWorkFeed;
                        if (move.Feed.Value > limit && reported.Add(property))
                        {
                            issues.Add(new ValidationIssue(
                                property,
                                ValidationCode.AboveMaximum,
                                $"machine-profile limit is {Text(limit)} mm/min, but the tool path requests {Text(move.Feed.Value)} mm/min",
                                limit));
                        }
                    }

                    currentX = move.X ?? currentX;
                    currentY = move.Y ?? currentY;
                }

                if (issues.Count > 0)
                {
                    failures.Add(new OperationValidationFailure(
                        operation.SourceIndex < 0 ? 0 : operation.SourceIndex,
                        operation.Name,
                        operation.Source?.GetType().Name ?? "ToolPathOperation",
                        issues));
                }
            }

            return failures;
        }

        private static void ValidateArcExtrema(
            ArcMove arc,
            double startX,
            double startY,
            MachineProfileSettings profile,
            IList<ValidationIssue> issues,
            ISet<string> reported)
        {
            var centerX = startX + arc.ArcCenterOffsetX;
            var centerY = startY + arc.ArcCenterOffsetY;
            var radius = Math.Sqrt(
                arc.ArcCenterOffsetX * arc.ArcCenterOffsetX
                + arc.ArcCenterOffsetY * arc.ArcCenterOffsetY);
            if (!(radius > 0) || !double.IsFinite(radius))
                return;

            var startAngle = Math.Atan2(startY - centerY, startX - centerX);
            var endAngle = Math.Atan2(arc.EndY - centerY, arc.EndX - centerX);
            var sweep = arc.Kind == ToolMoveKind.ArcClockwise
                ? PositiveAngle(startAngle - endAngle)
                : PositiveAngle(endAngle - startAngle);
            var fullCircle = sweep <= AngleTolerance
                && Math.Abs(arc.EndX - startX) <= AngleTolerance
                && Math.Abs(arc.EndY - startY) <= AngleTolerance;

            var cardinalAngles = new[] { 0.0, Math.PI / 2, Math.PI, 3 * Math.PI / 2 };
            foreach (var angle in cardinalAngles)
            {
                var distance = arc.Kind == ToolMoveKind.ArcClockwise
                    ? PositiveAngle(startAngle - angle)
                    : PositiveAngle(angle - startAngle);
                if (!fullCircle && distance > sweep + AngleTolerance)
                    continue;

                AddCoordinateIssue(
                    "Machine.X", centerX + radius * Math.Cos(angle),
                    profile.MinX, profile.MaxX, issues, reported);
                AddCoordinateIssue(
                    "Machine.Y", centerY + radius * Math.Sin(angle),
                    profile.MinY, profile.MaxY, issues, reported);
            }
        }

        private static double PositiveAngle(double angle)
        {
            var result = angle % (2 * Math.PI);
            return result < 0 ? result + 2 * Math.PI : result;
        }

        private static void AddCoordinateIssue(
            string property,
            double? value,
            double min,
            double max,
            IList<ValidationIssue> issues,
            ISet<string> reported)
        {
            if (!value.HasValue || !reported.Add(property))
                return;

            if (value.Value < min)
            {
                issues.Add(new ValidationIssue(
                    property,
                    ValidationCode.BelowMinimum,
                    $"machine-profile minimum is {Text(min)} mm, but the tool path reaches {Text(value.Value)} mm",
                    min));
            }
            else if (value.Value > max)
            {
                issues.Add(new ValidationIssue(
                    property,
                    ValidationCode.AboveMaximum,
                    $"machine-profile maximum is {Text(max)} mm, but the tool path reaches {Text(value.Value)} mm",
                    max));
            }
            else
            {
                reported.Remove(property);
            }
        }

        private static string Text(double value)
            => value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
