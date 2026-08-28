#nullable enable
using System.Linq;
using GCodeGenerator.Models;
using GCodeGenerator.Tests.Fixtures;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Высоты, на которых инструмент проходит над заготовкой, проверяются
    /// относительно самой заготовки, а не только на «это число».
    ///
    /// Безопасная высота фрезерования, высота переходов между отверстиями и
    /// отвод внутри отверстия — три величины, отделяющие холостой ход от
    /// материала. Каждая из них абсолютная, поэтому «разумное» значение
    /// зависит от того, где лежит поверхность: те же 1,0 мм безопасны над
    /// нулём и уводят инструмент внутрь материала при обработке выступа
    /// высотой 5 мм. Прежде от всех трёх требовалось только быть конечным
    /// числом.
    /// </summary>
    [TestClass]
    public sealed class SafeHeightValidationTests
    {
        // ------------------------------------------------------------------
        // Фрезерование: безопасная высота против высоты контура
        // ------------------------------------------------------------------

        [TestMethod]
        public void Profile_SafeZBelowContourHeight_IsRejected()
        {
            var operation = OperationFixtures.ProfileCircle();
            operation.ContourHeight = 5.0;
            operation.SafeZHeight = 1.0;

            AssertSingleIssue(operation, nameof(operation.SafeZHeight), ValidationCode.NotAbove);
        }

        /// <summary>
        /// Равенство — тоже отказ: перенос ровно по верхней плоскости
        /// заготовки означает, что фреза идёт по ней вплотную.
        /// </summary>
        [TestMethod]
        public void Profile_SafeZEqualToContourHeight_IsRejected()
        {
            var operation = OperationFixtures.ProfileCircle();
            operation.ContourHeight = 2.0;
            operation.SafeZHeight = 2.0;

            AssertSingleIssue(operation, nameof(operation.SafeZHeight), ValidationCode.NotAbove);
        }

        /// <summary>
        /// Заготовка ниже нуля — обычный случай, и отрицательные высоты в нём
        /// законны: важно их взаимное расположение, а не знак.
        /// </summary>
        [TestMethod]
        public void Profile_BothHeightsNegative_IsValidWhenSafeZIsHigher()
        {
            var operation = OperationFixtures.ProfileCircle();
            operation.ContourHeight = -10.0;
            operation.SafeZHeight = -8.5;

            AssertValid(operation);
        }

        [TestMethod]
        public void Pocket_SafeZBelowContourHeight_IsRejected()
        {
            var operation = OperationFixtures.PocketCircle();
            operation.ContourHeight = 3.0;
            operation.SafeZHeight = 1.0;

            AssertSingleIssue(operation, nameof(operation.SafeZHeight), ValidationCode.NotAbove);
        }

        /// <summary>
        /// Остров задаёт только запрещённую геометрию и сам не обрабатывается:
        /// его высоты никуда не выводятся и мешать генерации проекта не должны.
        /// </summary>
        [TestMethod]
        public void Island_SafeZBelowContourHeight_IsNotFlagged()
        {
            var operation = OperationFixtures.PocketCircle();
            operation.PocketMode = PocketMode.Island;
            operation.ContourHeight = 3.0;
            operation.SafeZHeight = 1.0;

            AssertValid(operation);
        }

        /// <summary>
        /// Высота контура, которая сама не является числом, названа один раз:
        /// сравнивать с ней нечего, и второе сообщение о том же говорило бы
        /// о параметре, который пользователь не трогал.
        /// </summary>
        [TestMethod]
        public void Profile_NonFiniteContourHeight_IsReportedOnce()
        {
            var operation = OperationFixtures.ProfileCircle();
            operation.ContourHeight = double.NaN;

            AssertSingleIssue(operation, nameof(operation.ContourHeight), ValidationCode.NotFinite);
        }

        // ------------------------------------------------------------------
        // Сверление: переход между отверстиями
        // ------------------------------------------------------------------

        [TestMethod]
        public void Drill_SafeZBetweenHolesBelowHoleTop_IsRejected()
        {
            var operation = OperationFixtures.DrillLine();
            operation.StartZ = 4.0;
            // Отвод поднят вместе с поверхностью: проверяется одна величина,
            // а не заодно и он.
            operation.RetractHeight = 4.5;
            operation.SafeZBetweenHoles = 1.0;

            AssertSingleIssue(
                operation, nameof(operation.SafeZBetweenHoles), ValidationCode.NotAbove);
        }

        /// <summary>
        /// Поднятая поверхность делает малыми сразу обе высоты сверления, и
        /// названы должны быть обе: подняв только одну, пользователь получил
        /// бы отказ во второй раз.
        /// </summary>
        [TestMethod]
        public void Drill_RaisedSurface_NamesBothHeights()
        {
            var operation = OperationFixtures.DrillLine();
            operation.StartZ = 4.0;

            var issues = operation.Validate();

            CollectionAssert.AreEquivalent(
                new[] { nameof(operation.RetractHeight), nameof(operation.SafeZBetweenHoles) },
                issues.Select(issue => issue.ParameterName).Distinct().ToArray(),
                string.Join("; ", issues.Select(i => i.ToString())));
        }

        /// <summary>
        /// Отверстия могут лежать на разной высоте: пределом служит самое
        /// высокое из них, иначе переход задевал бы именно его.
        /// </summary>
        [TestMethod]
        public void Drill_SafeZBetweenHoles_IsMeasuredAgainstHighestHole()
        {
            var operation = DrillPointsOperation.CreateNew(DrillMode.Points);
            operation.SafeZBetweenHoles = 2.0;
            operation.Holes.Add(Hole(x: 0, z: 0.0));
            operation.Holes.Add(Hole(x: 10, z: 1.5));

            AssertValid(operation);

            operation.Holes.Add(Hole(x: 20, z: 6.0));

            AssertSingleIssue(
                operation, nameof(operation.SafeZBetweenHoles), ValidationCode.NotAbove);
        }

        // ------------------------------------------------------------------
        // Сверление: отвод внутри отверстия
        // ------------------------------------------------------------------

        [TestMethod]
        public void Drill_HoleRetractBelowHoleTop_IsRejected()
        {
            var operation = DrillPointsOperation.CreateNew(DrillMode.Points);
            operation.Holes.Add(Hole(x: 0, z: 0.0, retract: -0.5));

            AssertSingleIssue(operation, "Holes[0].RetractHeight", ValidationCode.BelowMinimum);
        }

        /// <summary>
        /// Отвод ровно к верху отверстия — полный выход сверла, обычный режим
        /// сверления с эвакуацией стружки.
        /// </summary>
        [TestMethod]
        public void Drill_HoleRetractEqualToHoleTop_IsValid()
        {
            var operation = DrillPointsOperation.CreateNew(DrillMode.Points);
            operation.Holes.Add(Hole(x: 0, z: -2.0, retract: -2.0));
            operation.SafeZBetweenHoles = 1.0;

            AssertValid(operation);
        }

        // ------------------------------------------------------------------
        // Общие ожидания
        // ------------------------------------------------------------------

        /// <summary>
        /// Операция, только что созданная со значениями по умолчанию, не
        /// должна встречать пользователя отказом по высотам: он их ещё
        /// не трогал. Проверяется весь каталог — новый тип операции с иными
        /// умолчаниями попадёт сюда сам.
        /// </summary>
        [TestMethod]
        public void DefaultHeights_OfEveryCatalogOperation_AreValid()
        {
            foreach (var descriptor in GCodeGenerator.Operations.OperationCatalog.All)
            {
                var operation = descriptor.Create();
                if (operation is not IValidatable validatable)
                    continue;

                // Геометрии у новой операции ещё нет, и об этом она сообщает
                // законно; здесь интересны только высоты.
                var heights = validatable.Validate()
                    .Where(issue => issue.Code == ValidationCode.NotAbove)
                    .ToList();

                Assert.AreEqual(0, heights.Count,
                    $"{descriptor.PersistentName}: {string.Join("; ", heights.Select(i => i.ToString()))}");
            }
        }

        /// <summary>
        /// Отверстие для проверки одной высоты за раз: отвод по умолчанию
        /// поднят над верхом отверстия, поэтому собственных замечаний
        /// не вызывает.
        /// </summary>
        private static DrillHole Hole(double x, double z, double? retract = null)
            => new DrillHole
            {
                X = x,
                Y = 0,
                Z = z,
                TotalDepth = 2,
                StepDepth = 1,
                RetractHeight = retract ?? z + 1.0,
            };

        private static void AssertValid(IValidatable operation)
        {
            var issues = operation.Validate();
            Assert.AreEqual(0, issues.Count, string.Join("; ", issues.Select(i => i.ToString())));
        }

        private static void AssertSingleIssue(
            IValidatable operation, string property, ValidationCode code)
        {
            var issues = operation.Validate();
            Assert.AreEqual(1, issues.Count, string.Join("; ", issues.Select(i => i.ToString())));
            Assert.AreEqual(property, issues[0].Property);
            Assert.AreEqual(code, issues[0].Code);
            Assert.IsFalse(string.IsNullOrWhiteSpace(issues[0].Message));
        }
    }
}
