using System;
using System.Collections.Generic;
using System.Linq;
using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.Models;
using GCodeGenerator.Tests.Fixtures;
using GCodeGenerator.Trajectory;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Трёхмерный предпросмотр строится из траектории, а не разбором готовой
    /// программы.
    ///
    /// Раньше окно читало G-слова и восстанавливало по ним, чем было каждое
    /// движение, в какой плоскости лежит дуга и где ноль детали, — то есть
    /// программа интерпретировала собственный вывод, и расхождение между
    /// показанным и выполненным было ничем не защищено. Здесь проверяется,
    /// что новый путь даёт ту же траекторию, что и прежний разбор.
    /// </summary>
    [TestClass]
    public class ToolPathSceneTests
    {
        private const double Tolerance = 1e-6;

        private static IEnumerable<OperationBase[]> ReferenceCases()
        {
            yield return new OperationBase[] { OperationFixtures.DrillPoints() };
            yield return new OperationBase[] { OperationFixtures.DrillLine() };
            yield return new OperationBase[] { OperationFixtures.ProfileCircle() };
            yield return new OperationBase[] { OperationFixtures.ProfileRectangle() };
            yield return new OperationBase[] { OperationFixtures.ProfileEllipse() };
            yield return new OperationBase[] { OperationFixtures.PocketCircle() };
            yield return new OperationBase[] { OperationFixtures.PocketRectangle() };
            yield return new OperationBase[] { OperationFixtures.PocketDxf() };
            yield return new OperationBase[]
            {
                OperationFixtures.DrillLine(), OperationFixtures.ProfileCircle(), OperationFixtures.PocketRectangle()
            };
        }

        /// <summary>
        /// Главная проверка волны: сцена из траектории совпадает со сценой,
        /// полученной прежним разбором программы, — на всех типах операций,
        /// включая дуги и контуры из чертежа.
        /// </summary>
        [TestMethod]
        public void SceneFromToolPath_MatchesSceneFromProgram()
        {
            var generator = new SimpleGCodeGenerator();
            var settings = new GCodeSettings();
            var problems = new List<string>();
            var checkedCases = 0;

            foreach (var operations in ReferenceCases())
            {
                checkedCases++;
                var list = new List<OperationBase>(operations);

                var fromProgram = SceneBuilder.Build(generator.Generate(list, settings));
                var fromToolPath = ToolPathSceneBuilder.Build(generator.BuildToolPath(list, settings));

                if (fromProgram.Segments.Count != fromToolPath.Segments.Count)
                {
                    problems.Add($"{Describe(operations)}: сегментов {fromProgram.Segments.Count} против {fromToolPath.Segments.Count}");
                    continue;
                }

                for (int i = 0; i < fromProgram.Segments.Count; i++)
                {
                    var expected = fromProgram.Segments[i];
                    var actual = fromToolPath.Segments[i];

                    if (expected.MoveType != actual.MoveType)
                        problems.Add($"{Describe(operations)} [{i}]: тип {expected.MoveType} против {actual.MoveType}");
                    else if (!Same(expected.Start, actual.Start) || !Same(expected.End, actual.End))
                        problems.Add($"{Describe(operations)} [{i}]: {Show(expected)} против {Show(actual)}");
                }
            }

            Assert.IsTrue(checkedCases > 0, "Ни одного случая не проверено");
            Assert.AreEqual(0, problems.Count, string.Join(Environment.NewLine, problems));
        }

        /// <summary>
        /// Дуга остаётся дугой: у сегмента есть центр, радиус и точки, по
        /// которым окно рисует её плавной.
        /// </summary>
        [TestMethod]
        public void ArcMove_KeepsCenterRadiusAndPoints()
        {
            var settings = new GCodeSettings();
            settings.Format.AllowArcs = true;
            var generator = new SimpleGCodeGenerator();

            var scene = ToolPathSceneBuilder.Build(generator.BuildToolPath(
                new List<OperationBase> { OperationFixtures.ProfileCircle() }, settings));

            var arc = scene.Segments.FirstOrDefault(s => s.MoveType == MoveType.ArcCW || s.MoveType == MoveType.ArcCCW);
            Assert.IsNotNull(arc, "Контур по окружности даёт дуги");
            Assert.IsNotNull(arc.ArcCenter, "У дуги есть центр");
            Assert.IsTrue(arc.ArcRadius > 0, "У дуги есть радиус");
            Assert.IsTrue(arc.InterpolatedPoints != null && arc.InterpolatedPoints.Count >= 4,
                "Дуга разложена на точки для отрисовки");
        }

        [TestMethod]
        public void EmptyToolPath_GivesEmptyScene()
        {
            Assert.AreEqual(0, ToolPathSceneBuilder.Build(null).Segments.Count);
            Assert.AreEqual(0, ToolPathSceneBuilder.Build(new Toolpath.ToolPath()).Segments.Count);
        }

        /// <summary>
        /// Перемещение, которое ничего не двигает, сегмента не создаёт:
        /// в сцене не должно быть точек нулевой длины.
        /// </summary>
        [TestMethod]
        public void ZeroLengthMove_IsNotASegment()
        {
            var operation = new ToolPathOperationBuilder();
            operation.Rapid(10, 10, 0);
            operation.Rapid(10, 10, 0);

            var scene = ToolPathSceneBuilder.Build(operation.ToToolPath());

            Assert.AreEqual(1, scene.Segments.Count, "Второе перемещение никуда не ведёт");
        }

        private static bool Same(Vec3 a, Vec3 b)
            => Math.Abs(a.X - b.X) < Tolerance
               && Math.Abs(a.Y - b.Y) < Tolerance
               && Math.Abs(a.Z - b.Z) < Tolerance;

        private static string Show(TrajectorySegment segment)
            => $"({segment.Start.X:0.###},{segment.Start.Y:0.###},{segment.Start.Z:0.###})→" +
               $"({segment.End.X:0.###},{segment.End.Y:0.###},{segment.End.Z:0.###})";

        private static string Describe(OperationBase[] operations)
            => string.Join("+", operations.Select(o => o.GetType().Name));

        /// <summary>Небольшой помощник: траектория из нескольких перемещений.</summary>
        private sealed class ToolPathOperationBuilder
        {
            private readonly Toolpath.ToolPathOperation _operation =
                new Toolpath.ToolPathOperation("test", "test", 3);

            public void Rapid(double x, double y, double z)
                => new Toolpath.ToolPathBuilder(_operation).RapidTo(x, y, z);

            public Toolpath.ToolPath ToToolPath()
            {
                var path = new Toolpath.ToolPath();
                path.AddOperation(_operation);
                return path;
            }
        }
    }
}
