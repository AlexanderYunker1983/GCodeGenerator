using System;
using System.Collections.Generic;
using System.Linq;
using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.Models;
using GCodeGenerator.Tests.Fixtures;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Тесты доменной валидации операций (пункт 3.7 плана).
    ///
    /// Ключевой тест — AllReferenceOperations_AreValid: все 19 эталонных
    /// операций (включая DXF, загруженные реальным парсером) должны быть
    /// валидными. Проверки консервативны: помечаются только физически
    /// невозможные значения, проекты, которые приложение способно
    /// сгенерировать, не должны получать замечаний.
    /// </summary>
    [TestClass]
    public class OperationValidationTests
    {
        // ------------------------------------------------------------------
        // Вспомогательные методы
        // ------------------------------------------------------------------

        private static void AssertValid(IValidatable op)
        {
            var issues = op.Validate();
            Assert.AreEqual(0, issues.Count, string.Join("; ", issues.Select(i => i.ToString())));
        }

        /// <summary>Утверждает ровно одно замечание с указанным именем свойства.</summary>
        private static void AssertSingleIssue(IValidatable op, string property)
        {
            var issues = op.Validate();
            Assert.AreEqual(1, issues.Count, string.Join("; ", issues.Select(i => i.ToString())));
            Assert.AreEqual(property, issues[0].Property);
            Assert.IsFalse(string.IsNullOrWhiteSpace(issues[0].Message));
        }

        private static Polyline2D Poly(params (double x, double y)[] pts)
        {
            var p = new Polyline2D();
            foreach (var pt in pts)
                p.Points.Add(new Point2D { X = pt.x, Y = pt.y });
            return p;
        }

        // ------------------------------------------------------------------
        // Валидные операции — замечаний нет
        // ------------------------------------------------------------------

        /// <summary>
        /// Все 19 операций эталонного проекта (9 сверл + 6 профилей + 4 кармана)
        /// валидны. Покрывает и DXF-фикстуры, загруженные реальным парсером.
        /// </summary>
        [TestMethod]
        public void AllReferenceOperations_AreValid()
        {
            var ops = ReferenceOperations.Build();
            Assert.AreEqual(19, ops.Count);
            foreach (var op in ops)
            {
                Assert.IsTrue(op is IValidatable, $"{op.GetType().Name} must implement IValidatable");
                AssertValid((IValidatable)op);
            }
        }

        /// <summary>
        /// Пустое PackageName допустимо: диалог подставляет шаблон по умолчанию (DIP8).
        /// </summary>
        [TestMethod]
        public void DrillPackage_EmptyPackageName_IsValid()
        {
            var op = DrillPointsOperation.CreateNew(DrillMode.Package);
            op.Holes.Add(new DrillHole { X = 0, Y = 0, TotalDepth = 2, StepDepth = 1 });
            AssertValid(op);
        }

        /// <summary>
        /// Открытая полилиния легальна для DXF-профиля (генератор поддерживает
        /// открытые контуры), замкнутость не требуется.
        /// </summary>
        [TestMethod]
        public void ProfileDxf_OpenPolyline_IsValid()
        {
            var op = OperationFixtures.ProfileDxf();
            op.Polylines = new List<Polyline2D> { Poly((0, 0), (10, 0), (10, 5)) };
            AssertValid(op);
        }

        /// <summary>
        /// Замкнутый контур, замкнутый с точностью до допустимого отклонения
        /// (5e-4 &lt; 1e-3 — допуск импортера DXF), не помечается как открытый.
        /// </summary>
        [TestMethod]
        public void PocketDxf_ClosedWithinTolerance_IsValid()
        {
            var op = OperationFixtures.PocketDxf();
            op.ClosedContours = new List<Polyline2D>
            {
                Poly((0, 0), (10, 0), (10, 10), (0, 10), (0, 5e-4))
            };
            AssertValid(op);
        }

        // ------------------------------------------------------------------
        // Неизвестные значения из файла: DrillMode и PackageName
        // ------------------------------------------------------------------

        /// <summary>
        /// Неизвестный способ расстановки — файл хранит перечисление числом
        /// и может принести любое — это замечание проверки, а не исключение
        /// из неё: прежде Validate() падал из шаблона, минуя список проблем,
        /// который обязан вернуть по контракту IValidatable.
        /// </summary>
        [TestMethod]
        public void Drill_UnknownDrillMode_ReportsIssueInsteadOfThrowing()
        {
            var op = DrillPointsOperation.CreateNew(DrillMode.Points);
            op.DrillMode = (DrillMode)99;

            AssertSingleIssue(op, nameof(op.DrillMode));
            Assert.IsTrue(op.HasErrors, "признак ошибок обязан работать, а не бросать");
            Assert.AreEqual(0, op.HolesToDrill.Count,
                "расстановка при неизвестном режиме безопасно пуста: её читает окно ещё до проверки");
            StringAssert.Contains(op.GetDescription(), "0 hole",
                "описание не должно падать — оно показывает пустую расстановку");
        }

        /// <summary>
        /// Неизвестный режим отклоняется и генерацией — общим отчётом
        /// об ошибках операций с именем поля, а не исключением шаблона
        /// без указания операции.
        /// </summary>
        [TestMethod]
        public void Generate_UnknownDrillMode_IsRejectedWithValidationReport()
        {
            var op = DrillPointsOperation.CreateNew(DrillMode.Points);
            op.DrillMode = (DrillMode)99;

            var failure = Assert.Throws<GCodeGenerationValidationException>(
                () => new SimpleGCodeGenerator().Generate(
                    new List<OperationBase> { op }, SettingsFixtures.Default()));

            Assert.AreEqual(1, failure.Failures.Count);
            Assert.AreEqual(nameof(op.DrillMode), failure.Failures[0].Issues.Single().Property);
        }

        /// <summary>
        /// Опечатка в имени корпуса — замечание на поле PackageName, а не
        /// тихая подмена корпусом по умолчанию: «DIP9» не должен молча
        /// сверлиться как DIP8.
        /// </summary>
        [TestMethod]
        public void DrillPackage_UnknownPackageName_IsRejected()
        {
            var op = DrillPointsOperation.CreateNew(DrillMode.Package);
            op.PackageName = "DIP9";

            AssertSingleIssue(op, nameof(op.PackageName));
        }

        /// <summary>
        /// Имя корпуса сравнивается без учёта регистра — как и подстановка
        /// шаблона: «dip8» из старого файла остаётся допустимым.
        /// </summary>
        [TestMethod]
        public void DrillPackage_KnownPackageNameInDifferentCase_IsValid()
        {
            var op = DrillPointsOperation.CreateNew(DrillMode.Package);
            op.PackageName = "dip8";

            AssertValid(op);
        }

        // ------------------------------------------------------------------
        // Сверление
        // ------------------------------------------------------------------

        [TestMethod]
        public void DrillPoints_NoHoles_Invalid()
        {
            var op = new DrillPointsOperation { DrillMode = DrillMode.Points };
            AssertSingleIssue(op, nameof(DrillPointsOperation.Holes));
        }

        [TestMethod]
        public void DrillPoints_ZeroStepDepthHole_Invalid()
        {
            var op = OperationFixtures.DrillPoints();
            op.Holes[1].StepDepth = 0;
            AssertSingleIssue(op, "Holes[1].StepDepth");
        }

        [TestMethod]
        public void DrillPoints_NegativeTotalDepthHole_Invalid()
        {
            var op = OperationFixtures.DrillPoints();
            op.Holes[0].TotalDepth = -1;
            AssertSingleIssue(op, "Holes[0].TotalDepth");
        }

        [TestMethod]
        public void DrillLine_ZeroHoleCount_Invalid()
        {
            var op = OperationFixtures.DrillLine();
            op.HoleCount = 0;
            AssertSingleIssue(op, nameof(DrillPointsOperation.HoleCount));
        }

        [TestMethod]
        public void DrillLine_ZeroDistance_Invalid()
        {
            var op = OperationFixtures.DrillLine();
            op.Distance = 0;
            AssertSingleIssue(op, nameof(DrillPointsOperation.Distance));
        }

        [TestMethod]
        public void DrillArray_ZeroRowCount_Invalid()
        {
            var op = OperationFixtures.DrillArray();
            op.RowCount = 0;
            AssertSingleIssue(op, nameof(DrillPointsOperation.RowCount));
        }

        [TestMethod]
        public void DrillArray_ZeroRowPitch_Invalid()
        {
            var op = OperationFixtures.DrillArray();
            op.RowPitch = 0;
            AssertSingleIssue(op, nameof(DrillPointsOperation.RowPitch));
        }

        [TestMethod]
        public void DrillRect_NegativeDistance_Invalid()
        {
            var op = OperationFixtures.DrillRect();
            op.Distance = -5;
            AssertSingleIssue(op, nameof(DrillPointsOperation.Distance));
        }

        [TestMethod]
        public void DrillCircle_ZeroRadius_Invalid()
        {
            var op = OperationFixtures.DrillCircle();
            op.Radius = 0;
            AssertSingleIssue(op, nameof(DrillPointsOperation.Radius));
        }

        [TestMethod]
        public void DrillArc_ZeroHoleCount_Invalid()
        {
            var op = OperationFixtures.DrillArc();
            op.HoleCount = 0;
            AssertSingleIssue(op, nameof(DrillPointsOperation.HoleCount));
        }

        [TestMethod]
        public void DrillPolygon_TwoSides_Invalid()
        {
            var op = OperationFixtures.DrillPolygon();
            op.NumberOfSides = 2;
            AssertSingleIssue(op, nameof(DrillPointsOperation.NumberOfSides));
        }

        [TestMethod]
        public void DrillPolygon_ZeroHolesPerSide_Invalid()
        {
            var op = OperationFixtures.DrillPolygon();
            op.HolesPerSide = 0;
            AssertSingleIssue(op, nameof(DrillPointsOperation.HolesPerSide));
        }

        [TestMethod]
        public void DrillEllipse_NegativeRadiusY_Invalid()
        {
            var op = OperationFixtures.DrillEllipse();
            op.RadiusY = -1;
            AssertSingleIssue(op, nameof(DrillPointsOperation.RadiusY));
        }

        /// <summary>
        /// Подачи и высота отвода проверяются у сверления так же, как у
        /// фрезеровки: правила резания общие, и раньше половина из них до
        /// сверления не доходила — рабочая подача в плоскости, обе подачи по
        /// оси и отрицательный отвод принимались молча.
        /// </summary>
        [TestMethod]
        public void Drill_CuttingParameters_AreValidatedLikeMilling()
        {
            AssertSingleIssue(WithFeed(op => op.FeedXYWork = 0), nameof(DrillPointsOperation.FeedXYWork));
            AssertSingleIssue(WithFeed(op => op.FeedZWork = 0), nameof(DrillPointsOperation.FeedZWork));
            AssertSingleIssue(WithFeed(op => op.FeedZRapid = -1), nameof(DrillPointsOperation.FeedZRapid));
            AssertSingleIssue(WithFeed(op => op.RetractHeight = -0.5), nameof(DrillPointsOperation.RetractHeight));
        }

        /// <summary>
        /// Сверление по шаблону с изменённым параметром резания: отверстия
        /// строит шаблон, поэтому их собственные значения остаются верными
        /// и в список проблем попадает ровно одно — то, что изменено.
        /// </summary>
        private static DrillPointsOperation WithFeed(Action<DrillPointsOperation> change)
        {
            var operation = OperationFixtures.DrillLine();
            change(operation);
            return operation;
        }

        [TestMethod]
        public void DrillPattern_ZeroTotalDepth_Invalid()
        {
            var op = OperationFixtures.DrillLine();
            op.TotalDepth = 0;
            AssertSingleIssue(op, nameof(DrillPointsOperation.TotalDepth));
        }

        [TestMethod]
        public void DrillPattern_ZeroStepDepth_Invalid()
        {
            var op = OperationFixtures.DrillCircle();
            op.StepDepth = 0;
            AssertSingleIssue(op, nameof(DrillPointsOperation.StepDepth));
        }

        // ------------------------------------------------------------------
        // Профили
        // ------------------------------------------------------------------

        [TestMethod]
        public void ProfileCircle_ZeroRadius_Invalid()
        {
            var op = OperationFixtures.ProfileCircle();
            op.Radius = 0;
            AssertSingleIssue(op, nameof(ProfileCircleOperation.Radius));
        }

        [TestMethod]
        public void ProfileEllipse_NegativeRadiusX_Invalid()
        {
            var op = OperationFixtures.ProfileEllipse();
            op.RadiusX = -1;
            AssertSingleIssue(op, nameof(ProfileEllipseOperation.RadiusX));
        }

        [TestMethod]
        public void ProfilePolygon_TwoSides_Invalid()
        {
            var op = OperationFixtures.ProfilePolygon();
            op.NumberOfSides = 2;
            AssertSingleIssue(op, nameof(ProfilePolygonOperation.NumberOfSides));
        }

        [TestMethod]
        public void ProfileRectangle_ZeroWidth_Invalid()
        {
            var op = OperationFixtures.ProfileRectangle();
            op.Width = 0;
            AssertSingleIssue(op, nameof(ProfileRectangleOperation.Width));
        }

        [TestMethod]
        public void ProfileRoundedRectangle_NegativeHeight_Invalid()
        {
            var op = OperationFixtures.ProfileRoundedRectangle();
            op.Height = -5;
            AssertSingleIssue(op, nameof(ProfileRoundedRectangleOperation.Height));
        }

        [TestMethod]
        public void ProfileRoundedRectangle_OversizedCornerRadii_AreNotFlagged()
        {
            // Геометрия склюпает радиусы, поэтому такие значения допустимы.
            var op = OperationFixtures.ProfileRoundedRectangle();
            op.RadiusTopLeft = 100;
            op.RadiusBottomRight = -3;
            AssertValid(op);
        }

        [TestMethod]
        public void Profile_ZeroStepDepth_Invalid()
        {
            var op = OperationFixtures.ProfileCircle();
            op.StepDepth = 0;
            AssertSingleIssue(op, nameof(ProfileCircleOperation.StepDepth));
        }

        [TestMethod]
        public void Profile_NegativeToolDiameter_Invalid()
        {
            var op = OperationFixtures.ProfileCircle();
            op.ToolDiameter = -1;
            AssertSingleIssue(op, nameof(ProfileCircleOperation.ToolDiameter));
        }

        [TestMethod]
        public void Profile_ZeroTotalDepth_Invalid()
        {
            var op = OperationFixtures.ProfileCircle();
            op.TotalDepth = 0;
            AssertSingleIssue(op, nameof(ProfileCircleOperation.TotalDepth));
        }

        [TestMethod]
        public void ProfileDxf_EmptyPolylines_Invalid()
        {
            var op = OperationFixtures.ProfileDxf();
            op.Polylines = new List<Polyline2D>();
            AssertSingleIssue(op, nameof(ProfileDxfOperation.Polylines));
        }

        [TestMethod]
        public void ProfileDxf_SinglePointPolyline_Invalid()
        {
            var op = OperationFixtures.ProfileDxf();
            op.Polylines = new List<Polyline2D> { Poly((0, 0)) };
            AssertSingleIssue(op, "Polylines[0].Points");
        }

        // ------------------------------------------------------------------
        // Карманы
        // ------------------------------------------------------------------

        [TestMethod]
        public void PocketCircle_ZeroRadius_Invalid()
        {
            var op = OperationFixtures.PocketCircle();
            op.Radius = 0;
            AssertSingleIssue(op, nameof(PocketCircleOperation.Radius));
        }

        [TestMethod]
        public void PocketEllipse_ZeroRadiusY_Invalid()
        {
            var op = OperationFixtures.PocketEllipse();
            op.RadiusY = 0;
            AssertSingleIssue(op, nameof(PocketEllipseOperation.RadiusY));
        }

        [TestMethod]
        public void PocketRectangle_NegativeWidth_Invalid()
        {
            var op = OperationFixtures.PocketRectangle();
            op.Width = -1;
            AssertSingleIssue(op, nameof(PocketRectangleOperation.Width));
        }

        [TestMethod]
        public void Pocket_ZeroStepDepth_Invalid()
        {
            var op = OperationFixtures.PocketCircle();
            op.StepDepth = 0;
            AssertSingleIssue(op, nameof(PocketCircleOperation.StepDepth));
        }

        [TestMethod]
        public void Pocket_ZeroToolDiameter_Invalid()
        {
            var op = OperationFixtures.PocketCircle();
            op.ToolDiameter = 0;
            AssertSingleIssue(op, nameof(PocketCircleOperation.ToolDiameter));
        }

        [TestMethod]
        public void PocketDxf_EmptyContours_Invalid()
        {
            var op = OperationFixtures.PocketDxf();
            op.ClosedContours = new List<Polyline2D>();
            AssertSingleIssue(op, nameof(PocketDxfOperation.ClosedContours));
        }

        [TestMethod]
        public void PocketDxf_TwoPointContour_Invalid()
        {
            var op = OperationFixtures.PocketDxf();
            op.ClosedContours = new List<Polyline2D> { Poly((0, 0), (10, 0)) };
            AssertSingleIssue(op, "ClosedContours[0].Points");
        }

        [TestMethod]
        public void PocketDxf_OpenContour_Invalid()
        {
            var op = OperationFixtures.PocketDxf();
            op.ClosedContours = new List<Polyline2D>
            {
                Poly((0, 0), (10, 0), (10, 10), (0, 9))
            };
            AssertSingleIssue(op, "ClosedContours[0]");
        }

        [TestMethod]
        public void PocketDxf_ClosedContour_IsValid()
        {
            var op = OperationFixtures.PocketDxf();
            op.ClosedContours = new List<Polyline2D>
            {
                Poly((0, 0), (10, 0), (10, 10), (0, 10), (0, 0))
            };
            AssertValid(op);
        }
    }
}
