#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Media.Media3D;
using GCodeGenerator.Trajectory;

namespace GCodeGenerator.Views.Scene
{
    /// <summary>Диапазон одной оси координатной сетки.</summary>
    internal sealed class CoordinateGridAxisRange
    {
        public CoordinateGridAxisRange(double minimum, double maximum, IReadOnlyList<double> ticks)
        {
            Minimum = minimum;
            Maximum = maximum;
            Ticks = ticks;
        }

        public double Minimum { get; }

        public double Maximum { get; }

        public IReadOnlyList<double> Ticks { get; }
    }

    /// <summary>Общий масштаб и диапазоны трёх плоскостей сетки.</summary>
    internal sealed class CoordinateGridLayout
    {
        public CoordinateGridLayout(
            double step,
            double lineThickness,
            double labelHeight,
            CoordinateGridAxisRange x,
            CoordinateGridAxisRange y,
            CoordinateGridAxisRange z)
        {
            Step = step;
            LineThickness = lineThickness;
            LabelHeight = labelHeight;
            X = x;
            Y = y;
            Z = z;
        }

        public double Step { get; }

        public double LineThickness { get; }

        public double LabelHeight { get; }

        public CoordinateGridAxisRange X { get; }

        public CoordinateGridAxisRange Y { get; }

        public CoordinateGridAxisRange Z { get; }
    }

    /// <summary>Линии, числовые отметки и готовая модель одной плоскости.</summary>
    internal sealed class CoordinateGridPlaneModel
    {
        public CoordinateGridPlaneModel(
            MeshGeometry3D lines,
            MeshGeometry3D labels,
            Model3DGroup model)
        {
            Lines = lines;
            Labels = labels;
            Model = model;
        }

        public MeshGeometry3D Lines { get; }

        public MeshGeometry3D Labels { get; }

        public Model3DGroup Model { get; }
    }

    /// <summary>Три независимо включаемые основные координатные плоскости.</summary>
    internal sealed class CoordinateGridModels
    {
        public CoordinateGridModels(
            CoordinateGridLayout layout,
            CoordinateGridPlaneModel xy,
            CoordinateGridPlaneModel xz,
            CoordinateGridPlaneModel yz)
        {
            Layout = layout;
            Xy = xy;
            Xz = xz;
            Yz = yz;
        }

        public CoordinateGridLayout Layout { get; }

        public CoordinateGridPlaneModel Xy { get; }

        public CoordinateGridPlaneModel Xz { get; }

        public CoordinateGridPlaneModel Yz { get; }
    }

    /// <summary>
    /// Строит адаптивные координатные сетки XY, XZ и YZ. Все три используют
    /// один физический шаг, поэтому клетки сохраняют одинаковый размер при
    /// переходе между плоскостями. Числа собраны из векторных штрихов прямо
    /// в 3D: им не нужны текстуры, шрифты и отдельные WPF-элементы на каждую
    /// отметку.
    /// </summary>
    internal static class CoordinateGridBuilder
    {
        private const int TargetIntervals = 10;
        private const double DefaultHalfRange = 10.0;
        private const double MinimumSpan = 1e-6;
        private const double LabelHeightToStep = 0.24;
        private const double GridThicknessToStep = 0.012;
        private const double MinimumGridThickness = 0.01;

        private static readonly Vector3D XAxis = new Vector3D(1, 0, 0);
        private static readonly Vector3D YAxis = new Vector3D(0, 1, 0);
        private static readonly Vector3D ZAxis = new Vector3D(0, 0, 1);

        private const int SegmentTop = 1;
        private const int SegmentUpperRight = 2;
        private const int SegmentLowerRight = 4;
        private const int SegmentBottom = 8;
        private const int SegmentLowerLeft = 16;
        private const int SegmentUpperLeft = 32;
        private const int SegmentMiddle = 64;

        private static readonly int[] DigitSegments =
        {
            SegmentTop | SegmentUpperRight | SegmentLowerRight | SegmentBottom | SegmentLowerLeft | SegmentUpperLeft,
            SegmentUpperRight | SegmentLowerRight,
            SegmentTop | SegmentUpperRight | SegmentMiddle | SegmentLowerLeft | SegmentBottom,
            SegmentTop | SegmentUpperRight | SegmentLowerRight | SegmentBottom | SegmentMiddle,
            SegmentUpperLeft | SegmentMiddle | SegmentUpperRight | SegmentLowerRight,
            SegmentTop | SegmentUpperLeft | SegmentMiddle | SegmentLowerRight | SegmentBottom,
            SegmentTop | SegmentUpperLeft | SegmentMiddle | SegmentLowerLeft | SegmentLowerRight | SegmentBottom,
            SegmentTop | SegmentUpperRight | SegmentLowerRight,
            SegmentTop | SegmentUpperRight | SegmentLowerRight | SegmentBottom | SegmentLowerLeft | SegmentUpperLeft | SegmentMiddle,
            SegmentTop | SegmentUpperRight | SegmentLowerRight | SegmentBottom | SegmentUpperLeft | SegmentMiddle,
        };

        /// <summary>Строит модели сразу для всех плоскостей; окно затем лишь переключает ссылки на них.</summary>
        public static CoordinateGridModels Build(TrajectoryScene? scene, SceneMaterials materials)
        {
            if (materials == null)
                throw new ArgumentNullException(nameof(materials));

            var layout = CreateLayout(scene);
            return new CoordinateGridModels(
                layout,
                BuildPlane(layout.X, layout.Y, XAxis, YAxis, layout, materials),
                BuildPlane(layout.X, layout.Z, XAxis, ZAxis, layout, materials),
                BuildPlane(layout.Y, layout.Z, YAxis, ZAxis, layout, materials));
        }

        /// <summary>Вычисляет красивый шаг 1–2–5 и диапазоны, включающие траекторию и ноль детали.</summary>
        public static CoordinateGridLayout CreateLayout(TrajectoryScene? scene)
        {
            double minX;
            double maxX;
            double minY;
            double maxY;
            double minZ;
            double maxZ;

            var bounds = scene?.Bounds;
            if (bounds == null || !Finite(bounds.Value.Min) || !Finite(bounds.Value.Max))
            {
                minX = minY = minZ = -DefaultHalfRange;
                maxX = maxY = maxZ = DefaultHalfRange;
            }
            else
            {
                minX = Math.Min(0, bounds.Value.Min.X);
                maxX = Math.Max(0, bounds.Value.Max.X);
                minY = Math.Min(0, bounds.Value.Min.Y);
                maxY = Math.Max(0, bounds.Value.Max.Y);
                minZ = Math.Min(0, bounds.Value.Min.Z);
                maxZ = Math.Max(0, bounds.Value.Max.Z);
            }

            // Координатные стрелки для небольшой программы имеют плечо
            // 10 мм. Сетка не должна сжиматься у нуля заметно сильнее осей:
            // иначе деления формально есть, но занимают несколько пикселей.
            minX = Math.Min(minX, -DefaultHalfRange);
            maxX = Math.Max(maxX, DefaultHalfRange);
            minY = Math.Min(minY, -DefaultHalfRange);
            maxY = Math.Max(maxY, DefaultHalfRange);
            minZ = Math.Min(minZ, -DefaultHalfRange);
            maxZ = Math.Max(maxZ, DefaultHalfRange);

            var span = Math.Max(Math.Max(maxX - minX, maxY - minY), maxZ - minZ);
            if (!double.IsFinite(span) || span < MinimumSpan)
                span = DefaultHalfRange * 2;

            var step = NiceStep(span / TargetIntervals);
            var lineThickness = Math.Max(step * GridThicknessToStep, MinimumGridThickness);
            var labelHeight = step * LabelHeightToStep;

            return new CoordinateGridLayout(
                step,
                lineThickness,
                labelHeight,
                CreateRange(minX, maxX, step),
                CreateRange(minY, maxY, step),
                CreateRange(minZ, maxZ, step));
        }

        private static CoordinateGridPlaneModel BuildPlane(
            CoordinateGridAxisRange horizontal,
            CoordinateGridAxisRange vertical,
            Vector3D horizontalAxis,
            Vector3D verticalAxis,
            CoordinateGridLayout layout,
            SceneMaterials materials)
        {
            var lines = new MeshGeometry3D();
            var labels = new MeshGeometry3D();
            var origin = new Point3D(0, 0, 0);

            Point3D PointAt(double horizontalValue, double verticalValue)
                => origin + horizontalAxis * horizontalValue + verticalAxis * verticalValue;

            foreach (var value in horizontal.Ticks)
            {
                SceneGeometry.AddLine(
                    lines,
                    PointAt(value, vertical.Minimum),
                    PointAt(value, vertical.Maximum),
                    layout.LineThickness);
            }

            foreach (var value in vertical.Ticks)
            {
                SceneGeometry.AddLine(
                    lines,
                    PointAt(horizontal.Minimum, value),
                    PointAt(horizontal.Maximum, value),
                    layout.LineThickness);
            }

            var labelOffset = layout.LabelHeight * 1.15;
            foreach (var value in horizontal.Ticks)
            {
                if (IsZero(value, layout.Step))
                    continue;
                AddText(
                    labels,
                    FormatValue(value, layout.Step),
                    PointAt(value, -labelOffset),
                    horizontalAxis,
                    verticalAxis,
                    layout.LabelHeight);
            }

            foreach (var value in vertical.Ticks)
            {
                if (IsZero(value, layout.Step))
                    continue;
                AddText(
                    labels,
                    FormatValue(value, layout.Step),
                    PointAt(-labelOffset, value),
                    horizontalAxis,
                    verticalAxis,
                    layout.LabelHeight);
            }

            var model = new Model3DGroup();
            AddModel(model, lines, materials.GridLines);
            AddModel(model, labels, materials.GridLabels);
            model.Freeze();
            return new CoordinateGridPlaneModel(lines, labels, model);
        }

        private static void AddModel(Model3DGroup group, MeshGeometry3D mesh, Material material)
        {
            if (mesh.Positions == null || mesh.Positions.Count == 0)
                return;
            group.Children.Add(new GeometryModel3D(mesh, material) { BackMaterial = material });
        }

        private static CoordinateGridAxisRange CreateRange(double minimum, double maximum, double step)
        {
            double first;
            double last;
            if (maximum - minimum < MinimumSpan)
            {
                first = -5 * step;
                last = 5 * step;
            }
            else
            {
                first = Math.Floor(minimum / step) * step;
                last = Math.Ceiling(maximum / step) * step;
                if (IsZero(first - minimum, step))
                    first -= step;
                if (IsZero(last - maximum, step))
                    last += step;
            }

            var count = Math.Max(1, (int)Math.Round((last - first) / step));
            var ticks = new List<double>(count + 1);
            for (var index = 0; index <= count; index++)
            {
                var value = first + index * step;
                ticks.Add(IsZero(value, step) ? 0 : value);
            }
            return new CoordinateGridAxisRange(first, last, ticks);
        }

        private static double NiceStep(double rawStep)
        {
            if (!double.IsFinite(rawStep) || rawStep <= 0)
                return 1;

            var exponent = Math.Floor(Math.Log10(rawStep));
            var magnitude = Math.Pow(10, exponent);
            var fraction = rawStep / magnitude;
            var niceFraction = fraction <= 1 ? 1
                : fraction <= 2 ? 2
                : fraction <= 5 ? 5
                : 10;
            return niceFraction * magnitude;
        }

        private static bool Finite(Vec3 point)
            => double.IsFinite(point.X) && double.IsFinite(point.Y) && double.IsFinite(point.Z);

        private static bool IsZero(double value, double step)
            => Math.Abs(value) <= step * 1e-9;

        private static string FormatValue(double value, double step)
        {
            var decimals = step < 1
                ? Math.Min(6, Math.Max(0, (int)Math.Ceiling(-Math.Log10(step))))
                : 0;
            var format = decimals == 0 ? "0" : "0." + new string('0', decimals);
            return value.ToString(format, CultureInfo.InvariantCulture);
        }

        private static void AddText(
            MeshGeometry3D mesh,
            string text,
            Point3D center,
            Vector3D horizontalAxis,
            Vector3D verticalAxis,
            double height)
        {
            var spacing = height * 0.12;
            var totalWidth = 0.0;
            for (var index = 0; index < text.Length; index++)
            {
                totalWidth += CharacterWidth(text[index], height);
                if (index + 1 < text.Length)
                    totalWidth += spacing;
            }

            var cursor = -totalWidth / 2.0;
            foreach (var character in text)
            {
                var width = CharacterWidth(character, height);
                var characterCenter = center + horizontalAxis * (cursor + width / 2.0);
                AddCharacter(mesh, character, characterCenter, horizontalAxis, verticalAxis, width, height);
                cursor += width + spacing;
            }
        }

        private static double CharacterWidth(char character, double height)
            => character == '.' ? height * 0.2 : height * 0.58;

        private static void AddCharacter(
            MeshGeometry3D mesh,
            char character,
            Point3D center,
            Vector3D horizontalAxis,
            Vector3D verticalAxis,
            double width,
            double height)
        {
            var thickness = height * 0.08;
            var left = -width / 2.0;
            var right = width / 2.0;
            var top = height / 2.0;
            var bottom = -height / 2.0;

            if (character == '.')
            {
                AddStroke(mesh, center, horizontalAxis, verticalAxis,
                    -width * 0.25, bottom, width * 0.25, bottom, thickness);
                return;
            }

            if (character == '-')
            {
                AddStroke(mesh, center, horizontalAxis, verticalAxis,
                    left, 0, right, 0, thickness);
                return;
            }

            if (character < '0' || character > '9')
                return;

            var segments = DigitSegments[character - '0'];
            if ((segments & SegmentTop) != 0)
                AddStroke(mesh, center, horizontalAxis, verticalAxis, left, top, right, top, thickness);
            if ((segments & SegmentUpperRight) != 0)
                AddStroke(mesh, center, horizontalAxis, verticalAxis, right, top, right, 0, thickness);
            if ((segments & SegmentLowerRight) != 0)
                AddStroke(mesh, center, horizontalAxis, verticalAxis, right, 0, right, bottom, thickness);
            if ((segments & SegmentBottom) != 0)
                AddStroke(mesh, center, horizontalAxis, verticalAxis, left, bottom, right, bottom, thickness);
            if ((segments & SegmentLowerLeft) != 0)
                AddStroke(mesh, center, horizontalAxis, verticalAxis, left, 0, left, bottom, thickness);
            if ((segments & SegmentUpperLeft) != 0)
                AddStroke(mesh, center, horizontalAxis, verticalAxis, left, top, left, 0, thickness);
            if ((segments & SegmentMiddle) != 0)
                AddStroke(mesh, center, horizontalAxis, verticalAxis, left, 0, right, 0, thickness);
        }

        private static void AddStroke(
            MeshGeometry3D mesh,
            Point3D center,
            Vector3D horizontalAxis,
            Vector3D verticalAxis,
            double startX,
            double startY,
            double endX,
            double endY,
            double thickness)
        {
            SceneGeometry.AddLine(
                mesh,
                center + horizontalAxis * startX + verticalAxis * startY,
                center + horizontalAxis * endX + verticalAxis * endY,
                thickness);
        }
    }
}
