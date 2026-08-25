using GCodeGenerator.Models;

namespace GCodeGenerator.Tests.Fixtures
{
    /// <summary>
    /// Фабрики операций для фикстур.
    /// Формулы построения отверстий сверления повторяют RebuildHoles() во ViewModels/Drill/*;
    /// параметры паттерна задаются типизированными свойствами + DrillMode (пункт 3.1 плана),
    /// значения — те же, что раньше фикстуры записывали в Metadata.
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

        private static void SetCommonDrillZParams(DrillPointsOperation op, double totalDepth = DefaultTotalDepth,
            double stepDepth = DefaultStepDepth)
        {
            op.TotalDepth = totalDepth;
            op.StepDepth = stepDepth;
            op.FeedZRapid = DefaultFeedZRapid;
            op.FeedZWork = DefaultFeedZWork;
            op.RetractHeight = DefaultRetractHeight;
        }

        // ---------------------------------------------------------------------
        // Сверление: 9 видов
        //
        // Отверстия строит DrillPatternBuilder ядра: фикстура задаёт только
        // параметры шаблона. Прежде формулы здесь повторялись, то есть тест
        // сверял продукт с собственной копией вычислений.
        // ---------------------------------------------------------------------

        /// <summary>
        /// Операция шаблона как есть: отверстия она вычисляет сама по своим
        /// параметрам, дозаполнять их больше не требуется.
        /// </summary>
        private static DrillPointsOperation WithPatternHoles(DrillPointsOperation op) => op;

        /// <summary>Точки вручную: 3 отверстия.</summary>
        public static DrillPointsOperation DrillPoints()
        {
            var op = new DrillPointsOperation { DrillMode = DrillMode.Points };
            op.Holes.Add(Hole(10.0, 20.0));
            op.Holes.Add(Hole(30.0, 20.0));
            op.Holes.Add(Hole(20.0, 40.0));
            SetCommonDrillZParams(op);
            return op;
        }

        /// <summary>Линия: 5 отверстий с шагом 5 мм вдоль X.</summary>
        public static DrillPointsOperation DrillLine()
        {
            var op = new DrillPointsOperation
            {
                DrillMode = DrillMode.Line,
                StartX = 10.0,
                StartY = 10.0,
                StartZ = 0.0,
                Distance = 5.0,
                HoleCount = 5,
                AngleDeg = 0.0
            };
            SetCommonDrillZParams(op);
            return WithPatternHoles(op);
        }

        /// <summary>Массив: 4×3 = 12 отверстий (шаг 5 мм, шаг ряда 5 мм).</summary>
        public static DrillPointsOperation DrillArray()
        {
            var op = new DrillPointsOperation
            {
                DrillMode = DrillMode.Array,
                StartX = 10.0,
                StartY = 10.0,
                StartZ = 0.0,
                Distance = 5.0,
                HoleCount = 4,
                AngleDeg = 0.0,
                RowPitch = 5.0,
                RowCount = 3
            };
            SetCommonDrillZParams(op);
            return WithPatternHoles(op);
        }

        /// <summary>Прямоугольник: периметр сетки 4×3 — 10 отверстий.</summary>
        public static DrillPointsOperation DrillRect()
        {
            var op = new DrillPointsOperation
            {
                DrillMode = DrillMode.Rect,
                StartX = 10.0,
                StartY = 10.0,
                StartZ = 0.0,
                Distance = 5.0,
                HoleCount = 4,
                AngleDeg = 0.0,
                RowPitch = 5.0,
                RowCount = 3
            };
            SetCommonDrillZParams(op);
            return WithPatternHoles(op);
        }

        /// <summary>Окружность: 6 отверстий на R20.</summary>
        public static DrillPointsOperation DrillCircle()
        {
            var op = new DrillPointsOperation
            {
                DrillMode = DrillMode.Circle,
                CenterX = 0.0,
                CenterY = 0.0,
                Z = 0.0,
                Radius = 20.0,
                HoleCount = 6,
                StartAngleDeg = 0.0
            };
            SetCommonDrillZParams(op);
            return WithPatternHoles(op);
        }

        /// <summary>Дуга: 5 отверстий от 0° до 180° на R20.</summary>
        public static DrillPointsOperation DrillArc()
        {
            var op = new DrillPointsOperation
            {
                DrillMode = DrillMode.Arc,
                CenterX = 0.0,
                CenterY = 0.0,
                Z = 0.0,
                Radius = 20.0,
                HoleCount = 5,
                StartAngleDeg = 0.0,
                EndAngleDeg = 180.0
            };
            SetCommonDrillZParams(op);
            return WithPatternHoles(op);
        }

        /// <summary>Многоугольник: квадрат R20, по 3 отверстия на сторону — 12 отверстий.</summary>
        public static DrillPointsOperation DrillPolygon()
        {
            var op = new DrillPointsOperation
            {
                DrillMode = DrillMode.Polygon,
                CenterX = 0.0,
                CenterY = 0.0,
                Z = 0.0,
                Radius = 20.0,
                NumberOfSides = 4,
                HolesPerSide = 3,
                RotationAngle = 0.0
            };
            SetCommonDrillZParams(op);
            return WithPatternHoles(op);
        }

        /// <summary>Эллипс: 8 отверстий на эллипсе 25×15.</summary>
        public static DrillPointsOperation DrillEllipse()
        {
            var op = new DrillPointsOperation
            {
                DrillMode = DrillMode.Ellipse,
                CenterX = 0.0,
                CenterY = 0.0,
                Z = 0.0,
                RadiusX = 25.0,
                RadiusY = 15.0,
                RotationAngle = 0.0,
                HoleCount = 8,
                StartAngleDeg = 0.0
            };
            SetCommonDrillZParams(op);
            return WithPatternHoles(op);
        }

        /// <summary>Корпус SOIC-8: 2 ряда по 4 вывода — 8 отверстий.</summary>
        public static DrillPointsOperation DrillPackage()
        {
            var op = new DrillPointsOperation
            {
                DrillMode = DrillMode.Package,
                CenterX = 0.0,
                CenterY = 0.0,
                Z = 0.0,
                RotationAngle = 0.0,
                PackageName = "SOIC-8"
            };
            SetCommonDrillZParams(op);
            return WithPatternHoles(op);
        }

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

        /// <summary>
        /// Контур с наклонным врезанием: угол мал, а глубина велика, поэтому
        /// рампа не укладывается в один оборот и идёт несколькими витками
        /// с отводом на безопасное расстояние между проходами.
        /// </summary>
        public static ProfileCircleOperation ProfileCircleAngledEntry()
        {
            var operation = ProfileCircle();
            operation.EntryMode = EntryMode.Angled;
            operation.EntryAngle = 1.0;
            operation.SafeDistanceBetweenPasses = 0.8;
            return operation;
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
