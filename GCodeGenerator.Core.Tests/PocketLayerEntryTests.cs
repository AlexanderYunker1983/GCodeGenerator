#nullable enable
using System;
using System.Collections.Generic;
using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.Models;
using GCodeGenerator.Toolpath;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Правило врезания: в материал инструмент входит только рабочей подачей.
    ///
    /// Быстрый ход по Z вниз допустим до верха слоя — выше него материала нет,
    /// он снят предыдущими проходами или лежит вне заготовки. Всё, что глубже,
    /// проходится подачей <c>FeedZWork</c>: <c>G0</c> на дно слоя означает удар
    /// торцом фрезы по нетронутому материалу.
    ///
    /// Проверяется на уровне траектории, а не текста программы: правило
    /// относится к движению инструмента и не зависит ни от постпроцессора,
    /// ни от числа знаков в координатах.
    ///
    /// Стратегии выборки возвращаются к врезанию в каждом месте, где очередной
    /// проход начинается не там, где закончился предыдущий, — отдельный рез
    /// параллельной линии, участок за островом, разорванный виток спирали, —
    /// поэтому проверка идёт по всем стратегиям сразу и отдельно повторяется
    /// для карманов с островом, где такие разрывы возникают всегда.
    /// </summary>
    [TestClass]
    public sealed class PocketLayerEntryTests
    {
        /// <summary>Допуск сравнения высот: траектория хранит числа без округления.</summary>
        private const double Tolerance = 1e-9;

        private static readonly PocketStrategy[] AllStrategies =
        {
            PocketStrategy.Spiral,
            PocketStrategy.Concentric,
            PocketStrategy.Radial,
            PocketStrategy.ZigZag,
            PocketStrategy.Lines,
        };

        [TestMethod]
        public void EveryStrategy_NeverPlungesIntoMaterialAtRapidFeed()
        {
            foreach (var strategy in AllStrategies)
            {
                var pocket = RectanglePocket(strategy);
                AssertPlungesUseWorkingFeed(Build(pocket), pocket, strategy.ToString());
            }
        }

        [TestMethod]
        public void EveryStrategy_WithIsland_NeverPlungesIntoMaterialAtRapidFeed()
        {
            foreach (var strategy in AllStrategies)
            {
                var pocket = RectanglePocket(strategy);
                AssertPlungesUseWorkingFeed(
                    Build(pocket, CircleIsland()), pocket, $"{strategy} с островом");
            }
        }

        /// <summary>
        /// Многослойный карман: у каждого слоя свой верх, и врезание в него
        /// начинается именно с него, а не с верха заготовки.
        /// </summary>
        [TestMethod]
        public void EveryStrategy_MultipleLayers_NeverPlungesIntoMaterialAtRapidFeed()
        {
            foreach (var strategy in AllStrategies)
            {
                var pocket = RectanglePocket(strategy);
                pocket.TotalDepth = 3.0;
                pocket.StepDepth = 0.8; // последний слой — неполный
                AssertPlungesUseWorkingFeed(
                    Build(pocket, CircleIsland()), pocket, $"{strategy}, 4 слоя");
            }
        }

        /// <summary>
        /// Черновой проход с припуском и чистовая обработка стенок и дна:
        /// у этих проходов собственные высоты, и правило действует и для них.
        /// </summary>
        [TestMethod]
        public void RoughingAndFinishing_NeverPlungeIntoMaterialAtRapidFeed()
        {
            foreach (var strategy in AllStrategies)
            {
                var pocket = RectanglePocket(strategy);
                pocket.TotalDepth = 2.0;
                pocket.StepDepth = 0.7;
                pocket.IsRoughingEnabled = true;
                pocket.IsFinishingEnabled = true;
                pocket.FinishAllowance = 0.3;
                pocket.FinishingMode = PocketFinishingMode.All;

                AssertPlungesUseWorkingFeed(
                    Build(pocket, CircleIsland()), pocket, $"{strategy}, черновая и чистовая");
            }
        }

        /// <summary>
        /// Отдельный разбор стратегии параллельных линий: она входит в слой
        /// заново перед каждым резом, поэтому именно на ней правило нарушалось
        /// на любом кармане, а не только вокруг островов.
        /// </summary>
        [TestMethod]
        public void Lines_EntersEveryCutFromLayerTopAtWorkingFeed()
        {
            var pocket = RectanglePocket(PocketStrategy.Lines);
            var path = Build(pocket);

            var entries = 0;
            var previousZ = double.PositiveInfinity;
            var atLayerTop = false;

            foreach (var move in path.Moves())
            {
                if (!move.Z.HasValue)
                    continue;

                var target = move.Z.Value;
                if (move.Kind == ToolMoveKind.Rapid)
                {
                    atLayerTop = Math.Abs(target - pocket.ContourHeight) <= Tolerance;
                }
                else if (Math.Abs(target - (pocket.ContourHeight - pocket.TotalDepth)) <= Tolerance
                         && target < previousZ - Tolerance)
                {
                    entries++;
                    Assert.IsTrue(atLayerTop,
                        $"врезание №{entries} началось не с верха слоя (Z={previousZ:0.###})");
                }

                previousZ = target;
            }

            Assert.IsTrue(entries > 1,
                $"стратегия линий входит в слой перед каждым резом, врезаний найдено {entries}");
        }

        /// <summary>
        /// Ни один быстрый ход не опускает инструмент ниже верха слоя.
        /// </summary>
        /// <param name="path">Построенная траектория.</param>
        /// <param name="pocket">Операция кармана — из неё известны высоты слоёв.</param>
        /// <param name="what">Название случая для сообщения об ошибке.</param>
        private static void AssertPlungesUseWorkingFeed(
            ToolPath path, PocketOperationBase pocket, string what)
        {
            var layerTops = LayerTops(pocket);
            var previousZ = double.PositiveInfinity;
            var plunges = 0;

            foreach (var move in path.Moves())
            {
                if (!move.Z.HasValue)
                    continue;

                var target = move.Z.Value;
                if (move.Kind == ToolMoveKind.Rapid && target < previousZ - Tolerance)
                {
                    plunges++;
                    Assert.IsTrue(IsLayerTopOrHigher(target, layerTops),
                        $"{what}: быстрый ход опускает инструмент на Z={target:0.####}, "
                        + "а это ниже верха слоя — в материал входят только рабочей подачей");
                }

                previousZ = target;
            }

            Assert.IsTrue(plunges > 0, $"{what}: в траектории нет ни одного опускания по Z");
        }

        /// <summary>
        /// Высота допустима для быстрого хода, если она выше верха заготовки
        /// или совпадает с верхом одного из слоёв любого прохода операции.
        /// </summary>
        private static bool IsLayerTopOrHigher(double z, IReadOnlyCollection<double> layerTops)
        {
            foreach (var top in layerTops)
            {
                if (z >= top - Tolerance)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Верхние высоты всех слоёв операции. Проходы берутся у планировщика:
        /// черновой проход и чистовая обработка идут по собственным высотам,
        /// и повторять его раскладку в тесте значило бы проверять её саму.
        /// </summary>
        private static IReadOnlyCollection<double> LayerTops(PocketOperationBase pocket)
        {
            var tops = new List<double>();
            foreach (var pass in PocketPassPlanner.Plan(pocket).Passes)
            {
                var operation = pass.Operation;
                var finalZ = operation.ContourHeight - operation.TotalDepth;
                for (var top = operation.ContourHeight; top > finalZ; top -= operation.StepDepth)
                    tops.Add(top);
            }

            return tops;
        }

        private static ToolPath Build(params OperationBase[] operations)
            => new SimpleGCodeGenerator().BuildToolPath(operations, new GCodeSettings());

        private static PocketRectangleOperation RectanglePocket(PocketStrategy strategy)
            => new PocketRectangleOperation
            {
                Width = 40,
                Height = 30,
                ToolDiameter = 2,
                ContourHeight = 0,
                SafeZHeight = 5,
                TotalDepth = 1,
                StepDepth = 1,
                StepPercentOfTool = 100,
                PocketStrategy = strategy,
                LineAngleDeg = 0,
            };

        private static PocketCircleOperation CircleIsland()
            => new PocketCircleOperation
            {
                PocketMode = PocketMode.Island,
                Radius = 4,
            };
    }
}
