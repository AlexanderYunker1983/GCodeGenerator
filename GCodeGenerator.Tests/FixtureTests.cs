using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.Models;
using GCodeGenerator.Tests.Fixtures;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Тесты фикстур операций (пункт 0.3 плана):
    /// фикстуры строятся, DXF-ассеты парсятся реальными парсерами продукта,
    /// варианты настроек дают ожидаемые отличия в G-коде.
    /// </summary>
    [TestClass]
    public class FixtureTests
    {
        private static readonly SimpleGCodeGenerator Generator = new SimpleGCodeGenerator();

        [TestMethod]
        public void Every_FixtureCase_Builds_GCodeProgram_EndingWithM30()
        {
            foreach (var fixture in FixtureCatalog.All)
            {
                var program = Generator.Generate(fixture.Operations, fixture.Settings);
                Assert.IsTrue(program.Lines.Count > 0, $"Пустая программа для фикстуры {fixture.Name}");
                var lastLine = Regex.Replace(program.Lines[program.Lines.Count - 1], @"^N\d+ ", "");
                Assert.AreEqual("M30", lastLine,
                    $"Финальная строка программы фикстуры {fixture.Name} должна быть M30");
            }
        }

        [TestMethod]
        public void Drill_Fixtures_Produce_Expected_Hole_Counts()
        {
            var expected = new Dictionary<Func<DrillPointsOperation>, int>
            {
                { OperationFixtures.DrillPoints, 3 },
                { OperationFixtures.DrillLine, 5 },
                { OperationFixtures.DrillArray, 12 },
                { OperationFixtures.DrillRect, 10 },
                { OperationFixtures.DrillCircle, 6 },
                { OperationFixtures.DrillArc, 5 },
                { OperationFixtures.DrillPolygon, 12 },
                { OperationFixtures.DrillEllipse, 8 },
                { OperationFixtures.DrillPackage, 8 }
            };

            foreach (var pair in expected)
            {
                var op = pair.Key();
                Assert.AreEqual(pair.Value, op.Holes.Count,
                    $"Неверное число отверстий для {op.Name}: ожидалось {pair.Value}, фактически {op.Holes.Count}");
            }
        }

        [TestMethod]
        public void Profile_Dxf_Asset_Parses_Expected_Polylines()
        {
            // profile_sample.dxf: «D»-контур = 3 LINE + 1 ARC (270°→90°, 180° → 16 сегментов → 17 точек).
            var polylines = DxfFixtureLoader.LoadProfilePolylines("profile_sample.dxf");

            Assert.AreEqual(4, polylines.Count, "Ожидалось 4 полилинии (3 LINE + 1 ARC)");
            CollectionAssert.AreEqual(new[] { 2, 17, 2, 2 },
                polylines.Select(p => p.Points.Count).ToArray(), "Неверное число точек в полилиниях");

            var first = polylines[0];
            Assert.AreEqual(0.0, first.Points[0].X, 1e-9);
            Assert.AreEqual(0.0, first.Points[0].Y, 1e-9);
            Assert.AreEqual(30.0, first.Points[1].X, 1e-9);
            Assert.AreEqual(0.0, first.Points[1].Y, 1e-9);

            var arc = polylines[1];
            Assert.AreEqual(30.0, arc.Points[0].X, 1e-6);
            Assert.AreEqual(0.0, arc.Points[0].Y, 1e-6);
            Assert.AreEqual(30.0, arc.Points[arc.Points.Count - 1].X, 1e-6);
            Assert.AreEqual(20.0, arc.Points[arc.Points.Count - 1].Y, 1e-6);
        }

        [TestMethod]
        public void Pocket_Dxf_Asset_Parses_Expected_ClosedContours()
        {
            // pocket_sample.dxf: прямоугольник 40×20 (S=800) и прямоугольник 12×12 (S=144),
            // оба из LINE-сущностей; парсер соединяет линии в замкнутые контуры (5 точек, первая == последней).
            var contours = DxfFixtureLoader.LoadPocketClosedContours("pocket_sample.dxf");

            Assert.AreEqual(2, contours.Count, "Ожидалось 2 замкнутых контура");

            foreach (var contour in contours)
            {
                Assert.AreEqual(5, contour.Points.Count, "Контур из 4 линий должен иметь 5 точек (с замыканием)");
                var first = contour.Points[0];
                var last = contour.Points[contour.Points.Count - 1];
                var d = Math.Sqrt(Math.Pow(first.X - last.X, 2) + Math.Pow(first.Y - last.Y, 2));
                Assert.IsTrue(d <= 0.001, "Контур должен быть замкнутым (первая == последняя точка)");
            }

            var areas = contours.Select(GetArea).OrderBy(a => a).ToArray();
            Assert.AreEqual(144.0, areas[0], 1e-6, "Площадь малого прямоугольника");
            Assert.AreEqual(800.0, areas[1], 1e-6, "Площадь большого прямоугольника");
        }

        [TestMethod]
        public void Dxf_Fixture_Operations_Carry_Asset_Geometry()
        {
            var profile = OperationFixtures.ProfileDxf();
            Assert.AreEqual(4, profile.Polylines.Count, "ProfileDxf должен нести 4 полилинии из DXF");
            Assert.IsTrue(profile.Polylines.Sum(p => p.Points.Count) > 0);

            var pocket = OperationFixtures.PocketDxf();
            Assert.AreEqual(2, pocket.ClosedContours.Count, "PocketDxf должен нести 2 замкнутых контура из DXF");
        }

        [TestMethod]
        public void Dxf_Fixture_Programs_Contain_Substantial_Moves()
        {
            // Проверка валидности фикстур: DXF-операции должны давать не пустую траекторию.
            foreach (var name in new[] { "Profile.Dxf.Default", "Pocket.Dxf.Default", "Pocket.Dxf.ArcsOff" })
            {
                var fixture = FixtureCatalog.All.First(f => f.Name == name);
                var program = Generator.Generate(fixture.Operations, fixture.Settings);
                var moves = program.Lines.Count(l => Regex.IsMatch(l, @"(^N\d+ )?(G0|G1|G2|G3)( |$)"));
                Assert.IsTrue(moves > 10, $"Фикстура {name} должна генерировать траекторию, фактически moves={moves}");
            }
        }

        [TestMethod]
        public void Settings_NoLineNumbers_ProducesNoLineNumbers()
        {
            var fixture = FixtureCatalog.All.First(f => f.Name == "Drill.Points.NoLineNumbers");
            var program = Generator.Generate(fixture.Operations, fixture.Settings);
            Assert.IsFalse(program.Lines.Any(l => Regex.IsMatch(l, @"^N\d+ ")),
                "При UseLineNumbers=false строки не должны начинаться с N<номер>");
        }

        [TestMethod]
        public void Settings_PaddedGCodes_ProducesPaddedGAndM()
        {
            var fixture = FixtureCatalog.All.First(f => f.Name == "Drill.Points.PaddedGCodes");
            var program = Generator.Generate(fixture.Operations, fixture.Settings);
            Assert.IsTrue(program.Lines.Any(l => l.Contains("G01") || l.Contains("G00")),
                "Ожидаются G01/G00 при UsePaddedGCodes=true");
            Assert.IsTrue(program.Lines.Any(l => Regex.IsMatch(l, @"^(N\d+ )?M03 S\d+$")),
                "Команда шпинделя должна присутствовать (M03 с S-кодом)");
            Assert.AreEqual("M30", Regex.Replace(program.Lines[program.Lines.Count - 1], @"^N\d+ ", ""),
                "Финальная строка должна остаться M30");
        }

        [TestMethod]
        public void Settings_SpindleCoolantOff_OmitsSpindleAndCoolantCommands()
        {
            var fixture = FixtureCatalog.All.First(f => f.Name == "Drill.Points.SpindleCoolantOff");
            var program = Generator.Generate(fixture.Operations, fixture.Settings);
            Assert.IsFalse(program.Lines.Any(l => Regex.IsMatch(l, @"^N\d+ M[34589]( |$)")),
                "При SpindleControlEnabled=false и CoolantControlEnabled=false не должно быть M3/M4/M5/M8/M9");
        }

        [TestMethod]
        public void Settings_WcsG55_EmitsG55AtProgramStart()
        {
            var fixture = FixtureCatalog.All.First(f => f.Name == "Multi.Operation.WcsG55");
            var program = Generator.Generate(fixture.Operations, fixture.Settings);
            var wcsLine = program.Lines.FirstOrDefault(l =>
                Regex.IsMatch(l, @"^N\d+ G55$") || l == "G55");
            Assert.IsNotNull(wcsLine, "Ожидалась строка G55 в начале программы");
        }

        [TestMethod]
        public void Settings_G92StartEnd_EmitsG92AndEndMove()
        {
            var fixture = FixtureCatalog.All.First(f => f.Name == "Multi.Operation.G92StartEnd");
            var program = Generator.Generate(fixture.Operations, fixture.Settings);

            var g92 = program.Lines.FirstOrDefault(l => l.Contains("G92 X0 Y0 Z5"));
            Assert.IsNotNull(g92, "Ожидалась строка G92 X0 Y0 Z5");

            var endMove = program.Lines
                .Where(l => l.Contains("X100 Y0 Z5"))
                .LastOrDefault();
            Assert.IsNotNull(endMove, "Ожидался быстрый переход в конечную точку X100 Y0 Z5 перед M5/M30");
        }

        [TestMethod]
        public void Settings_ArcsOff_ProfileCircle_UsesNoArcCommands()
        {
            var withArcs = FixtureCatalog.All.First(f => f.Name == "Profile.Circle.Default");
            var withoutArcs = FixtureCatalog.All.First(f => f.Name == "Profile.Circle.ArcsOff");

            var programWithArcs = Generator.Generate(withArcs.Operations, withArcs.Settings);
            Assert.IsTrue(programWithArcs.Lines.Any(l => Regex.IsMatch(l, @"(^|\s)G[23]\s")),
                "Профиль круга с AllowArcs=true должен содержать дуги G2/G3");

            var programWithoutArcs = Generator.Generate(withoutArcs.Operations, withoutArcs.Settings);
            Assert.IsFalse(programWithoutArcs.Lines.Any(l => Regex.IsMatch(l, @"(^|\s)G[23]\s")),
                "При AllowArcs=false дуги G2/G3 запрещены");
        }

        private static double GetArea(DxfPolyline contour)
        {
            double area = 0;
            var points = contour.Points;
            for (int i = 0; i < points.Count - 1; i++)
            {
                area += points[i].X * points[i + 1].Y - points[i + 1].X * points[i].Y;
            }
            return Math.Abs(area / 2.0);
        }
    }
}
