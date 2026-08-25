using System;
using System.Collections.Generic;

namespace GCodeGenerator.Models
{
    /// <summary>
    /// Строит список отверстий по параметрам шаблона сверления.
    ///
    /// Раньше эти формулы жили в девяти view-моделях диалогов: ядро не могло
    /// пересчитать отверстия по параметрам операции, а тесты вынужденно
    /// повторяли те же вычисления вместо того, чтобы проверять их. Список
    /// отверстий при этом хранится в операции и сохраняется в проект, то есть
    /// параметры шаблона и его результат могли разойтись — например, если файл
    /// проекта отредактирован вне программы.
    ///
    /// Формулы перенесены дословно, поэтому программы для существующих
    /// проектов не меняются.
    /// </summary>
    public static class DrillPatternBuilder
    {
        /// <summary>
        /// Отверстия шаблона операции. Для режима <see cref="DrillMode.Points"/>
        /// шаблона нет: отверстия задаются пользователем поштучно и
        /// возвращаются как есть.
        /// </summary>
        /// <param name="operation">Операция сверления.</param>
        public static List<DrillHole> Build(DrillPointsOperation operation)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            switch (operation.DrillMode)
            {
                case DrillMode.Line: return BuildLine(operation);
                case DrillMode.Array: return BuildArray(operation);
                case DrillMode.Rect: return BuildRectangle(operation);
                case DrillMode.Circle: return BuildCircle(operation);
                case DrillMode.Arc: return BuildArc(operation);
                case DrillMode.Polygon: return BuildPolygon(operation);
                case DrillMode.Ellipse: return BuildEllipse(operation);
                case DrillMode.Package: return BuildPackage(operation);
                case DrillMode.Points:
                default:
                    return new List<DrillHole>(operation.Holes);
            }
        }

        /// <summary>Отверстие шаблона: координаты плюс общие параметры глубины и подач.</summary>
        private static DrillHole Hole(DrillPointsOperation operation, double x, double y, double z)
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

        private static List<DrillHole> BuildLine(DrillPointsOperation operation)
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

        private static List<DrillHole> BuildArray(DrillPointsOperation operation)
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

        private static List<DrillHole> BuildRectangle(DrillPointsOperation operation)
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

        private static List<DrillHole> BuildCircle(DrillPointsOperation operation)
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

        private static List<DrillHole> BuildArc(DrillPointsOperation operation)
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

        private static List<DrillHole> BuildPolygon(DrillPointsOperation operation)
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

        private static List<DrillHole> BuildEllipse(DrillPointsOperation operation)
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

        private static List<DrillHole> BuildPackage(DrillPointsOperation operation)
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
}
