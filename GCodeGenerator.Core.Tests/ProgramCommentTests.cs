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
            Assert.AreEqual(
                "Bearing seat: Pocket circle",
                ProgramComments.Operation("Bearing seat", "Pocket circle"));
        }

        /// <summary>
        /// Русское имя в программу не выводится — остаётся описание. Оно
        /// английское, собрано продуктом и называет тип операции с размерами,
        /// то есть говорит о ней то же самое.
        /// </summary>
        [TestMethod]
        public void Operation_WithNonAsciiName_KeepsOnlyTheDescription()
        {
            Assert.AreEqual(
                "Pocket circle R10mm",
                ProgramComments.Operation("Карман под подшипник", "Pocket circle R10mm"));
        }

        /// <summary>
        /// Имя отбрасывается целиком, а не по символам: «Pocket Карман» —
        /// это уже не английское имя, и половина его в листинге бесполезна.
        /// </summary>
        [TestMethod]
        public void Operation_WithPartlyNonAsciiName_KeepsOnlyTheDescription()
        {
            Assert.AreEqual(
                "Pocket circle R10mm",
                ProgramComments.Operation("Pocket Карман", "Pocket circle R10mm"));
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
        /// Теперь такое имя в программу не попадает: в комментарии остаётся
        /// английское описание операции.
        /// </summary>
        [TestMethod]
        public void RussianOperationName_DoesNotReachTheProgram()
        {
            var program = ProgramWithName("Сверление платы");

            Assert.IsFalse(program.Lines.Any(line => line.Contains("Сверление")),
                "Русское имя в программу не выводится");
            Assert.IsFalse(program.Lines.Any(line => line.Any(symbol => symbol > 127)),
                "В программе не осталось символов вне ASCII");
            Assert.IsTrue(program.Lines.Any(line => line.Contains("Drill")),
                "Вместо имени остаётся английское описание операции");
        }

        /// <summary>
        /// Английское имя выводится как есть: оно читается в листинге и
        /// помогает найти операцию в списке.
        /// </summary>
        [TestMethod]
        public void EnglishOperationName_ReachesTheProgram()
        {
            var program = ProgramWithName("Board drilling");

            Assert.IsTrue(program.Lines.Any(line => line.Contains("Board drilling")),
                "Английское имя попадает в программу");
        }

        /// <summary>Программа одной операции с заданным именем.</summary>
        /// <param name="name">Имя операции.</param>
        private static GCodeProgram ProgramWithName(string name)
        {
            var operation = OperationFixtures.DrillPoints();
            operation.Name = name;

            return OperationToolPath.Program(
                new DrillPointsOperationGenerator(),
                operation,
                new GCodeSettings { Format = new GCodeFormatSettings { UseComments = true } });
        }

        private static void AssertAscii(string text)
        {
            var offender = text.FirstOrDefault(symbol => symbol > 127);
            Assert.AreEqual('\0', offender, $"Символ вне ASCII в «{text}»");
        }
    }
}
