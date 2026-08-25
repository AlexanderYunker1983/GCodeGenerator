using System.Windows.Media;
using GCodeGenerator.Preview;
using GCodeGenerator.Views.Scene;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Цвета плоской схемы операций.
    ///
    /// Раньше они стояли литералами в отрисовке и не зависели от темы:
    /// тёмно-зелёный контур и серый холостой ход на тёмном фоне почти
    /// сливались с ним. Проверить это можно было только глазами на
    /// запущенной программе, поэтому цвета вынесены в палитру.
    /// </summary>
    [TestClass]
    public class OperationPreviewPaletteTests
    {
        private static readonly Color DarkThemeBackground = Color.FromRgb(37, 37, 37);

        [TestMethod]
        public void Background_DecidesWhetherThemeIsDark()
        {
            Assert.IsFalse(OperationPreviewPalette.ForBackground(Colors.White).IsDarkBackground);
            Assert.IsTrue(OperationPreviewPalette.ForBackground(DarkThemeBackground).IsDarkBackground);
        }

        /// <summary>
        /// На тёмном фоне линии светлее: иначе контур и холостой ход
        /// не отличить от самого фона.
        /// </summary>
        [TestMethod]
        public void DarkTheme_LightensEveryLine()
        {
            var light = OperationPreviewPalette.ForBackground(Colors.White);
            var dark = OperationPreviewPalette.ForBackground(DarkThemeBackground);

            AssertLighter(dark.Contour, light.Contour, "контур");
            AssertLighter(dark.Hole, light.Hole, "отверстие");
            AssertLighter(dark.RapidMove, light.RapidMove, "холостой ход");
            AssertLighter(dark.Grid, light.Grid, "сетка");
        }

        /// <summary>
        /// Каждая линия должна отличаться от фона по яркости: цвет, совпавший
        /// с фоном, означал бы невидимую фигуру.
        /// </summary>
        [TestMethod]
        public void EveryLine_StandsOutFromBackground()
        {
            foreach (var background in new[] { Colors.White, DarkThemeBackground, Colors.Black })
            {
                var palette = OperationPreviewPalette.ForBackground(background);
                var backgroundBrightness = Brightness(background);

                foreach (var (name, brush) in new[]
                {
                    ("отверстие", palette.Hole),
                    ("контур", palette.Contour),
                    ("рабочий ход", palette.CuttingMove),
                    ("холостой ход", palette.RapidMove),
                    ("выделение", palette.Selected),
                    ("наведение", palette.Hovered),
                    ("сетка", palette.Grid),
                })
                {
                    var difference = System.Math.Abs(Brightness(ColorOf(brush)) - backgroundBrightness);
                    Assert.IsTrue(difference > 0.15,
                        $"{name} на фоне {background}: разница яркости {difference:F2} слишком мала");
                }
            }
        }

        /// <summary>
        /// По цвету читают, что изображено, поэтому роли различаются между
        /// собой: отверстие, контур и холостой ход не должны совпадать.
        /// </summary>
        [TestMethod]
        public void ShapeRoles_HaveDistinctColors()
        {
            var palette = OperationPreviewPalette.ForBackground(Colors.White);

            var hole = ColorOf(palette.ForShape(OperationShapeKind.Point));
            var contour = ColorOf(palette.ForShape(OperationShapeKind.Contour));
            var rapid = ColorOf(palette.ForShape(OperationShapeKind.RapidMove));

            Assert.AreNotEqual(hole, contour);
            Assert.AreNotEqual(contour, rapid);
            Assert.AreNotEqual(hole, rapid);
        }

        /// <summary>
        /// Выделенная операция остаётся самой заметной: её цвет отличается
        /// от цвета любой невыделенной фигуры.
        /// </summary>
        [TestMethod]
        public void Selection_DiffersFromEveryShapeColor()
        {
            foreach (var background in new[] { Colors.White, DarkThemeBackground })
            {
                var palette = OperationPreviewPalette.ForBackground(background);
                var selected = ColorOf(palette.Selected);

                Assert.AreNotEqual(selected, ColorOf(palette.Hole), background.ToString());
                Assert.AreNotEqual(selected, ColorOf(palette.Contour), background.ToString());
                Assert.AreNotEqual(selected, ColorOf(palette.RapidMove), background.ToString());
                Assert.AreNotEqual(selected, ColorOf(palette.Hovered), background.ToString());
            }
        }

        private static void AssertLighter(Brush onDark, Brush onLight, string what)
        {
            Assert.IsTrue(Brightness(ColorOf(onDark)) > Brightness(ColorOf(onLight)),
                $"{what}: на тёмном фоне цвет должен быть светлее");
        }

        private static Color ColorOf(Brush brush) => ((SolidColorBrush)brush).Color;

        private static double Brightness(Color color)
            => (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255.0;
    }
}
