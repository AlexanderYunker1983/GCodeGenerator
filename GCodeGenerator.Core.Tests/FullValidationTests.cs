using System.Collections.Generic;
using System.Linq;
using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Полный набор проверок параметров.
    ///
    /// Раньше проверялись только глубина, шаг и диаметр инструмента —
    /// остальное доходило до станка как есть: нулевая рабочая подача давала
    /// «F0», шаг выборки больше диаметра фрезы оставлял между проходами
    /// нетронутый материал, а стратегия под номером, которого нет
    /// в перечислении, молча обрабатывалась спиралью.
    /// </summary>
    [TestClass]
    public class FullValidationTests
    {
        private static IReadOnlyList<ValidationIssue> Check(PocketCircleOperation operation)
            => operation.Validate();

        private static PocketCircleOperation ValidPocket()
            => new PocketCircleOperation { CenterX = 0, CenterY = 0, Radius = 10 };

        private static ProfileCircleOperation ValidProfile()
            => new ProfileCircleOperation { CenterX = 0, CenterY = 0, Radius = 10 };

        [TestMethod]
        public void ValidOperation_HasNoIssues()
        {
            Assert.AreEqual(0, Check(ValidPocket()).Count, "Операция со значениями по умолчанию годится для станка");
            Assert.AreEqual(0, ValidProfile().Validate().Count);
        }

        /// <summary>
        /// Нулевая рабочая подача — это «F0»: инструмент не поедет, а стойка
        /// либо остановится с ошибкой, либо будет ждать бесконечно.
        /// </summary>
        [TestMethod]
        public void ZeroWorkingFeed_IsRejected()
        {
            var operation = ValidPocket();
            operation.FeedXYWork = 0;

            Assert.IsTrue(Check(operation).Any(i => i.Property == nameof(operation.FeedXYWork)),
                "Нулевая рабочая подача в плоскости названа");

            operation = ValidPocket();
            operation.FeedZWork = 0;
            Assert.IsTrue(Check(operation).Any(i => i.Property == nameof(operation.FeedZWork)),
                "Нулевая рабочая подача по глубине названа");
        }

        [TestMethod]
        public void ZeroRapidFeed_IsRejected()
        {
            var operation = ValidPocket();
            operation.FeedXYRapid = 0;

            Assert.IsTrue(Check(operation).Any(i => i.Property == nameof(operation.FeedXYRapid)));
        }

        /// <summary>
        /// Шаг больше диаметра фрезы оставляет между проходами нетронутый
        /// материал: карман получится расчерченным, а не выбранным.
        /// </summary>
        [TestMethod]
        public void StepPercentOutOfRange_IsRejected()
        {
            foreach (var percent in new[] { 0.0, -10.0, 150.0 })
            {
                var operation = ValidPocket();
                operation.StepPercentOfTool = percent;

                Assert.IsTrue(Check(operation).Any(i => i.Property == nameof(operation.StepPercentOfTool)),
                    $"Шаг {percent}% недопустим");
            }
        }

        [TestMethod]
        public void DecimalsOutOfRange_IsRejected()
        {
            var negative = ValidPocket();
            negative.Decimals = -1;
            Assert.IsTrue(Check(negative).Any(i => i.Property == nameof(negative.Decimals)),
                "Отрицательное число знаков роняло форматирование координат");

            var huge = ValidPocket();
            huge.Decimals = 12;
            Assert.IsTrue(Check(huge).Any(i => i.Property == nameof(huge.Decimals)),
                "Двенадцать знаков после запятой не несут смысла для станка");
        }

        [TestMethod]
        public void NonFiniteCoordinate_IsRejected()
        {
            var operation = ValidPocket();
            operation.ContourHeight = double.NaN;

            Assert.IsTrue(Check(operation).Any(i => i.Property == nameof(operation.ContourHeight)),
                "Нечисловая высота контура дала бы «XNaN» в программе");
        }

        [TestMethod]
        public void NegativeAllowance_IsRejected()
        {
            var operation = ValidPocket();
            operation.FinishAllowance = -0.5;

            Assert.IsTrue(Check(operation).Any(i => i.Property == nameof(operation.FinishAllowance)));
        }

        /// <summary>
        /// Чистовой проход снимает припуск: без припуска снимать нечего.
        /// </summary>
        [TestMethod]
        public void FinishingWithoutAllowance_IsRejected()
        {
            var operation = ValidPocket();
            operation.IsFinishingEnabled = true;
            operation.FinishAllowance = 0;

            Assert.IsTrue(Check(operation).Any(i => i.Property == nameof(operation.FinishAllowance)));
        }

        /// <summary>
        /// Значение перечисления вне списка приходит из файла проекта —
        /// перечисления сохраняются числами.
        /// </summary>
        [TestMethod]
        public void UndefinedEnumValue_IsRejected()
        {
            var operation = ValidPocket();
            operation.PocketStrategy = (PocketStrategy)99;

            var issues = Check(operation);
            Assert.IsTrue(issues.Any(i => i.Property == nameof(operation.PocketStrategy)
                                          && i.Code == ValidationCode.NotAllowed),
                "Неизвестная стратегия не должна молча становиться спиралью");

            operation = ValidPocket();
            operation.PocketMode = (PocketMode)99;
            issues = Check(operation);
            Assert.IsTrue(issues.Any(i => i.Property == nameof(operation.PocketMode)
                                          && i.Code == ValidationCode.NotAllowed),
                "Неизвестное назначение геометрии не должно считаться обычным карманом");
        }

        [TestMethod]
        public void HelicalEntryParametersOutOfRange_AreRejected()
        {
            var operation = ValidPocket();
            operation.EntryMode = PocketEntryMode.Helical;
            operation.EntryAngle = 0;
            operation.HelicalEntryDiameter = 0;

            var issues = Check(operation);

            Assert.IsTrue(issues.Any(i => i.Property == nameof(operation.EntryAngle)),
                "нулевой угол не опускает инструмент по спирали");
            Assert.IsTrue(issues.Any(i => i.Property == nameof(operation.HelicalEntryDiameter)),
                "нулевой диаметр превращает спираль в вертикальную линию");

            operation.EntryAngle = 0.1;
            operation.HelicalEntryDiameter = double.Epsilon;
            Assert.IsTrue(Check(operation).Any(i => i.Code == ValidationCode.Inconsistent),
                "практически нулевой диаметр не должен порождать бесконечное число кадров");
        }

        /// <summary>
        /// Параметры спирали не влияют на вертикальный вход: старый проект
        /// без них использует конструкторские значения и остаётся валидным.
        /// </summary>
        [TestMethod]
        public void VerticalPocketEntry_IgnoresHelicalParameters()
        {
            var operation = ValidPocket();
            operation.EntryMode = PocketEntryMode.Vertical;
            operation.EntryAngle = 0;
            operation.HelicalEntryDiameter = 0;

            Assert.AreEqual(0, Check(operation).Count);
        }

        [TestMethod]
        public void RampEntryAngleOutOfRange_IsRejected()
        {
            var operation = ValidProfile();
            operation.EntryMode = EntryMode.Angled;
            operation.EntryAngle = 0;

            Assert.IsTrue(operation.Validate().Any(i => i.Property == nameof(operation.EntryAngle)),
                "Нулевой угол рампы не опускает инструмент вовсе");

            operation.EntryAngle = 90;
            Assert.IsTrue(operation.Validate().Any(i => i.Property == nameof(operation.EntryAngle)),
                "Прямой угол — это уже вертикальный вход");
        }

        /// <summary>
        /// Вертикальный вход углом не пользуется, поэтому его значение
        /// проверять незачем.
        /// </summary>
        [TestMethod]
        public void VerticalEntry_IgnoresEntryAngle()
        {
            var operation = ValidProfile();
            operation.EntryMode = EntryMode.Vertical;
            operation.EntryAngle = 0;

            Assert.AreEqual(0, operation.Validate().Count);
        }

        [TestMethod]
        public void DrillHoleFeeds_AreValidated()
        {
            var operation = new DrillPointsOperation
            {
                Holes = { new DrillHole { X = 1, Y = 1, FeedZWork = 0 } }
            };

            Assert.IsTrue(operation.Validate().Any(i => i.Property == "Holes[0].FeedZWork"));
        }

        [TestMethod]
        public void LineNumberStepZero_IsRejected()
        {
            var settings = new GCodeSettings();
            settings.Format.UseLineNumbers = true;
            settings.Format.LineNumberStep = 0;

            var issues = GCodeSettingsValidation.Validate(settings);

            Assert.IsTrue(issues.Any(i => i.Property == nameof(GCodeFormatSettings.LineNumberStep)),
                "Нулевой шаг даёт программу, где все строки называются одинаково");
        }

        [TestMethod]
        public void SpindleSpeedAndDelay_AreValidated()
        {
            var settings = new GCodeSettings();
            settings.Spindle.SpindleControlEnabled = true;
            settings.Spindle.SpindleSpeedEnabled = true;
            settings.Spindle.SpindleSpeedRpm = 0;
            settings.Spindle.SpindleDelayEnabled = true;
            settings.Spindle.SpindleDelaySeconds = 0;

            var issues = GCodeSettingsValidation.Validate(settings);

            Assert.IsTrue(issues.Any(i => i.Property == nameof(SpindleSettings.SpindleSpeedRpm)),
                "Нулевые обороты означают невращающийся шпиндель");
            Assert.IsTrue(issues.Any(i => i.Property == nameof(SpindleSettings.SpindleDelaySeconds)),
                "Нулевая задержка не даёт шпинделю раскрутиться");
        }

        /// <summary>
        /// Отказ доходит до пользователя целиком: генерация не начинается,
        /// а причины перечислены все сразу.
        /// </summary>
        [TestMethod]
        public void Generation_StopsAndNamesEveryReason()
        {
            var operation = ValidPocket();
            operation.FeedXYWork = 0;
            operation.Decimals = 99;

            var error = Assert.Throws<GCodeGenerationValidationException>(
                () => new SimpleGCodeGenerator().Generate(
                    new List<OperationBase> { operation }, new GCodeSettings()));

            var issues = error.Failures.SelectMany(f => f.Issues).ToList();
            Assert.IsTrue(issues.Any(i => i.Property == "FeedXYWork"), "Названа подача");
            Assert.IsTrue(issues.Any(i => i.Property == "Decimals"), "Названо число знаков");
        }

        /// <summary>
        /// У каждой проблемы есть код и имя параметра: по ним окно подберёт
        /// сообщение на языке пользователя и подсветит нужное поле.
        /// </summary>
        [TestMethod]
        public void EveryIssue_CarriesCodeAndParameterName()
        {
            var operation = new DrillPointsOperation
            {
                Holes = { new DrillHole { TotalDepth = 0 } }
            };

            var issue = operation.Validate().First(i => i.Property.StartsWith("Holes["));

            Assert.AreEqual(ValidationCode.NotPositive, issue.Code);
            Assert.AreEqual("TotalDepth", issue.ParameterName, "Имя параметра — без индекса отверстия");
        }
    }
}
