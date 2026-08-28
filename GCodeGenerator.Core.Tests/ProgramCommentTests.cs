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
        /// по-русски. Прежде оно уходило в программу как есть: считалось, что
        /// это его выбор и его ответственность. Выбор, однако, делался вслепую
        /// — узнать, что стойка не принимает кириллицу, можно было только
        /// у станка, и ни окно, ни документация об этом не предупреждали.
        ///
        /// Теперь по умолчанию имя переводится в латиницу: смысл сохраняется,
        /// а кадр остаётся тем, что стойка заведомо прочитает.
        /// </summary>
        [TestMethod]
        public void UserOperationName_IsTransliteratedByDefault()
        {
            var program = ProgramWithName("Сверление платы", new GCodeFormatSettings());

            Assert.IsTrue(program.Lines.Any(line => line.Contains("Sverlenie platy")),
                "Имя операции переводится в латиницу");
            Assert.IsFalse(program.Lines.Any(line => line.Any(symbol => symbol > 127)),
                "В программе не осталось символов вне ASCII");
        }

        /// <summary>
        /// Прежнее поведение никуда не делось — оно стало выбором: стойки,
        /// читающие UTF-8, получают имя ровно таким, каким его написали.
        /// </summary>
        [TestMethod]
        public void UserOperationName_IsWrittenAsGiven_WhenAsciiIsNotRequired()
        {
            var program = ProgramWithName(
                "Сверление платы", new GCodeFormatSettings { AsciiOnlyComments = false });

            Assert.IsTrue(program.Lines.Any(line => line.Contains("Сверление платы")),
                "Без требования латиницы имя попадает в программу как есть");
        }

        /// <summary>Программа одной операции с заданным именем.</summary>
        /// <param name="name">Имя операции.</param>
        /// <param name="format">Настройки вывода.</param>
        private static GCodeProgram ProgramWithName(string name, GCodeFormatSettings format)
        {
            var operation = OperationFixtures.DrillPoints();
            operation.Name = name;
            format.UseComments = true;

            return OperationToolPath.Program(
                new DrillPointsOperationGenerator(),
                operation,
                new GCodeSettings { Format = format });
        }

        private static void AssertAscii(string text)
        {
            var offender = text.FirstOrDefault(symbol => symbol > 127);
            Assert.AreEqual('\0', offender, $"Символ вне ASCII в «{text}»");
        }
    }
}
