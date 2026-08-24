using System.Collections.Generic;
using System.Linq;
using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Юнит-тесты GCodeFormatter (пункт 4.2 плана): рендеринг структуры в
    /// текст — слова, паддинг G/M, линейные номера, видимость комментариев.
    /// Поведение должно совпадать с прежними локальными функциями
    /// FormatG/FormatM/AddLine в SimpleGCodeGenerator.
    /// </summary>
    [TestClass]
    public class GCodeFormatterTests
    {
        private static GCodeProgram Program(params GCodeBlock[] blocks)
        {
            var program = new GCodeProgram();
            foreach (var b in blocks)
                program.Blocks.Add(b);
            return program;
        }

        private static GCodeBlock Move(params GCodeWord[] words) => new GCodeBlock(words);
        private static GCodeBlock Comment(string text) => new GCodeBlock(null, text);

        // ------------------------------------------------------------------
        // Рендеринг слов
        // ------------------------------------------------------------------

        [TestMethod]
        public void GWords_Padding()
        {
            var plain = new GCodeSettings();
            var padded = new GCodeSettings { Format = new GCodeFormatSettings { UsePaddedGCodes = true } };

            Assert.AreEqual("G0", Render(new GCodeBlock(new[] { GCodeWord.G(0) }), plain));
            Assert.AreEqual("G00", Render(new GCodeBlock(new[] { GCodeWord.G(0) }), padded));
            Assert.AreEqual("G1", Render(new GCodeBlock(new[] { GCodeWord.G(1) }), plain));
            Assert.AreEqual("G01", Render(new GCodeBlock(new[] { GCodeWord.G(1) }), padded));
            Assert.AreEqual("G54", Render(new GCodeBlock(new[] { GCodeWord.G(54) }), plain));
            Assert.AreEqual("G54", Render(new GCodeBlock(new[] { GCodeWord.G(54) }), padded));
        }

        [TestMethod]
        public void MWords_Padding()
        {
            var plain = new GCodeSettings();
            var padded = new GCodeSettings { Format = new GCodeFormatSettings { UsePaddedGCodes = true } };

            Assert.AreEqual("M3", Render(new GCodeBlock(new[] { GCodeWord.M(3) }), plain));
            Assert.AreEqual("M03", Render(new GCodeBlock(new[] { GCodeWord.M(3) }), padded));
            Assert.AreEqual("M30", Render(new GCodeBlock(new[] { GCodeWord.M(30) }), plain));
            Assert.AreEqual("M30", Render(new GCodeBlock(new[] { GCodeWord.M(30) }), padded));
        }

        [TestMethod]
        public void RawWords_NeverPadded()
        {
            var padded = new GCodeSettings { Format = new GCodeFormatSettings { UsePaddedGCodes = true } };
            Assert.AreEqual("G92", Render(new GCodeBlock(new[] { GCodeWord.Raw("G92") }), padded));
            Assert.AreEqual("M30", Render(new GCodeBlock(new[] { GCodeWord.Raw("M30") }), padded));
        }

        [TestMethod]
        public void AxisWords_Decimals()
        {
            var s = new GCodeSettings();
            Assert.AreEqual("X10.500", Render(new GCodeBlock(new[] { GCodeWord.X(10.5, 3) }), s));
            Assert.AreEqual("X0.000", Render(new GCodeBlock(new[] { GCodeWord.X(0.0, 3) }), s));
            // Нормализация -0.0 (GCodeGenerationHelper.FormatNumber)
            Assert.AreEqual("Z0.000", Render(new GCodeBlock(new[] { GCodeWord.Z(-0.0, 3) }), s));
            Assert.AreEqual("Y-2.250", Render(new GCodeBlock(new[] { GCodeWord.Y(-2.25, 3) }), s));
        }

        [TestMethod]
        public void AxisWords_Plain()
        {
            var s = new GCodeSettings();
            Assert.AreEqual("X0", Render(new GCodeBlock(new[] { GCodeWord.X(0.0, -1) }), s));
            Assert.AreEqual("Z5", Render(new GCodeBlock(new[] { GCodeWord.Z(5.0, -1) }), s));
            Assert.AreEqual("X100", Render(new GCodeBlock(new[] { GCodeWord.X(100.0, -1) }), s));
        }

        [TestMethod]
        public void SpindleAndDwellWords()
        {
            var s = new GCodeSettings();
            Assert.AreEqual("M3 S12000", Render(new GCodeBlock(new[] { GCodeWord.M(3), GCodeWord.S(12000) }), s));
            Assert.AreEqual("G4 P2000", Render(new GCodeBlock(new[] { GCodeWord.G(4), GCodeWord.P(2000.0) }), s));
        }

        [TestMethod]
        public void MoveLine_WordsJoined()
        {
            var s = new GCodeSettings();
            var block = new GCodeBlock(new[]
            {
                GCodeWord.G(1), GCodeWord.X(10.0, 3), GCodeWord.Y(20.0, 3), GCodeWord.F(300.0, 3)
            });
            Assert.AreEqual("G1 X10.000 Y20.000 F300.000", Render(block, s));
        }

        [TestMethod]
        public void ArcLine_Words()
        {
            var s = new GCodeSettings();
            var block = new GCodeBlock(new[]
            {
                GCodeWord.G(2), GCodeWord.X(10.0, 3), GCodeWord.Y(0.0, 3),
                GCodeWord.I(5.0, 3), GCodeWord.J(0.0, 3), GCodeWord.F(300.0, 3)
            });
            Assert.AreEqual("G2 X10.000 Y0.000 I5.000 J0.000 F300.000", Render(block, s));
        }

        [TestMethod]
        public void CommentLine()
        {
            var s = new GCodeSettings();
            Assert.AreEqual("(Pass 1, depth -1.000)", Render(Comment("Pass 1, depth -1.000"), s));
        }

        private static string Render(GCodeBlock block, GCodeSettings settings)
        {
            settings.Format.UseLineNumbers = false; // тесты рендеринга слов без номеров строк
            var program = Program(block);
            return GCodeFormatter.Format(program, settings)[0];
        }

        // ------------------------------------------------------------------
        // Линейные номера и комментарии
        // ------------------------------------------------------------------

        [TestMethod]
        public void LineNumbers_StartAndStep()
        {
            var s = new GCodeSettings(); // start=10, step=10
            var program = Program(Comment("hdr"), Move(GCodeWord.G(54)), Move(GCodeWord.Raw("M30")));
            var lines = GCodeFormatter.Format(program, s);
            CollectionAssert.AreEqual(
                new[] { "N10 (hdr)", "N20 G54", "N30 M30" }, lines);
            Assert.AreEqual(10, program.Blocks[0].LineNumber);
            Assert.AreEqual(20, program.Blocks[1].LineNumber);
            Assert.AreEqual(30, program.Blocks[2].LineNumber);
        }

        [TestMethod]
        public void LineNumbers_Disabled()
        {
            var s = new GCodeSettings { Format = new GCodeFormatSettings { UseLineNumbers = false } };
            var program = Program(Comment("hdr"), Move(GCodeWord.G(54)));
            var lines = GCodeFormatter.Format(program, s);
            CollectionAssert.AreEqual(new[] { "(hdr)", "G54" }, lines);
            Assert.AreEqual(0, program.Blocks[0].LineNumber);
        }

        [TestMethod]
        public void LineNumbers_CustomStartStep()
        {
            var s = new GCodeSettings { Format = new GCodeFormatSettings { LineNumberStart = 5, LineNumberStep = 5 } };
            var program = Program(Move(GCodeWord.G(54)), Move(GCodeWord.Raw("M30")));
            var lines = GCodeFormatter.Format(program, s);
            CollectionAssert.AreEqual(new[] { "N5 G54", "N10 M30" }, lines);
        }

        [TestMethod]
        public void Comments_Disabled_NotNumbered()
        {
            // Legacy behavior: comment lines are not emitted and do not
            // consume line numbers.
            var s = new GCodeSettings { Format = new GCodeFormatSettings { UseComments = false } };
            var program = Program(Comment("hdr"), Move(GCodeWord.G(54)), Comment("mid"), Move(GCodeWord.Raw("M30")));
            var lines = GCodeFormatter.Format(program, s);
            CollectionAssert.AreEqual(new[] { "N10 G54", "N20 M30" }, lines);
        }

        [TestMethod]
        public void Comments_Enabled_Numbered()
        {
            var s = new GCodeSettings();
            var program = Program(Comment("hdr"), Move(GCodeWord.G(54)), Comment("mid"), Move(GCodeWord.Raw("M30")));
            var lines = GCodeFormatter.Format(program, s);
            CollectionAssert.AreEqual(
                new[] { "N10 (hdr)", "N20 G54", "N30 (mid)", "N40 M30" }, lines);
        }

        [TestMethod]
        public void ProgramLines_Populated()
        {
            var s = new GCodeSettings();
            var program = Program(Comment("hdr"), Move(GCodeWord.Raw("M30")));
            GCodeFormatter.Format(program, s);
            CollectionAssert.AreEqual(new[] { "N10 (hdr)", "N20 M30" }, program.Lines.ToList());
        }
    }
}
