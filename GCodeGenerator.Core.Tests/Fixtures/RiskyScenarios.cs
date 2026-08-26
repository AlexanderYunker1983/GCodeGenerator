using GCodeGenerator.Models;

namespace GCodeGenerator.Tests.Fixtures
{
    /// <summary>
    /// Операции рискованной логики: остановки по уклону и отсечке, «песочные
    /// часы», спирали на краях диапазона, разбиение окружности профиля.
    ///
    /// Параметры разделяют два потребителя: RiskyLogicTests проверяет ими
    /// счётчики и инварианты — быстрый сигнал «что-то изменилось», — а
    /// golden-эталоны каталога фикстур фиксируют программы целиком и
    /// показывают, что именно изменилось. Прежде параметры жили только в
    /// тестах, и точная траектория этих сценариев не фиксировалась нигде.
    /// </summary>
    public static class RiskyScenarios
    {
        /// <summary>Круг R6, фреза 3, уклон 45°: уклон съедает контур до полной глубины.</summary>
        public static PocketCircleOperation TaperCircle() => new PocketCircleOperation
        {
            Name = "Taper circle",
            CenterX = 0, CenterY = 0, Radius = 6,
            ToolDiameter = 3, TotalDepth = 10, StepDepth = 1, WallTaperAngleDeg = 45,
        };

        /// <summary>Квадрат 7.5, фреза 3, уклон 45°: остановка по росту площади оффсета.</summary>
        public static PocketRectangleOperation TaperRectangle() => new PocketRectangleOperation
        {
            Name = "Taper rectangle",
            Width = 7.5, Height = 7.5, ReferencePointX = 0, ReferencePointY = 0,
            ToolDiameter = 3, TotalDepth = 10, StepDepth = 1, WallTaperAngleDeg = 45,
        };

        /// <summary>Круг R1.6, фреза 3: эффективный диаметр чуть выше порога отсечки.</summary>
        public static PocketCircleOperation CircleAboveCutoff() => new PocketCircleOperation
        {
            Name = "Circle above cutoff",
            CenterX = 0, CenterY = 0, Radius = 1.6,
            ToolDiameter = 3, TotalDepth = 2, StepDepth = 1, WallTaperAngleDeg = 0,
        };

        /// <summary>Круг R1.55, фреза 3: эффективный диаметр ниже порога, ни одного хода.</summary>
        public static PocketCircleOperation CircleBelowCutoff() => new PocketCircleOperation
        {
            Name = "Circle below cutoff",
            CenterX = 0, CenterY = 0, Radius = 1.55,
            ToolDiameter = 3, TotalDepth = 2, StepDepth = 1, WallTaperAngleDeg = 0,
        };

        /// <summary>Круг R50 без уклона: полная глубина (в golden не входит — 23 тысячи ходов).</summary>
        public static PocketCircleOperation FullDepthCircle() => new PocketCircleOperation
        {
            Name = "Full-depth circle",
            CenterX = 0, CenterY = 0, Radius = 50,
            ToolDiameter = 3, TotalDepth = 4, StepDepth = 1, WallTaperAngleDeg = 0,
        };

        /// <summary>Узкая трапеция, уклон 10°: фрезеруется на полную глубину.</summary>
        public static PocketDxfOperation Trapezoid()
        {
            var op = new PocketDxfOperation
            {
                Name = "Trapezoid", ToolDiameter = 3, TotalDepth = 5, StepDepth = 1, WallTaperAngleDeg = 10,
            };
            op.ClosedContours.Add(Poly((0, 0), (12, 0), (8, 6), (4, 6), (0, 0)));
            return op;
        }

        /// <summary>Большой контур 40×20 и крошечный 2×2: крошечный пропускается.</summary>
        public static PocketDxfOperation MultiContour()
        {
            var op = new PocketDxfOperation
            {
                Name = "Multi contour", ToolDiameter = 3, TotalDepth = 4, StepDepth = 1, WallTaperAngleDeg = 0,
            };
            op.ClosedContours.Add(Poly((0, 0), (40, 0), (40, 20), (0, 20), (0, 0)));
            op.ClosedContours.Add(Poly((60, 0), (62, 0), (62, 2), (60, 2), (60, 0)));
            return op;
        }

        /// <summary>Только крошечный контур 2×2: отсечка без фантомной фрезеровки.</summary>
        public static PocketDxfOperation TinyContour()
        {
            var op = new PocketDxfOperation
            {
                Name = "Tiny contour", ToolDiameter = 3, TotalDepth = 4, StepDepth = 1, WallTaperAngleDeg = 0,
            };
            op.ClosedContours.Add(Poly((60, 0), (62, 0), (62, 2), (60, 2), (60, 0)));
            return op;
        }

        /// <summary>DXF-квадрат 7.5, уклон 45°: остановка на третьем слое.</summary>
        public static PocketDxfOperation SquareTaper45()
        {
            var op = new PocketDxfOperation
            {
                Name = "Square taper", ToolDiameter = 3, TotalDepth = 10, StepDepth = 1, WallTaperAngleDeg = 45,
            };
            op.ClosedContours.Add(Poly((0, 0), (7.5, 0), (7.5, 7.5), (0, 7.5), (0, 0)));
            return op;
        }

        /// <summary>«Песочные часы» с уклоном 15°: обе половины, пока уклон их не закроет.</summary>
        public static PocketDxfOperation HourglassTaper15()
        {
            var op = new PocketDxfOperation
            {
                Name = "Hourglass taper", ToolDiameter = 3, TotalDepth = 6, StepDepth = 1, WallTaperAngleDeg = 15,
            };
            op.ClosedContours.Add(HourglassContour());
            return op;
        }

        /// <summary>«Песочные часы» без уклона: обе половины на полную глубину.</summary>
        public static PocketDxfOperation HourglassFlat()
        {
            var op = new PocketDxfOperation
            {
                Name = "Hourglass flat", ToolDiameter = 3, TotalDepth = 6, StepDepth = 1, WallTaperAngleDeg = 0,
            };
            op.ClosedContours.Add(HourglassContour());
            return op;
        }

        /// <summary>П-образный контур, уклон 10°: стенки смыкаются на третьем слое.</summary>
        public static PocketDxfOperation UShape()
        {
            var op = new PocketDxfOperation
            {
                Name = "U-shape", ToolDiameter = 3, TotalDepth = 6, StepDepth = 1, WallTaperAngleDeg = 10,
            };
            op.ClosedContours.Add(Poly((0, 0), (12, 0), (12, 10), (8, 10), (8, 4), (4, 4), (4, 10), (0, 10), (0, 0)));
            return op;
        }

        /// <summary>Спираль с шагом 10% от фрезы: много витков, граница по смещённому контуру.</summary>
        public static PocketCircleOperation SpiralFineStep() => new PocketCircleOperation
        {
            Name = "Spiral fine step",
            CenterX = 0, CenterY = 0, Radius = 10,
            ToolDiameter = 3, TotalDepth = 1, StepDepth = 1, StepPercentOfTool = 10,
        };

        /// <summary>Спираль в малом контуре R4: граница по смещённому контуру.</summary>
        public static PocketCircleOperation SpiralSmallContour() => new PocketCircleOperation
        {
            Name = "Spiral small contour",
            CenterX = 0, CenterY = 0, Radius = 4,
            ToolDiameter = 3, TotalDepth = 1, StepDepth = 1, StepPercentOfTool = 40,
        };

        /// <summary>Окружность профиля с мелким разбиением (0.5 мм): полилиния или пара дуг G2.</summary>
        public static ProfileCircleOperation CircleProfileFine() => new ProfileCircleOperation
        {
            Name = "Circle profile fine",
            CenterX = 0, CenterY = 0, Radius = 10,
            ToolDiameter = 3, TotalDepth = 2, StepDepth = 1, MaxSegmentLength = 0.5,
        };

        /// <summary>Окружность профиля с крупным разбиением (2 мм): вдвое короче полилиния.</summary>
        public static ProfileCircleOperation CircleProfileCoarse() => new ProfileCircleOperation
        {
            Name = "Circle profile coarse",
            CenterX = 0, CenterY = 0, Radius = 10,
            ToolDiameter = 3, TotalDepth = 2, StepDepth = 1, MaxSegmentLength = 2,
        };

        private static Polyline2D HourglassContour()
            => Poly((0, 0), (10, 0), (6, 4), (10, 8), (0, 8), (4, 4), (0, 0));

        private static Polyline2D Poly(params (double x, double y)[] pts)
        {
            var p = new Polyline2D();
            foreach (var pt in pts)
                p.Points.Add(new Point2D { X = pt.x, Y = pt.y });
            return p;
        }
    }
}
