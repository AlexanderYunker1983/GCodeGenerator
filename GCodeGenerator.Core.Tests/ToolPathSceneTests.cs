using System;
using System.Collections.Generic;
using System.Linq;
using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.Models;
using GCodeGenerator.Preview;
using GCodeGenerator.Tests.Fixtures;
using GCodeGenerator.Toolpath;
using GCodeGenerator.Trajectory;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Сцены промежуточной траектории и готовой программы. Рабочее окно
    /// показывает программу: только в ней есть координатный пролог, эпилог
    /// и округление выбранной точности. Прямой построитель ToolPath остаётся
    /// для внутренних геометрических проверок.
    /// </summary>
    [TestClass]
    public class ToolPathSceneTests
    {
        private const double Tolerance = 1e-3;

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
        /// Без координатного пролога и парковки две сцены совпадают с точностью
        /// вывода: программа намеренно округляет исходный ToolPath.
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
        public void ProgramScene_HonorsG92RoundingAndFooterParking()
        {
            var source = OperationFixtures.ProfileCircle();
            var operation = new ToolPathOperation("profile", "profile", 3, source);
            var builder = new ToolPathBuilder(operation);
            builder.RapidTo(x: 0.0006, y: 2.0, z: 1.0, feed: 1000);
            var path = new ToolPath();
            path.AddOperation(operation);

            var settings = new GCodeSettings();
            settings.WorkCoordinate.AddStartPosition = true;
            settings.WorkCoordinate.StartX = 100;
            settings.WorkCoordinate.StartY = 200;
            settings.WorkCoordinate.StartZ = 10;
            settings.WorkCoordinate.AddEndPosition = true;
            settings.WorkCoordinate.EndX = 50;
            settings.WorkCoordinate.EndY = 60;
            settings.WorkCoordinate.EndZ = 3;

            var program = new GenericPostProcessor().Build(path, settings);
            var scene = SceneBuilder.Build(program);

            Assert.AreEqual(new Vec3(100, 200, 10), scene.Segments[0].Start,
                "G92 задаёт исходную позицию без фантомного хода от нуля");
            Assert.AreEqual(new Vec3(0.001, 2, 1), scene.Segments[0].End,
                "предпросмотр видит округлённое слово X0.001");
            Assert.AreSame(source, scene.Segments[0].Source);
            Assert.IsTrue(scene.Segments.Any(segment =>
                    segment.End == new Vec3(50, 60, 3)),
                "парковка эпилога входит в сцену");
            Assert.IsTrue(scene.Segments.Any(segment => segment.Source == null),
                "служебные перемещения не приписываются операции");

            var top = ProgramSceneProjection.Build(program);
            Assert.IsTrue(top.Shapes.Any(shape => ReferenceEquals(shape.Operation, source)));
            Assert.IsTrue(top.Shapes.Any(shape => shape.Operation == null
                                                  && shape.Kind == OperationShapeKind.RapidMove));
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
