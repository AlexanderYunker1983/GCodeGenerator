using System.Globalization;
using System.Linq;
using GCodeGenerator.Operations;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Описание операции не зависит от локали машины. Описание уходит не
    /// только в список окна: постпроцессор пишет его комментарием в файл
    /// программы, а одна и та же операция обязана давать один и тот же файл
    /// на любой машине. Раньше числа описаний форматировались культурой
    /// прогона — на русской локали глубина превращалась в «2,5», — а тесты
    /// этого не видели, потому что закрепляли культуру прогона вручную.
    /// Теперь культура прогонов не закрепляется, и этот тест — единственное
    /// место, где она переключается: намеренно, в обе стороны.
    /// </summary>
    [TestClass]
    public class OperationDescriptionCultureTests
    {
        /// <summary>
        /// Каждый тип каталога: описание под ru-RU совпадает с описанием под
        /// инвариантной культурой. Во все double-параметры выставляется
        /// дробное значение: целое число выглядит одинаково в любой локали
        /// и расхождения не показало бы.
        /// </summary>
        [TestMethod]
        public void GetDescription_DoesNotDependOnMachineCulture()
        {
            var original = CultureInfo.CurrentCulture;
            var fractionalSeen = false;
            try
            {
                foreach (var descriptor in OperationCatalog.All)
                {
                    var operation = descriptor.Create();
                    foreach (var property in operation.GetType().GetProperties()
                                 .Where(p => p.PropertyType == typeof(double) && p.CanWrite))
                    {
                        property.SetValue(operation, 1.5);
                    }

                    CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                    var invariant = operation.GetDescription();

                    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
                    var russian = operation.GetDescription();

                    Assert.AreEqual(invariant, russian,
                        $"{descriptor.OperationType.Name}: описание зависит от культуры прогона.");
                    fractionalSeen |= invariant.Contains("1.5");
                }
            }
            finally
            {
                CultureInfo.CurrentCulture = original;
            }

            // Страховка самой проверки: если дробные значения не дошли до
            // описаний (например, из-за смены механизма установки свойств),
            // сравнение культур сравнивало бы целые числа и всегда проходило.
            Assert.IsTrue(fractionalSeen,
                "Ни одно описание не содержит дробного числа — проверка культур ничего не отличает.");
        }
    }
}
