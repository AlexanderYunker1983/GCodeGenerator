using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using GCodeGenerator.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Ошибки параметров видны прямо у полей окна.
    ///
    /// Прежде о неверном значении пользователь узнавал только при генерации —
    /// одним сообщением со списком проблем всех операций сразу, без связи
    /// с конкретным полем, хотя имя параметра в проблеме уже было. Теперь
    /// операция сообщает об ошибках как источник привязки, и окно показывает
    /// их у того поля, к которому они относятся.
    /// </summary>
    [TestClass]
    public class FieldErrorTests
    {
        [TestCleanup]
        public void RestoreFormatter() => ValidationMessages.Formatter = null;

        private static List<string> ErrorsOf(INotifyDataErrorInfo source, string property)
            => (source.GetErrors(property) ?? Enumerable.Empty<object>())
                .Cast<object>()
                .Select(e => e?.ToString())
                .ToList();

        [TestMethod]
        public void InvalidParameter_ReportsErrorOnItsOwnField()
        {
            var operation = new PocketCircleOperation { Radius = 10 };
            Assert.IsFalse(operation.HasErrors, "Исправная операция ошибок не показывает");

            operation.StepDepth = 0;

            Assert.IsTrue(operation.HasErrors, "Операция сообщает об ошибке");
            Assert.AreEqual(1, ErrorsOf(operation, nameof(operation.StepDepth)).Count,
                "Ошибка привязана к своему полю");
            Assert.AreEqual(0, ErrorsOf(operation, nameof(operation.Radius)).Count,
                "Соседнее поле чистое");
        }

        [TestMethod]
        public void FixingParameter_ClearsTheError()
        {
            var operation = new PocketCircleOperation { Radius = 0 };
            Assert.IsTrue(operation.HasErrors);

            operation.Radius = 12;

            Assert.IsFalse(operation.HasErrors, "Исправленное значение снимает ошибку");
            Assert.AreEqual(0, ErrorsOf(operation, nameof(operation.Radius)).Count);
        }

        /// <summary>
        /// Правка любого параметра пересматривает весь список: одно значение
        /// способно сделать недопустимым другое.
        /// </summary>
        [TestMethod]
        public void AnyChange_RaisesErrorsChanged()
        {
            var operation = new PocketCircleOperation();
            var raised = 0;
            operation.ErrorsChanged += (_, _) => raised++;

            operation.Radius = 5;
            operation.ToolDiameter = 2;

            Assert.AreEqual(2, raised, "Об изменении списка ошибок сообщается на каждую правку");
        }

        /// <summary>
        /// Ошибка отверстия относится к колонке таблицы: имя параметра
        /// не должно тащить за собой индекс строки.
        /// </summary>
        [TestMethod]
        public void HoleError_IsReportedOnTheColumnName()
        {
            var operation = new DrillPointsOperation
            {
                Holes = { new DrillHole { TotalDepth = 0 } }
            };

            Assert.IsTrue(ErrorsOf(operation, "TotalDepth").Count > 0,
                "Ошибка глубины отверстия видна у колонки глубины");
        }

        /// <summary>
        /// Текст ошибки подставляет приложение: домен знает код проблемы,
        /// но не язык окна.
        /// </summary>
        [TestMethod]
        public void ErrorText_ComesFromTheApplication()
        {
            ValidationMessages.Formatter = issue => $"перевод:{issue.Code}";
            var operation = new PocketCircleOperation { Radius = 0 };

            var errors = ErrorsOf(operation, nameof(operation.Radius));

            Assert.AreEqual(1, errors.Count);
            Assert.AreEqual($"перевод:{ValidationCode.NotPositive}", errors[0]);
        }

        [TestMethod]
        public void WithoutTranslation_EnglishTextRemains()
        {
            ValidationMessages.Formatter = null;
            var operation = new PocketCircleOperation { Radius = 0 };

            var errors = ErrorsOf(operation, nameof(operation.Radius));

            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains(errors[0], "greater than zero", "Без перевода остаётся текст для журнала");
        }

        /// <summary>
        /// Пустое имя параметра означает «все ошибки операции» — так WPF
        /// спрашивает об ошибках объекта целиком.
        /// </summary>
        [TestMethod]
        public void EmptyPropertyName_ReturnsEveryError()
        {
            var operation = new PocketCircleOperation { Radius = 0 };
            operation.StepDepth = 0;

            Assert.IsTrue(ErrorsOf(operation, null).Count >= 2, "Спрошены все ошибки сразу");
        }

        /// <summary>
        /// Список ошибок пересчитывается не на каждый запрос: проверка идёт
        /// по всей операции, а окно спрашивает о ней по полю за раз.
        /// </summary>
        [TestMethod]
        public void ErrorList_IsComputedOncePerChange()
        {
            var operation = new CountingOperation();

            _ = operation.HasErrors;
            _ = operation.GetErrors(nameof(CountingOperation.Radius));
            _ = operation.GetErrors(nameof(CountingOperation.StepDepth));

            Assert.AreEqual(1, operation.ValidateCalls, "Пока операция не менялась, проверка выполняется однажды");

            operation.Radius = 3;
            _ = operation.HasErrors;

            Assert.AreEqual(2, operation.ValidateCalls, "После правки список пересчитывается заново");
        }

        /// <summary>Операция, считающая обращения к проверке.</summary>
        private sealed class CountingOperation : PocketCircleOperation, IValidatable
        {
            public int ValidateCalls { get; private set; }

            public new IReadOnlyList<ValidationIssue> Validate()
            {
                ValidateCalls++;
                return Array.Empty<ValidationIssue>();
            }
        }
    }
}
