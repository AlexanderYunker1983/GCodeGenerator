using System.Windows;
using System.Windows.Media;
using GCodeGenerator.Preview;

namespace GCodeGenerator.Views.Scene
{
    /// <summary>
    /// Цвета плоской схемы операций.
    ///
    /// Прежде они стояли литералами прямо в отрисовке: тёмно-зелёный контур,
    /// стально-синие отверстия, серый холостой ход. На тёмной теме такие
    /// цвета теряются — ровно так же, как терялись ось Z и ноль детали в
    /// трёхмерном окне, пока и там цвета не начали строиться от фона.
    ///
    /// Роль цвета при этом остаётся прежней на любой теме: по нему читают,
    /// что перед глазами — отверстие, контур, рабочий ход или переход, — и
    /// выделение остаётся самым заметным. Меняется только светлота, чтобы
    /// линия не сливалась с фоном.
    /// </summary>
    internal sealed class OperationPreviewPalette
    {
        /// <summary>Ключ цвета фона темы MahApps.</summary>
        private const string ThemeBackgroundKey = "MahApps.Colors.ThemeBackground";

        /// <summary>Доля осветления цветов на тёмном фоне.</summary>
        private const double Lightening = 0.4;

        private OperationPreviewPalette(Color background)
        {
            Background = background;
            IsDarkBackground = Brightness(background) < 0.5;

            Hole = Brush(Color.FromRgb(70, 130, 180));
            Contour = Brush(Color.FromRgb(0, 100, 0));
            CuttingMove = Brush(Color.FromRgb(0, 100, 0));
            RapidMove = Brush(Color.FromRgb(112, 128, 144));

            // Выделенная и наведённая операции читаются первыми, поэтому
            // их цвета остаются насыщенными на любом фоне.
            Selected = Brush(IsDarkBackground ? Color.FromRgb(255, 80, 80) : Color.FromRgb(220, 0, 0));
            Hovered = Brush(IsDarkBackground ? Color.FromRgb(255, 190, 60) : Color.FromRgb(230, 130, 0));

            Grid = Brush(IsDarkBackground ? Color.FromRgb(150, 150, 150) : Color.FromRgb(110, 110, 110));
        }

        /// <summary>Палитра для заданного цвета фона.</summary>
        public static OperationPreviewPalette ForBackground(Color background)
            => new OperationPreviewPalette(background);

        /// <summary>
        /// Палитра для темы, действующей в приложении. Вне приложения
        /// (в тестах и конструкторе разметки) берётся светлый фон.
        /// </summary>
        public static OperationPreviewPalette ForCurrentTheme()
        {
            var resource = Application.Current?.TryFindResource(ThemeBackgroundKey);
            return ForBackground(resource is Color color ? color : Colors.White);
        }

        /// <summary>Цвет фона, от которого построена палитра.</summary>
        public Color Background { get; }

        /// <summary>Тёмная тема: фон темнее середины шкалы яркости.</summary>
        public bool IsDarkBackground { get; }

        /// <summary>Отверстия сверления.</summary>
        public Brush Hole { get; }

        /// <summary>Контуры профилей и карманов.</summary>
        public Brush Contour { get; }

        /// <summary>Рабочий ход траектории.</summary>
        public Brush CuttingMove { get; }

        /// <summary>Холостой переход траектории.</summary>
        public Brush RapidMove { get; }

        /// <summary>Выделенная операция.</summary>
        public Brush Selected { get; }

        /// <summary>Операция под курсором.</summary>
        public Brush Hovered { get; }

        /// <summary>Координатная сетка.</summary>
        public Brush Grid { get; }

        /// <summary>Цвет фигуры по её роли на схеме.</summary>
        /// <param name="kind">Что изображает фигура.</param>
        public Brush ForShape(OperationShapeKind kind)
        {
            switch (kind)
            {
                case OperationShapeKind.Point:
                    return Hole;
                case OperationShapeKind.CuttingMove:
                    return CuttingMove;
                case OperationShapeKind.RapidMove:
                    return RapidMove;
                default:
                    return Contour;
            }
        }

        /// <summary>
        /// Кисть цвета, приведённого к фону: на тёмной теме светлее, на
        /// светлой — как задан.
        /// </summary>
        private Brush Brush(Color color)
        {
            var adjusted = IsDarkBackground ? Lighten(color, Lightening) : color;
            var brush = new SolidColorBrush(adjusted);
            brush.Freeze();
            return brush;
        }

        private static Color Lighten(Color color, double amount)
            => Color.FromRgb(
                (byte)(color.R + (255 - color.R) * amount),
                (byte)(color.G + (255 - color.G) * amount),
                (byte)(color.B + (255 - color.B) * amount));

        /// <summary>Воспринимаемая яркость цвета в диапазоне [0; 1].</summary>
        private static double Brightness(Color color)
            => (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255.0;
    }
}
