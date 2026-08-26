using System;
using System.Collections.Generic;
using System.Threading;
using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.GCodeGenerators.Strategies;
using GCodeGenerator.Models;
using GCodeGenerator.Tests.Fixtures;
using GCodeGenerator.Toolpath;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Отмена генерации внутри одной операции.
    ///
    /// Прежде токен проверялся только между операциями: глубокий карман
    /// из сотен слоёв нельзя было прервать, пока он не построится целиком,
    /// хотя результат уже решено отбросить.
    /// </summary>
    [TestClass]
    public class GenerationCancellationTests
    {
        /// <summary>
        /// Карман на десять слоёв, отмена после первого: стратегия обязана
        /// быть вызвана один раз, а не десять. Момент отмены ловится
        /// стратегией-обёрткой через реестр — тем же расширением, которым
        /// подключалась бы настоящая новая стратегия.
        /// </summary>
        [TestMethod]
        public void CancellationInsideOperation_StopsBetweenLayers()
        {
            var cancellation = new CancellationTokenSource();
            var strategy = new CancellingStrategy(new SpiralPocketingStrategy(), cancellation);
            var generator = new UnifiedPocketGenerator(new PocketStrategyRegistry(
                new Dictionary<PocketStrategy, IPocketPocketingStrategy>
                {
                    [PocketStrategy.Spiral] = strategy,
                }));
            var operation = OperationFixtures.PocketCircle();
            operation.TotalDepth = 10;
            operation.StepDepth = 1;
            var pathOperation = new ToolPathOperation(operation.Name, "", operation.Decimals, operation);

            Assert.Throws<OperationCanceledException>(() => generator.Generate(
                operation, new ToolPathBuilder(pathOperation), new GCodeSettings(), cancellation.Token));

            Assert.AreEqual(1, strategy.Calls, "После отмены следующий слой не начинается");
        }

        /// <summary>
        /// Уже отменённый токен останавливает каждую операцию до первой
        /// единицы работы: слоя, отверстия.
        /// </summary>
        [TestMethod]
        public void AlreadyCancelledToken_StopsEveryGeneratorBeforeWork()
        {
            var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            var settings = new GCodeSettings();

            foreach (var (generator, operation) in new (IOperationGenerator, OperationBase)[]
            {
                (new DrillPointsOperationGenerator(), OperationFixtures.DrillPoints()),
                (new UnifiedProfileGenerator(), OperationFixtures.ProfileCircle()),
                (new UnifiedPocketGenerator(), OperationFixtures.PocketCircle()),
            })
            {
                var pathOperation = new ToolPathOperation(operation.Name, "", 3, operation);
                var builder = new ToolPathBuilder(pathOperation);

                Assert.Throws<OperationCanceledException>(
                    () => generator.Generate(operation, builder, settings, cancelled.Token),
                    operation.GetType().Name);
                Assert.AreEqual(0, pathOperation.Items.Count,
                    $"{operation.GetType().Name}: перемещения не начались");
            }
        }

        /// <summary>
        /// Стратегия, отменяющая генерацию после первого обработанного слоя, —
        /// и считающая, сколько слоёв ей отдали после этого.
        /// </summary>
        private sealed class CancellingStrategy : IPocketPocketingStrategy
        {
            private readonly IPocketPocketingStrategy _inner;
            private readonly CancellationTokenSource _cancellation;

            public CancellingStrategy(IPocketPocketingStrategy inner, CancellationTokenSource cancellation)
            {
                _inner = inner;
                _cancellation = cancellation;
            }

            public int Calls { get; private set; }

            public void MillContour(PocketLayerContext layer, ToolPathBuilder builder)
            {
                Calls++;
                _cancellation.Cancel();
                _inner.MillContour(layer, builder);
            }
        }
    }
}
