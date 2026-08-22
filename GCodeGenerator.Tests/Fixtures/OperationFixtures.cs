using System;
using System.Collections.Generic;
using GCodeGenerator.Models;

namespace GCodeGenerator.Tests.Fixtures
{
    /// <summary>
    /// Фабрики операций для фикстур.
    /// Формулы построения отверстий сверления повторяют RebuildHoles() во ViewModels/Drill/*,
    /// ключи Metadata — те, что VM записывают в OnClosed (в пункте 3 плана они будут заменены
    /// типизированными свойствами; до тех пор фикстуры обязаны совпадать с поведением UI).
    /// </summary>
    public static class OperationFixtures
    {
        // Общие Z-параметры отверстий (значения по умолчанию в VM сверления).
        private const double DefaultTotalDepth = 2.0;
        private const double DefaultStepDepth = 1.0;
        private const double DefaultFeedZRapid = 500.0;
        private const double DefaultFeedZWork = 200.0;
        private const double DefaultRetractHeight = 0.3;

        // Общие параметры фрезерных операций.
        private const double DefaultMillingDepth = 2.0;
        private const double DefaultMillingStep = 1.0;
        private const double DefaultToolDiameter = 3.0;

        private static DrillHole Hole(double x, double y, double z = 0.0)
        {
            return new DrillHole
            {
                X = x,
                Y = y,
                Z = z,
                TotalDepth = DefaultTotalDepth,
                StepDepth = DefaultStepDepth,
                FeedZRapid = DefaultFeedZRapid,
                FeedZWork = DefaultFeedZWork,
                RetractHeight = DefaultRetractHeight
            };
        }

        private static void SetCommonDrillMetadata(DrillPointsOperation op, double totalDepth = DefaultTotalDepth,
            double stepDepth = DefaultStepDepth)
        {
            op.Metadata["TotalDepth"] = totalDepth;
            op.Metadata["StepDepth"] = stepDepth;
            op.Metadata["FeedZRapid"] = DefaultFeedZRapid;
            op.Metadata["FeedZWork"] = DefaultFeedZWork;
            op.Metadata["RetractHeight"] = DefaultRetractHeight;
        }

        // ---------------------------------------------------------------------
        // Сверление: 9 видов
        // ---------------------------------------------------------------------

        /// <summary>Точки вручную: 3 отверстия.</summary>
        public static DrillPointsOperation DrillPoints()
        {
            var op = new DrillPointsOperation();
            op.Holes.Add(Hole(10.0, 20.0));
            op.Holes.Add(Hole(30.0, 20.0));
            op.Holes.Add(Hole(20.0, 40.0));
            return op;
        }

        /// <summary>Линия: 5 отверстий с шагом 5 мм вдоль X.</summary>
        public static DrillPointsOperation DrillLine()
        {
            const double startX = 10.0, startY = 10.0, startZ = 0.0;
            const double distance = 5.0, angleDeg = 0.0;
            const int holeCount = 5;

            var op = new DrillPointsOperation();
            var angleRad = angleDeg * Math.PI / 180.0;
            var dx = distance * Math.Cos(angleRad);
            var dy = distance * Math.Sin(angleRad);
            for (int i = 0; i < holeCount; i++)
                op.Holes.Add(Hole(startX + dx * i, startY + dy * i, startZ));

            op.Metadata["StartX"] = startX;
            op.Metadata["StartY"] = startY;
            op.Metadata["StartZ"] = startZ;
            op.Metadata["Distance"] = distance;
            op.Metadata["HoleCount"] = holeCount;
            op.Metadata["AngleDeg"] = angleDeg;
            SetCommonDrillMetadata(op);
            return op;
        }

        /// <summary>Массив: 4×3 = 12 отверстий (шаг 5 мм, шаг ряда 5 мм).</summary>
        public static DrillPointsOperation DrillArray()
        {
            const double startX = 10.0, startY = 10.0, startZ = 0.0;
            const double distance = 5.0, angleDeg = 0.0;
            const int holeCount = 4;
            const double rowPitch = 5.0;
            const int rowCount = 3;

            var op = new DrillPointsOperation();
            var angleRad = angleDeg * Math.PI / 180.0;
            var dx = distance * Math.Cos(angleRad);
            var dy = distance * Math.Sin(angleRad);
            var px = -Math.Sin(angleRad) * rowPitch;
            var py = Math.Cos(angleRad) * rowPitch;
            for (int row = 0; row < rowCount; row++)
            {
                for (int col = 0; col < holeCount; col++)
                    op.Holes.Add(Hole(startX + dx * col + px * row, startY + dy * col + py * row, startZ));
            }

            op.Metadata["StartX"] = startX;
            op.Metadata["StartY"] = startY;
            op.Metadata["StartZ"] = startZ;
            op.Metadata["Distance"] = distance;
            op.Metadata["HoleCount"] = holeCount;
            op.Metadata["AngleDeg"] = angleDeg;
            op.Metadata["RowPitch"] = rowPitch;
            op.Metadata["RowCount"] = rowCount;
            SetCommonDrillMetadata(op);
            return op;
        }

        /// <summary>Контур прямоугольника: 4×3 с внутренними точками — 10 отверстий.</summary>
        public static DrillPointsOperation DrillRect()
        {
            const double startX = 10.0, startY = 10.0, startZ = 0.0;
            const double distance = 5.0, angleDeg = 0.0;
            const int holeCount = 4;
            const double rowPitch = 5.0;
            const int rowCount = 3;

            var op = new DrillPointsOperation();
            var angleRad = angleDeg * Math.PI / 180.0;
            var dx = distance * Math.Cos(angleRad);
            var dy = distance * Math.Sin(angleRad);
            var px = -Math.Sin(angleRad) * rowPitch;
            var py = Math.Cos(angleRad) * rowPitch;
            for (int row = 0; row < rowCount; row++)
            {
                for (int col = 0; col < holeCount; col++)
                {
                    var isBorderRow = row == 0 || row == rowCount - 1;
                    var isBorderCol = col == 0 || col == holeCount - 1;
                    if (!(isBorderRow || isBorderCol))
                        continue;
                    op.Holes.Add(Hole(startX + dx * col + px * row, startY + dy * col + py * row, startZ));
                }
            }

            op.Metadata["StartX"] = startX;
            op.Metadata["StartY"] = startY;
            op.Metadata["StartZ"] = startZ;
            op.Metadata["Distance"] = distance;
            op.Metadata["HoleCount"] = holeCount;
            op.Metadata["AngleDeg"] = angleDeg;
            op.Metadata["RowPitch"] = rowPitch;
            op.Metadata["RowCount"] = rowCount;
            SetCommonDrillMetadata(op);
            return op;
        }

        /// <summary>Окружность: 6 отверстий на R20.</summary>
        public static DrillPointsOperation DrillCircle()
        {
            const double centerX = 0.0, centerY = 0.0, z = 0.0;
            const double radius = 20.0;
            const int holeCount = 6;
            const double startAngleDeg = 0.0;

            var op = new DrillPointsOperation();
            var startRad = startAngleDeg * Math.PI / 180.0;
            var stepRad = 2 * Math.PI / holeCount;
            for (int i = 0; i < holeCount; i++)
            {
                var angle = startRad + stepRad * i;
                op.Holes.Add(Hole(centerX + radius * Math.Cos(angle), centerY + radius * Math.Sin(angle), z));
            }

            op.Metadata["CenterX"] = centerX;
            op.Metadata["CenterY"] = centerY;
            op.Metadata["Z"] = z;
            op.Metadata["Radius"] = radius;
            op.Metadata["HoleCount"] = holeCount;
            op.Metadata["StartAngleDeg"] = startAngleDeg;
            SetCommonDrillMetadata(op);
            return op;
        }

        /// <summary>Дуга: 5 отверстий от 0° до 180° на R20.</summary>
        public static DrillPointsOperation DrillArc()
        {
            const double centerX = 0.0, centerY = 0.0, z = 0.0;
            const double radius = 20.0;
            const int holeCount = 5;
            const double startAngleDeg = 0.0, endAngleDeg = 180.0;

            var op = new DrillPointsOperation();
            var startRad = startAngleDeg * Math.PI / 180.0;
            var endRad = endAngleDeg * Math.PI / 180.0;
            var arcSpan = endRad - startRad;
            while (arcSpan < 0) arcSpan += 2 * Math.PI;
            while (arcSpan > 2 * Math.PI) arcSpan -= 2 * Math.PI;
            if (arcSpan < 0.001)
                arcSpan = 2 * Math.PI;
            var stepRad = holeCount > 1 ? arcSpan / (holeCount - 1) : 0;
            for (int i = 0; i < holeCount; i++)
            {
                var angle = startRad + stepRad * i;
                op.Holes.Add(Hole(centerX + radius * Math.Cos(angle), centerY + radius * Math.Sin(angle), z));
            }

            op.Metadata["CenterX"] = centerX;
            op.Metadata["CenterY"] = centerY;
            op.Metadata["Z"] = z;
            op.Metadata["Radius"] = radius;
            op.Metadata["HoleCount"] = holeCount;
            op.Metadata["StartAngleDeg"] = startAngleDeg;
            op.Metadata["EndAngleDeg"] = endAngleDeg;
            SetCommonDrillMetadata(op);
            return op;
        }

        /// <summary>Полигон: квадрат R20, 3 отверстия на сторону — 12 отверстий.</summary>
        public static DrillPointsOperation DrillPolygon()
        {
            const double centerX = 0.0, centerY = 0.0, z = 0.0;
            const double radius = 20.0;
            const int numberOfSides = 4;
            const int holesPerSide = 3;
            const double rotationAngle = 0.0;

            var op = new DrillPointsOperation();
            var rotationRad = rotationAngle * Math.PI / 180.0;
            var angleStep = 2 * Math.PI / numberOfSides;

            var vertices = new List<(double x, double y)>();
            for (int i = 0; i < numberOfSides; i++)
            {
                var angle = i * angleStep + rotationRad;
                vertices.Add((centerX + radius * Math.Cos(angle), centerY + radius * Math.Sin(angle)));
            }

            for (int side = 0; side < numberOfSides; side++)
            {
                var startVertex = vertices[side];
                var endVertex = vertices[(side + 1) % numberOfSides];
                var dx = endVertex.x - startVertex.x;
                var dy = endVertex.y - startVertex.y;
                var stepX = holesPerSide > 1 ? dx / holesPerSide : 0;
                var stepY = holesPerSide > 1 ? dy / holesPerSide : 0;
                for (int holeIndex = 0; holeIndex < holesPerSide; holeIndex++)
                    op.Holes.Add(Hole(startVertex.x + stepX * holeIndex, startVertex.y + stepY * holeIndex, z));
            }

            op.Metadata["CenterX"] = centerX;
            op.Metadata["CenterY"] = centerY;
            op.Metadata["Z"] = z;
            op.Metadata["Radius"] = radius;
            op.Metadata["NumberOfSides"] = numberOfSides;
            op.Metadata["HolesPerSide"] = holesPerSide;
            op.Metadata["RotationAngle"] = rotationAngle;
            SetCommonDrillMetadata(op);
            return op;
        }

        /// <summary>Эллипс: 8 отверстий на эллипсе 25×15.</summary>
        public static DrillPointsOperation DrillEllipse()
        {
            const double centerX = 0.0, centerY = 0.0, z = 0.0;
            const double radiusX = 25.0, radiusY = 15.0;
            const double rotationAngle = 0.0;
            const int holeCount = 8;
            const double startAngleDeg = 0.0;

            var op = new DrillPointsOperation();
            var startRad = startAngleDeg * Math.PI / 180.0;
            var stepRad = 2 * Math.PI / holeCount;
            var rotationRad = rotationAngle * Math.PI / 180.0;
            var cosRot = Math.Cos(rotationRad);
            var sinRot = Math.Sin(rotationRad);
            for (int i = 0; i < holeCount; i++)
            {
                var angle = startRad + stepRad * i;
                var xEllipse = radiusX * Math.Cos(angle);
                var yEllipse = radiusY * Math.Sin(angle);
                op.Holes.Add(Hole(
                    centerX + xEllipse * cosRot - yEllipse * sinRot,
                    centerY + xEllipse * sinRot + yEllipse * cosRot, z));
            }

            op.Metadata["CenterX"] = centerX;
            op.Metadata["CenterY"] = centerY;
            op.Metadata["Z"] = z;
            op.Metadata["RadiusX"] = radiusX;
            op.Metadata["RadiusY"] = radiusY;
            op.Metadata["RotationAngle"] = rotationAngle;
            op.Metadata["HoleCount"] = holeCount;
            op.Metadata["StartAngleDeg"] = startAngleDeg;
            SetCommonDrillMetadata(op);
            return op;
        }

        /// <summary>Корпус SOIC-8: 2 ряда по 4 вывода — 8 отверстий.</summary>
        public static DrillPointsOperation DrillPackage()
        {
            const double centerX = 0.0, centerY = 0.0, z = 0.0;
            const double rotationAngle = 0.0;
            var package = new PackageDefinition("SOIC-8", 4, 1.27, 5.0);

            var op = new DrillPointsOperation();
            var angleRad = rotationAngle * Math.PI / 180.0;
            var cos = Math.Cos(angleRad);
            var sin = Math.Sin(angleRad);

            if (package.RowSpacing > 0)
            {
                var halfRowSpacing = package.RowSpacing / 2.0;
                var totalPinLength = (package.PinsPerRow - 1) * package.PinPitch;
                var halfPinLength = totalPinLength / 2.0;

                for (int i = 0; i < package.PinsPerRow; i++)
                {
                    var localX = -halfRowSpacing;
                    var localY = -halfPinLength + i * package.PinPitch;
                    op.Holes.Add(Hole(centerX + localX * cos - localY * sin, centerY + localX * sin + localY * cos, z));
                }
                for (int i = 0; i < package.PinsPerRow; i++)
                {
                    var localX = halfRowSpacing;
                    var localY = halfPinLength - i * package.PinPitch;
                    op.Holes.Add(Hole(centerX + localX * cos - localY * sin, centerY + localX * sin + localY * cos, z));
                }
            }
            else
            {
                var totalPinLength = (package.PinsPerRow - 1) * package.PinPitch;
                var halfPinLength = totalPinLength / 2.0;
                for (int i = 0; i < package.PinsPerRow; i++)
                {
                    var localX = 0.0;
                    var localY = -halfPinLength + i * package.PinPitch;
                    op.Holes.Add(Hole(centerX + localX * cos - localY * sin, centerY + localX * sin + localY * cos, z));
                }
            }

            op.Metadata["CenterX"] = centerX;
            op.Metadata["CenterY"] = centerY;
            op.Metadata["Z"] = z;
            op.Metadata["RotationAngle"] = rotationAngle;
            op.Metadata["PackageName"] = package.Name;
            SetCommonDrillMetadata(op);
            return op;
        }

        // ---------------------------------------------------------------------
        // Профили: 6 видов
        // ---------------------------------------------------------------------

        public static ProfileRectangleOperation ProfileRectangle()
        {
            return new ProfileRectangleOperation
            {
                Width = 40.0,
                Height = 20.0,
                ReferencePointX = 20.0,
                ReferencePointY = 20.0,
                ReferencePointType = ReferencePointType.Center,
                TotalDepth = DefaultMillingDepth,
                StepDepth = DefaultMillingStep,
                ToolDiameter = DefaultToolDiameter
            };
        }

        public static ProfileRoundedRectangleOperation ProfileRoundedRectangle()
        {
            return new ProfileRoundedRectangleOperation
            {
                Width = 40.0,
                Height = 20.0,
                RadiusTopLeft = 2.0,
                RadiusTopRight = 2.0,
                RadiusBottomLeft = 2.0,
                RadiusBottomRight = 2.0,
                ReferencePointX = 20.0,
                ReferencePointY = 20.0,
                ReferencePointType = ReferencePointType.Center,
                TotalDepth = DefaultMillingDepth,
                StepDepth = DefaultMillingStep,
                ToolDiameter = DefaultToolDiameter
            };
        }

        public static ProfileCircleOperation ProfileCircle()
        {
            return new ProfileCircleOperation
            {
                CenterX = 20.0,
                CenterY = 20.0,
                Radius = 10.0,
                TotalDepth = DefaultMillingDepth,
                StepDepth = DefaultMillingStep,
                ToolDiameter = DefaultToolDiameter
            };
        }

        public static ProfileEllipseOperation ProfileEllipse()
        {
            return new ProfileEllipseOperation
            {
                CenterX = 20.0,
                CenterY = 20.0,
                RadiusX = 15.0,
                RadiusY = 8.0,
                RotationAngle = 0.0,
                TotalDepth = DefaultMillingDepth,
                StepDepth = DefaultMillingStep,
                ToolDiameter = DefaultToolDiameter
            };
        }

        public static ProfilePolygonOperation ProfilePolygon()
        {
            return new ProfilePolygonOperation
            {
                CenterX = 20.0,
                CenterY = 20.0,
                NumberOfSides = 6,
                Radius = 10.0,
                RotationAngle = 0.0,
                TotalDepth = DefaultMillingDepth,
                StepDepth = DefaultMillingStep,
                ToolDiameter = DefaultToolDiameter
            };
        }

        /// <summary>DXF-профиль: контур «D» из Assets/profile_sample.dxf (3 LINE + 1 ARC).</summary>
        public static ProfileDxfOperation ProfileDxf()
        {
            const string assetName = "profile_sample.dxf";
            var op = new ProfileDxfOperation
            {
                DxfFilePath = DxfFixtureLoader.GetAssetPath(assetName),
                TotalDepth = DefaultMillingDepth,
                StepDepth = DefaultMillingStep,
                ToolDiameter = DefaultToolDiameter
            };
            op.Polylines = DxfFixtureLoader.LoadProfilePolylines(assetName);
            return op;
        }

        // ---------------------------------------------------------------------
        // Карманы: 4 вида
        // ---------------------------------------------------------------------

        public static PocketRectangleOperation PocketRectangle()
        {
            return new PocketRectangleOperation
            {
                Width = 40.0,
                Height = 20.0,
                ReferencePointX = 20.0,
                ReferencePointY = 20.0,
                ReferencePointType = ReferencePointType.Center,
                PocketStrategy = PocketStrategy.Spiral,
                TotalDepth = DefaultMillingDepth,
                StepDepth = DefaultMillingStep,
                ToolDiameter = DefaultToolDiameter,
                StepPercentOfTool = 40.0
            };
        }

        public static PocketCircleOperation PocketCircle()
        {
            return new PocketCircleOperation
            {
                CenterX = 20.0,
                CenterY = 20.0,
                Radius = 10.0,
                PocketStrategy = PocketStrategy.Spiral,
                TotalDepth = DefaultMillingDepth,
                StepDepth = DefaultMillingStep,
                ToolDiameter = DefaultToolDiameter,
                StepPercentOfTool = 40.0
            };
        }

        public static PocketEllipseOperation PocketEllipse()
        {
            return new PocketEllipseOperation
            {
                CenterX = 20.0,
                CenterY = 20.0,
                RadiusX = 15.0,
                RadiusY = 8.0,
                RotationAngle = 0.0,
                PocketStrategy = PocketStrategy.Spiral,
                TotalDepth = DefaultMillingDepth,
                StepDepth = DefaultMillingStep,
                ToolDiameter = DefaultToolDiameter,
                StepPercentOfTool = 40.0
            };
        }

        /// <summary>DXF-карман: 2 замкнутых контура (прямоугольники) из Assets/pocket_sample.dxf.</summary>
        public static PocketDxfOperation PocketDxf()
        {
            const string assetName = "pocket_sample.dxf";
            var op = new PocketDxfOperation
            {
                DxfFilePath = DxfFixtureLoader.GetAssetPath(assetName),
                PocketStrategy = PocketStrategy.Spiral,
                TotalDepth = DefaultMillingDepth,
                StepDepth = DefaultMillingStep,
                ToolDiameter = DefaultToolDiameter,
                StepPercentOfTool = 40.0
            };
            op.ClosedContours = DxfFixtureLoader.LoadPocketClosedContours(assetName);
            return op;
        }
    }
}
