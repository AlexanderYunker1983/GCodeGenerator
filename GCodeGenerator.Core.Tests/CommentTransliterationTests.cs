using GCodeGenerator.GCodeGenerators;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Перевод комментария в латиницу.
    ///
    /// Смысл имени должен пережить перевод: оператор станка читает комментарий
    /// и узнаёт по нему операцию. Поэтому проверяется не соответствие какому-то
    /// стандарту транслитерации, а то, что имя остаётся узнаваемым и в кадре
    /// не остаётся ни одного символа, на котором стойка споткнётся.
    /// </summary>
    [TestClass]
    public class CommentTransliterationTests
    {
        [TestMethod]
        [DataRow("Сверление платы", "Sverlenie platy")]
        [DataRow("Карман под подшипник", "Karman pod podshipnik")]
        [DataRow("Черновая выборка", "Chernovaya vyborka")]
        [DataRow("Щуп", "Shchup")]
        [DataRow("Объезд контура", "Obezd kontura")]
        [DataRow("Ёлочка", "Elochka")]
        public void RussianName_BecomesReadableLatin(string source, string expected)
        {
            Assert.AreEqual(expected, CommentTransliteration.ToAscii(source));
        }

        /// <summary>
        /// Слово целиком заглавными остаётся таким же: «ШАГ» — это «SHAG»,
        /// а не «ShAG».
        /// </summary>
        [TestMethod]
        [DataRow("ШАГ", "SHAG")]
        [DataRow("Шаг", "Shag")]
        [DataRow("ЧЕРНОВАЯ ОБРАБОТКА", "CHERNOVAYA OBRABOTKA")]
        public void UpperCase_StaysUpperCase(string source, string expected)
        {
            Assert.AreEqual(expected, CommentTransliteration.ToAscii(source));
        }

        /// <summary>
        /// Латиница, цифры и знаки не трогаются: имя, написанное по-английски,
        /// проходит через перевод без единого изменения.
        /// </summary>
        [TestMethod]
        [DataRow("Pocket Circle R10 (rough)")]
        [DataRow("Drill 12 holes, step 2.5")]
        [DataRow("")]
        public void AsciiText_IsUnchanged(string source)
        {
            Assert.AreEqual(source, CommentTransliteration.ToAscii(source));
        }

        /// <summary>
        /// Диакритика снимается: для немецких, французских и польских имён
        /// отдельной таблицы нет, но и вопросительных знаков они не заслужили.
        /// </summary>
        [TestMethod]
        [DataRow("Führung", "Fuhrung")]
        [DataRow("Précision", "Precision")]
        [DataRow("Łożysko", "?ozysko")]
        public void Diacritics_AreStripped(string source, string expected)
        {
            Assert.AreEqual(expected, CommentTransliteration.ToAscii(source));
        }

        /// <summary>
        /// Технические знаки сохраняют смысл: диаметр, градус и номер стоят
        /// в именах операций там, где важнее всего, и вопросительный знак
        /// на их месте обесценил бы весь комментарий.
        /// </summary>
        [TestMethod]
        [DataRow("Отверстие Ø8", "Otverstie D8")]
        [DataRow("Фаска 45°", "Faska 45deg")]
        [DataRow("Карман №3", "Karman No3")]
        [DataRow("Плита 40×20", "Plita 40x20")]
        [DataRow("Допуск ±0.05", "Dopusk +/-0.05")]
        [DataRow("Чистовая — стенки", "Chistovaya - stenki")]
        public void TechnicalSymbols_KeepTheirMeaning(string source, string expected)
        {
            Assert.AreEqual(expected, CommentTransliteration.ToAscii(source));
        }

        /// <summary>
        /// То, для чего замены нет, становится вопросительным знаком: потерю
        /// лучше видеть в программе, чем не заметить.
        /// </summary>
        [TestMethod]
        public void UntranslatableCharacters_BecomeQuestionMarks()
        {
            Assert.AreEqual("Karman ?", CommentTransliteration.ToAscii("Карман 孔"));
        }

        /// <summary>
        /// Главное свойство: что бы ни пришло на вход, на выходе только ASCII.
        /// </summary>
        [TestMethod]
        [DataRow("Сверление отверстий Ø8 под М10")]
        [DataRow("Обработка «начисто» — стенки и дно")]
        [DataRow("Ярлык: ½ прохода, 20°")]
        public void Result_IsAlwaysAscii(string source)
        {
            var result = CommentTransliteration.ToAscii(source);

            foreach (var symbol in result)
                Assert.IsTrue(symbol < 128, $"Символ вне ASCII: «{symbol}» в «{result}»");
        }

        [TestMethod]
        public void NullText_BecomesEmpty()
        {
            Assert.AreEqual(string.Empty, CommentTransliteration.ToAscii(null));
        }
    }
}
