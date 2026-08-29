using System.Collections.Generic;
using System.Linq;
using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.Models;
using GCodeGenerator.Tests.Fixtures;
using GCodeGenerator.Toolpath;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// DXF-карман из нескольких раздельных областей: каждая область — это
    /// самостоятельный карман, и врезаться в неё нужно так же, как в первую.
    /// Прежде рабочую подачу при врезании получала только первая область,
    /// а все последующие опускались на глубину слоя быстрым ходом — в ещё
    /// не тронутый материал: на первом слое центр второй области — сплошная
    /// заготовка. Дефект жил незамеченным, потому что golden-файлов
    /// с многообластным DXF-карманом нет, а последовательность
    /// «подход-врезание-возврат» здесь своя, а не общая с одноконтурным
    /// карманом.
    /// </summary>
    [TestClass]
    public class DxfPocketMultiAreaTests
    {
        private const double TotalDepth = 2.0;
        private const double FeedZWork = 200.0;

        /// <summary>
        /// Два отдельных квадрата в одной операции: смещение на радиус фрезы
        /// даёт две несвязанные области.
        /// </summary>
        private static PocketDxfOperation TwoSquares() => new PocketDxfOperation
        {
            ClosedContours = new List<Polyline2D> { Square(0.0), Square(40.0) },
            TotalDepth = TotalDepth,
            StepDepth = TotalDepth, // один слой: поведение видно без шума слоёв
            ContourHeight = 0.0,
            SafeZHeight = 5.0,
            ToolDiameter = 10.0,
            StepPercentOfTool = 40.0,
            FeedXYRapid = 1000.0,
            FeedXYWork = 300.0,
            FeedZRapid = 500.0,
            FeedZWork = FeedZWork,
            Decimals = 3,
            WallTaperAngleDeg = 0.0,
            IsRoughingEnabled = false,
            IsFinishingEnabled = false,
            PocketStrategy = PocketStrategy.Spiral,
        };

        private static Polyline2D Square(double left) => new Polyline2D
        {
            Points =
            {
                new Point2D { X = left, Y = 0.0 },
                new Point2D { X = left + 20.0, Y = 0.0 },
                new Point2D { X = left + 20.0, Y = 20.0 },
                new Point2D { X = left, Y = 20.0 },
                new Point2D { X = left, Y = 0.0 },
            },
        };

        /// <summary>
        /// Каждая область получает врезание рабочим ходом, а быстрые ходы
        /// не опускаются ниже верха слоя — там материал ещё не снят.
        /// Проверяется сама траектория, а не текст программы: важен вид
        /// перемещения и подача, а не формат кадра.
        /// </summary>
        [TestMethod]
        public void EveryArea_PlungesAtWorkFeed_AndRapidsStayAboveLayer()
        {
            var op = TwoSquares();
            Assert.AreEqual(0, op.Validate().Count, "фикстура из двух квадратов должна быть пригодной");

            var toolPath = OperationToolPath.Build(new UnifiedPocketGenerator(), op, new GCodeSettings());

            var z = 0.0;
            var workPlunges = 0;
            foreach (var move in toolPath.Moves())
            {
                var targetZ = move.Z ?? z;
                if (targetZ < z - 1e-9)
                {
                    if (move.Kind == ToolMoveKind.Rapid)
                    {
                        Assert.IsTrue(targetZ >= -1e-9,
                            $"быстрый ход опустился до Z={targetZ}: ниже верха слоя материал ещё не снят");
                    }
                    if (targetZ <= -TotalDepth + 1e-9)
                    {
                        Assert.AreEqual(ToolMoveKind.Linear, move.Kind,
                            "врезание на глубину слоя выполняется только рабочим ходом");
                        Assert.AreEqual(FeedZWork, move.Feed ?? double.NaN, 1e-9,
                            "врезание идёт на подаче FeedZWork");
                        workPlunges++;
                    }
                }
                z = targetZ;
            }

            Assert.AreEqual(2, workPlunges,
                "две раздельные области — два врезания рабочим ходом, по одному на область");
        }

        /// <summary>
        /// DXF-ветка не имеет общего подвода перед циклом областей, поэтому
        /// первая область сама обязана поднять инструмент. Иначе первым
        /// движением прямого вызова генератора был быстрый XY на исходной Z,
        /// которая может совпадать с поверхностью или находиться в детали.
        /// </summary>
        [TestMethod]
        public void FirstArea_RaisesToSafeZBeforeAnyXyPositioning()
        {
            var operation = TwoSquares();
            var moves = OperationToolPath.Build(
                    new UnifiedPocketGenerator(), operation, new GCodeSettings())
                .Moves()
                .ToList();

            Assert.IsTrue(moves.Count > 0, "траектория DXF-кармана построена");
            Assert.AreEqual(ToolMoveKind.Rapid, moves[0].Kind);
            Assert.AreEqual(operation.SafeZHeight, moves[0].Z ?? double.NaN, 1e-9,
                "первый ход поднимает инструмент на SafeZ");
            Assert.IsFalse(moves[0].X.HasValue || moves[0].Y.HasValue,
                "XY-позиционирование начинается только после подъёма");

            var firstXy = moves.FindIndex(move => move.X.HasValue || move.Y.HasValue);
            Assert.IsTrue(firstXy > 0, "перед первым XY существует отдельный подъём");
            Assert.AreEqual(operation.SafeZHeight, moves[firstXy - 1].Z ?? double.NaN, 1e-9);
        }
    }
}
