#nullable enable
using System;
using System.Collections.Generic;

namespace GCodeGenerator.Models
{
    /// <summary>
    /// Шаблон расстановки отверстий: по нему операция сверления вычисляет,
    /// что именно сверлить.
    ///
    /// Раньше все девять способов расстановки разбирались переключателем в
    /// одном классе на три сотни строк, а параметры каждого способа лежали
    /// вперемешку в самой операции. Способ, добавленный в перечисление, но
    /// забытый в переключателе, молча давал пустой список отверстий — то есть
    /// операцию, которая ничего не сверлит.
    ///
    /// Формулы перенесены дословно, поэтому программы для существующих
    /// проектов не меняются.
    /// </summary>
    public abstract class DrillPattern
    {
        /// <summary>Способ расстановки, который описывает этот шаблон.</summary>
        public abstract DrillMode Mode { get; }

        /// <summary>Отверстия шаблона по параметрам операции.</summary>
        /// <param name="operation">Операция сверления.</param>
        public IReadOnlyList<DrillHole> Holes(DrillPointsOperation operation)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            return Build(operation);
        }

        /// <summary>Расстановка отверстий: формулы конкретного шаблона.</summary>
        /// <param name="operation">Операция сверления.</param>
        protected abstract List<DrillHole> Build(DrillPointsOperation operation);

        /// <summary>
        /// Отверстие шаблона: координаты плюс общие параметры глубины и подач,
        /// одинаковые для всех отверстий операции.
        /// </summary>
        /// <param name="operation">Операция сверления.</param>
        /// <param name="x">Координата X отверстия.</param>
        /// <param name="y">Координата Y отверстия.</param>
        /// <param name="z">Координата Z поверхности.</param>
        protected static DrillHole Hole(DrillPointsOperation operation, double x, double y, double z)
            => new DrillHole
            {
                X = x,
                Y = y,
                Z = z,
                TotalDepth = operation.TotalDepth,
                StepDepth = operation.StepDepth,
                FeedZRapid = operation.FeedZRapid,
                FeedZWork = operation.FeedZWork,
                RetractHeight = operation.RetractHeight
            };
    }

    /// <summary>
    /// Поштучный «шаблон»: отверстия задаёт пользователь, вычислять нечего.
    /// Существует, чтобы у каждого режима был свой шаблон и вызывающему коду
    /// не приходилось выделять этот случай отдельно.
    /// </summary>
    public sealed class PointsDrillPattern : DrillPattern
    {
        public override DrillMode Mode => DrillMode.Points;

        protected override List<DrillHole> Build(DrillPointsOperation operation)
            => new List<DrillHole>(operation.Holes);
    }

    /// <summary>Отверстия по прямой линии под заданным углом.</summary>
    public sealed class LineDrillPattern : DrillPattern
    {
        public override DrillMode Mode => DrillMode.Line;

        protected override List<DrillHole> Build(DrillPointsOperation operation)
        {
            var holes = new List<DrillHole>();
            if (operation.HoleCount <= 0 || operation.Distance == 0)
                return holes;

            var angleRad = operation.AngleDeg * Math.PI / 180.0;
            var dx = operation.Distance * Math.Cos(angleRad);
            var dy = operation.Distance * Math.Sin(angleRad);

            for (int i = 0; i < operation.HoleCount; i++)
                holes.Add(Hole(operation, operation.StartX + dx * i, operation.StartY + dy * i, operation.StartZ));

            return holes;
        }
    }

    /// <summary>Прямоугольная сетка отверстий: все узлы.</summary>
    public sealed class ArrayDrillPattern : DrillPattern
    {
        public override DrillMode Mode => DrillMode.Array;

        protected override List<DrillHole> Build(DrillPointsOperation operation)
        {
            var holes = new List<DrillHole>();
            if (operation.HoleCount <= 0 || operation.Distance == 0 || operation.RowCount <= 0)
                return holes;

            var angleRad = operation.AngleDeg * Math.PI / 180.0;
            var dx = operation.Distance * Math.Cos(angleRad);
            var dy = operation.Distance * Math.Sin(angleRad);

            // Направление рядов перпендикулярно линии (поворот на 90° против часовой стрелки)
            var px = -Math.Sin(angleRad) * operation.RowPitch;
            var py = Math.Cos(angleRad) * operation.RowPitch;

            for (int row = 0; row < operation.RowCount; row++)
            {
                for (int col = 0; col < operation.HoleCount; col++)
                {
                    holes.Add(Hole(
                        operation,
                        operation.StartX + dx * col + px * row,
                        operation.StartY + dy * col + py * row,
                        operation.StartZ));
                }
            }

            return holes;
        }
    }

    /// <summary>Прямоугольная рамка: только узлы по периметру сетки.</summary>
    public sealed class RectDrillPattern : DrillPattern
    {
        public override DrillMode Mode => DrillMode.Rect;

        protected override List<DrillHole> Build(DrillPointsOperation operation)
        {
            var holes = new List<DrillHole>();
            if (operation.HoleCount <= 1 || operation.Distance == 0 || operation.RowCount <= 1)
                return holes;

            var angleRad = operation.AngleDeg * Math.PI / 180.0;
            var dx = operation.Distance * Math.Cos(angleRad);
            var dy = operation.Distance * Math.Sin(angleRad);

            var px = -Math.Sin(angleRad) * operation.RowPitch;
            var py = Math.Cos(angleRad) * operation.RowPitch;

            for (int row = 0; row < operation.RowCount; row++)
            {
                for (int col = 0; col < operation.HoleCount; col++)
                {
                    // Только периметр: внутренние узлы сетки пропускаются.
                    var isBorderRow = row == 0 || row == operation.RowCount - 1;
                    var isBorderCol = col == 0 || col == operation.HoleCount - 1;
                    if (!(isBorderRow || isBorderCol))
                        continue;

                    holes.Add(Hole(
                        operation,
                        operation.StartX + dx * col + px * row,
                        operation.StartY + dy * col + py * row,
                        operation.StartZ));
                }
            }

            return holes;
        }
    }

    /// <summary>Отверстия, равномерно расставленные по окружности.</summary>
    public sealed class CircleDrillPattern : DrillPattern
    {
        public override DrillMode Mode => DrillMode.Circle;

        protected override List<DrillHole> Build(DrillPointsOperation operation)
        {
            var holes = new List<DrillHole>();
            if (operation.HoleCount < 2 || operation.Radius == 0)
                return holes;

            var startRad = operation.StartAngleDeg * Math.PI / 180.0;
            var stepRad = 2 * Math.PI / operation.HoleCount;

            for (int i = 0; i < operation.HoleCount; i++)
            {
                var angle = startRad + stepRad * i;
                holes.Add(Hole(
                    operation,
                    operation.CenterX + operation.Radius * Math.Cos(angle),
                    operation.CenterY + operation.Radius * Math.Sin(angle),
                    operation.Z));
            }

            return holes;
        }
    }

    /// <summary>Отверстия по дуге между начальным и конечным углом.</summary>
    public sealed class ArcDrillPattern : DrillPattern
    {
        public override DrillMode Mode => DrillMode.Arc;

        protected override List<DrillHole> Build(DrillPointsOperation operation)
        {
            var holes = new List<DrillHole>();
            if (operation.HoleCount < 1 || operation.Radius == 0)
                return holes;

            var startRad = operation.StartAngleDeg * Math.PI / 180.0;
            var endRad = operation.EndAngleDeg * Math.PI / 180.0;

            // Раскрыв дуги приводится к диапазону [0, 2π]: дуга может проходить
            // через ноль градусов.
            var arcSpan = endRad - startRad;
            while (arcSpan < 0) arcSpan += 2 * Math.PI;
            while (arcSpan > 2 * Math.PI) arcSpan -= 2 * Math.PI;

            // Нулевой раскрыв трактуется как полная окружность.
            if (arcSpan < 0.001)
                arcSpan = 2 * Math.PI;

            var stepRad = operation.HoleCount > 1 ? arcSpan / (operation.HoleCount - 1) : 0;

            for (int i = 0; i < operation.HoleCount; i++)
            {
                var angle = startRad + stepRad * i;
                holes.Add(Hole(
                    operation,
                    operation.CenterX + operation.Radius * Math.Cos(angle),
                    operation.CenterY + operation.Radius * Math.Sin(angle),
                    operation.Z));
            }

            return holes;
        }
    }

    /// <summary>Отверстия по сторонам правильного многоугольника.</summary>
    public sealed class PolygonDrillPattern : DrillPattern
    {
        public override DrillMode Mode => DrillMode.Polygon;

        protected override List<DrillHole> Build(DrillPointsOperation operation)
        {
            var holes = new List<DrillHole>();
            if (operation.NumberOfSides < 3 || operation.Radius == 0 || operation.HolesPerSide < 1)
                return holes;

            var rotationRad = operation.RotationAngle * Math.PI / 180.0;
            var angleStep = 2 * Math.PI / operation.NumberOfSides;

            var vertices = new List<(double x, double y)>(operation.NumberOfSides);
            for (int i = 0; i < operation.NumberOfSides; i++)
            {
                var angle = i * angleStep + rotationRad;
                vertices.Add((
                    operation.CenterX + operation.Radius * Math.Cos(angle),
                    operation.CenterY + operation.Radius * Math.Sin(angle)));
            }

            for (int side = 0; side < operation.NumberOfSides; side++)
            {
                var startVertex = vertices[side];
                var endVertex = vertices[(side + 1) % operation.NumberOfSides];

                var dx = endVertex.x - startVertex.x;
                var dy = endVertex.y - startVertex.y;

                // Первое отверстие стороны попадает в вершину, остальные
                // распределяются по стороне; конечная вершина пропускается,
                // чтобы не задвоить отверстие на стыке сторон.
                var stepX = operation.HolesPerSide > 1 ? dx / operation.HolesPerSide : 0;
                var stepY = operation.HolesPerSide > 1 ? dy / operation.HolesPerSide : 0;

                for (int holeIndex = 0; holeIndex < operation.HolesPerSide; holeIndex++)
                {
                    holes.Add(Hole(
                        operation,
                        startVertex.x + stepX * holeIndex,
                        startVertex.y + stepY * holeIndex,
                        operation.Z));
                }
            }

            return holes;
        }
    }

    /// <summary>Отверстия по эллипсу с поворотом.</summary>
    public sealed class EllipseDrillPattern : DrillPattern
    {
        public override DrillMode Mode => DrillMode.Ellipse;

        protected override List<DrillHole> Build(DrillPointsOperation operation)
        {
            var holes = new List<DrillHole>();
            if (operation.HoleCount < 2 || operation.RadiusX == 0 || operation.RadiusY == 0)
                return holes;

            var startRad = operation.StartAngleDeg * Math.PI / 180.0;
            var stepRad = 2 * Math.PI / operation.HoleCount;
            var rotationRad = operation.RotationAngle * Math.PI / 180.0;
            var cosRot = Math.Cos(rotationRad);
            var sinRot = Math.Sin(rotationRad);

            for (int i = 0; i < operation.HoleCount; i++)
            {
                var angle = startRad + stepRad * i;
                var xEllipse = operation.RadiusX * Math.Cos(angle);
                var yEllipse = operation.RadiusY * Math.Sin(angle);

                holes.Add(Hole(
                    operation,
                    operation.CenterX + xEllipse * cosRot - yEllipse * sinRot,
                    operation.CenterY + xEllipse * sinRot + yEllipse * cosRot,
                    operation.Z));
            }

            return holes;
        }
    }

    /// <summary>Отверстия под выводы корпуса микросхемы.</summary>
    public sealed class PackageDrillPattern : DrillPattern
    {
        public override DrillMode Mode => DrillMode.Package;

        protected override List<DrillHole> Build(DrillPointsOperation operation)
        {
            var holes = new List<DrillHole>();
            var package = PackageCatalog.FindOrDefault(operation.PackageName);
            if (package == null || package.PinsPerRow < 1)
                return holes;

            var angleRad = operation.RotationAngle * Math.PI / 180.0;
            var cos = Math.Cos(angleRad);
            var sin = Math.Sin(angleRad);

            var totalPinLength = (package.PinsPerRow - 1) * package.PinPitch;
            var halfPinLength = totalPinLength / 2.0;

            void AddPin(double localX, double localY)
            {
                holes.Add(Hole(
                    operation,
                    operation.CenterX + localX * cos - localY * sin,
                    operation.CenterY + localX * sin + localY * cos,
                    operation.Z));
            }

            if (package.RowSpacing > 0)
            {
                // Двухрядный корпус: выводы нумеруются по кругу, поэтому второй
                // ряд идёт в обратном порядке.
                var halfRowSpacing = package.RowSpacing / 2.0;

                for (int i = 0; i < package.PinsPerRow; i++)
                    AddPin(-halfRowSpacing, -halfPinLength + i * package.PinPitch);

                for (int i = 0; i < package.PinsPerRow; i++)
                    AddPin(halfRowSpacing, halfPinLength - i * package.PinPitch);
            }
            else
            {
                // Однорядный корпус.
                for (int i = 0; i < package.PinsPerRow; i++)
                    AddPin(0.0, -halfPinLength + i * package.PinPitch);
            }

            return holes;
        }
    }

    /// <summary>
    /// Соответствие «способ расстановки → шаблон».
    ///
    /// Полнота реестра относительно перечисления проверяется тестом: способ
    /// без шаблона означал бы операцию, которая ничего не сверлит.
    /// </summary>
    public static class DrillPatterns
    {
        private static readonly Dictionary<DrillMode, DrillPattern> Registry = BuildRegistry();

        private static Dictionary<DrillMode, DrillPattern> BuildRegistry()
        {
            var patterns = new DrillPattern[]
            {
                new PointsDrillPattern(),
                new LineDrillPattern(),
                new ArrayDrillPattern(),
                new RectDrillPattern(),
                new CircleDrillPattern(),
                new ArcDrillPattern(),
                new PolygonDrillPattern(),
                new EllipseDrillPattern(),
                new PackageDrillPattern(),
            };

            var registry = new Dictionary<DrillMode, DrillPattern>(patterns.Length);
            foreach (var pattern in patterns)
                registry[pattern.Mode] = pattern;
            return registry;
        }

        /// <summary>Все зарегистрированные шаблоны — для проверки полноты.</summary>
        public static IReadOnlyDictionary<DrillMode, DrillPattern> All => Registry;

        /// <summary>
        /// Шаблон для способа расстановки. Незнакомое значение — отказ:
        /// файл проекта, принесший неизвестный режим, не должен молча
        /// превращаться в операцию без отверстий.
        /// </summary>
        /// <param name="mode">Способ расстановки отверстий.</param>
        public static DrillPattern For(DrillMode mode)
        {
            if (Registry.TryGetValue(mode, out var pattern))
                return pattern;

            throw new NotSupportedException($"Способ расстановки отверстий {(int)mode} не поддерживается.");
        }
    }
}
