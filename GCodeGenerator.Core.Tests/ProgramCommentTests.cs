using System.IO;
using System.Linq;
using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.Models;
using GCodeGenerator.Tests.Fixtures;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Комментарии в программе.
    ///
    /// Тексты собирались строками прямо в генераторах и планировщике, и
    /// увидеть весь набор было негде. Теперь они в одном месте, а здесь
    /// проверяется то, что от них требуется: понятный вид и безопасный для
    /// стойки набор символов.
    /// </summary>
    [TestClass]
    public class ProgramCommentTests
    {
        [TestMethod]
        public void Pass_NamesNumberAndDepth()
        {
            Assert.AreEqual("Pass 3, depth -1.500", ProgramComments.Pass(3, "-1.500"));
        }

        [TestMethod]
        public void Operation_NamesOperationAndDescription()
        {
            Assert.AreEqual("Карман: Pocket circle", ProgramComments.Operation("Карман", "Pocket circle"));
        }

        /// <summary>
        /// Собственные тексты программы состоят только из ASCII: комментарий
        /// уходит в файл, который читает стойка, а многие стойки на
        /// кириллице отказываются выполнять кадр или искажают его. Язык
        /// интерфейса на содержимое программы влиять не должен.
        /// </summary>
        [TestMethod]
        public void OwnComments_AreAsciiOnly()
        {
            AssertAscii(ProgramComments.ContourTooSmall);
            AssertAscii(ProgramComments.PocketTooSmallForAllowance);
            AssertAscii(ProgramComments.Pass(12, "-3.250"));
        }

        /// <summary>
        /// Ни один эталонный файл программы не содержит символов вне ASCII:
        /// это проверка того же правила на всём выводе продукта, а не только
        /// на отдельных строках.
        /// </summary>
        [TestMethod]
        public void GoldenPrograms_ContainOnlyAsciiCharacters()
        {
            var directory = Path.Combine(
                System.AppDomain.CurrentDomain.BaseDirectory, "Golden");
            Assert.IsTrue(Directory.Exists(directory), "Нет каталога эталонных программ");

            var files = Directory.GetFiles(directory, "*.nc");
            Assert.IsTrue(files.Length > 10, "Эталонных программ должно быть много");

            foreach (var file in files)
            {
                foreach (var (line, index) in File.ReadAllLines(file).Select((line, i) => (line, i)))
                {
                    var offender = line.FirstOrDefault(symbol => symbol > 127);
                    Assert.AreEqual('\0', offender,
                        $"{Path.GetFileName(file)}, строка {index + 1}: символ вне ASCII в «{line}»");
                }
            }
        }

        /// <summary>
        /// Имя операции пользователь задаёт сам и может написать его
        /// по-русски: тогда кириллица попадёт в комментарий программы.
        /// Это его выбор и его ответственность — продукт имя не искажает,
        /// но и не добавляет кириллицу от себя.
        /// </summary>
        [TestMethod]
        public void UserOperationName_IsWrittenAsGiven()
        {
            var operation = OperationFixtures.DrillPoints();
            operation.Name = "Сверление платы";

            var program = OperationToolPath.Program(
                new DrillPointsOperationGenerator(),
                operation,
                new GCodeSettings { Format = new GCodeFormatSettings { UseComments = true } });

            Assert.IsTrue(program.Lines.Any(line => line.Contains("Сверление платы")),
                "Имя операции попадает в программу как есть");
        }

        private static void AssertAscii(string text)
        {
            var offender = text.FirstOrDefault(symbol => symbol > 127);
            Assert.AreEqual('\0', offender, $"Символ вне ASCII в «{text}»");
        }
    }
}
