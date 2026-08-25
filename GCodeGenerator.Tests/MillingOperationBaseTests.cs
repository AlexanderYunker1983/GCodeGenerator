using System.Linq;
using GCodeGenerator.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Общая часть фрезерных операций: подачи, раскладка по глубине,
    /// инструмент и точность вывода. Раньше эти пятнадцать параметров
    /// объявлялись заново в каждой из десяти моделей.
    /// </summary>
    [TestClass]
    public class MillingOperationBaseTests
    {
        /// <summary>
        /// Каждая операция профиля и кармана наследует общую часть: иначе
        /// параметры резания снова начнут расходиться между типами.
        /// </summary>
        [TestMethod]
        public void EveryProfileAndPocketOperation_SharesCommonMillingParameters()
        {
            var millingCategories = new[] { OperationCategory.Profile, OperationCategory.Pocket };

            foreach (var descriptor in OperationCatalog.All.Where(d => millingCategories.Contains(d.Category)))
            {
                Assert.IsInstanceOfType(
                    descriptor.Create(),
                    typeof(MillingOperationBase),
                    $"{descriptor.PersistentName} должна наследовать общую часть фрезерных операций");
            }
        }

        /// <summary>
        /// Сверление устроено иначе: у каждого отверстия свои глубина и подачи
        /// по Z, поэтому общую часть фрезерных операций оно не наследует.
        /// </summary>
        [TestMethod]
        public void DrillOperation_DoesNotShareMillingParameters()
        {
            Assert.IsNotInstanceOfType(new DrillPointsOperation(), typeof(MillingOperationBase));
        }

        /// <summary>
        /// Плоское свойство и сгруппированное — один и тот же параметр,
        /// а не две копии значения.
        /// </summary>
        [TestMethod]
        public void FlatProperties_AndGroups_ShareSameValues()
        {
            var operation = new PocketCircleOperation();

            operation.FeedXYRapid = 1234;
            operation.FeedZWork = 55;
            operation.TotalDepth = 9;
            operation.SafeZHeight = 3;

            Assert.AreEqual(1234.0, operation.Feeds.XYRapid);
            Assert.AreEqual(55.0, operation.Feeds.ZWork);
            Assert.AreEqual(9.0, operation.Depth.TotalDepth);
            Assert.AreEqual(3.0, operation.Depth.SafeZHeight);

            operation.Feeds.XYWork = 321;
            operation.Depth.StepDepth = 0.25;

            Assert.AreEqual(321.0, operation.FeedXYWork);
            Assert.AreEqual(0.25, operation.StepDepth);
        }

        /// <summary>
        /// Значения по умолчанию сохранены: новая операция должна вести себя
        /// так же, как до выделения общей части.
        /// </summary>
        [TestMethod]
        public void Defaults_MatchPreviousValues()
        {
            var operation = new ProfileCircleOperation();

            Assert.AreEqual(1000.0, operation.FeedXYRapid);
            Assert.AreEqual(300.0, operation.FeedXYWork);
            Assert.AreEqual(500.0, operation.FeedZRapid);
            Assert.AreEqual(200.0, operation.FeedZWork);
            Assert.AreEqual(0.0, operation.ContourHeight);
            Assert.AreEqual(2.0, operation.TotalDepth);
            Assert.AreEqual(1.0, operation.StepDepth);
            Assert.AreEqual(1.0, operation.SafeZHeight);
            Assert.AreEqual(0.3, operation.RetractHeight);
            Assert.AreEqual(3.0, operation.ToolDiameter);
            Assert.AreEqual(3, operation.Decimals);
            Assert.AreEqual(MillingDirection.Clockwise, operation.Direction);
        }

        /// <summary>
        /// Каждая операция получает собственные группы параметров: значения
        /// одной операции не должны меняться при правке другой.
        /// </summary>
        [TestMethod]
        public void Operations_DoNotShareParameterObjects()
        {
            var first = new PocketCircleOperation();
            var second = new PocketCircleOperation();

            first.FeedXYWork = 999;

            Assert.AreEqual(300.0, second.FeedXYWork);
            Assert.AreNotSame(first.Feeds, second.Feeds);
            Assert.AreNotSame(first.Depth, second.Depth);
        }

        /// <summary>
        /// Копия операции получает собственные группы параметров: правка копии
        /// (например, припуск чернового прохода) не должна менять оригинал.
        /// </summary>
        [TestMethod]
        public void Clone_GetsIndependentParameterObjects()
        {
            var operation = new PocketCircleOperation { FeedXYWork = 250, TotalDepth = 4 };

            var clone = (PocketCircleOperation)OperationCloner.Clone(operation);
            clone.Feeds.XYWork = 100;
            clone.Depth.TotalDepth = 1;

            Assert.AreEqual(250.0, operation.FeedXYWork);
            Assert.AreEqual(4.0, operation.TotalDepth);
            Assert.AreNotSame(operation.Feeds, clone.Feeds);
            Assert.AreNotSame(operation.Depth, clone.Depth);
        }
    }
}
