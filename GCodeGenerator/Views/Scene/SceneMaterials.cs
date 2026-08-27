#nullable enable
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace GCodeGenerator.Views.Scene
{
    /// <summary>
    /// Палитра трёхмерной сцены.
    ///
    /// Цвета типов перемещений постоянны — по ним читают программу, и менять
    /// их вместе с темой нельзя. А вот то, что раньше рисовалось белым (начало
    /// координат) и тёмно-синим (ось Z), на светлом и тёмном фоне видно
    /// по-разному, поэтому эти цвета выводятся из фона окна: 3D-превью
    /// перестаёт быть единственным местом приложения, не знающим о теме.
    /// </summary>
    internal sealed class SceneMaterials
    {
        /// <summary>Ключ цвета фона темы MahApps.</summary>
        private const string ThemeBackgroundKey = "MahApps.Colors.ThemeBackground";

        /// <summary>Доля осветления цветов осей на тёмном фоне.</summary>
        private const double AxisLightening = 0.35;

        private SceneMaterials(Color background)
        {
            Background = background;
            BackgroundBrush = new SolidColorBrush(background);
            IsDarkBackground = Brightness(background) < 0.5;

            Rapid = Diffuse(Color.FromArgb(180, 100, 100, 255));
            Linear = Glowing(Color.FromRgb(220, 50, 50), Color.FromArgb(80, 255, 0, 0));
            ArcCW = Glowing(Color.FromRgb(255, 140, 0), Color.FromArgb(60, 255, 140, 0));
            ArcCCW = Glowing(Color.FromRgb(180, 200, 0), Color.FromArgb(60, 180, 200, 0));

            XAxis = Diffuse(ForBackgroundContrast(Color.FromRgb(200, 0, 0)));
            YAxis = Diffuse(ForBackgroundContrast(Color.FromRgb(0, 180, 0)));
            ZAxis = Diffuse(ForBackgroundContrast(Color.FromRgb(0, 80, 220)));

            var gridColor = IsDarkBackground
                ? Color.FromRgb(105, 105, 105)
                : Color.FromRgb(195, 195, 195);
            var gridLabelColor = IsDarkBackground
                ? Color.FromRgb(205, 205, 205)
                : Color.FromRgb(85, 85, 85);
            GridLines = Glowing(gridColor, Color.FromArgb(55, gridColor.R, gridColor.G, gridColor.B));
            GridLabels = Glowing(gridLabelColor,
                Color.FromArgb(90, gridLabelColor.R, gridLabelColor.G, gridLabelColor.B));

            // Начало координат и маркеры точек лежат поверх фона, поэтому
            // берут противоположную ему яркость.
            Origin = Diffuse(IsDarkBackground ? Colors.White : Color.FromRgb(40, 40, 40));
            StartMarker = Diffuse(IsDarkBackground ? Colors.LimeGreen : Color.FromRgb(0, 150, 0));
            EndMarker = Diffuse(IsDarkBackground ? Colors.Red : Color.FromRgb(200, 0, 0));
            TransitionMarker = Diffuse(IsDarkBackground ? Colors.Yellow : Color.FromRgb(200, 160, 0));
        }

        /// <summary>Палитра для заданного цвета фона.</summary>
        public static SceneMaterials ForBackground(Color background) => new SceneMaterials(background);

        /// <summary>
        /// Палитра для темы, действующей в приложении. Вне приложения
        /// (в тестах и конструкторе разметки) берётся светлый фон.
        /// </summary>
        public static SceneMaterials ForCurrentTheme()
        {
            var resource = Application.Current?.TryFindResource(ThemeBackgroundKey);
            return ForBackground(resource is Color color ? color : Colors.White);
        }

        /// <summary>Цвет фона, от которого построена палитра.</summary>
        public Color Background { get; }

        /// <summary>Кисть фона окна превью.</summary>
        public Brush BackgroundBrush { get; }

        /// <summary>Тёмная тема: фон темнее середины шкалы яркости.</summary>
        public bool IsDarkBackground { get; }

        /// <summary>Холостые перемещения.</summary>
        public Material Rapid { get; }

        /// <summary>Рабочие линейные перемещения.</summary>
        public Material Linear { get; }

        /// <summary>Дуги по часовой стрелке.</summary>
        public Material ArcCW { get; }

        /// <summary>Дуги против часовой стрелки.</summary>
        public Material ArcCCW { get; }

        /// <summary>Ось X.</summary>
        public Material XAxis { get; }

        /// <summary>Ось Y.</summary>
        public Material YAxis { get; }

        /// <summary>Ось Z.</summary>
        public Material ZAxis { get; }

        /// <summary>Тонкие линии координатных плоскостей.</summary>
        public Material GridLines { get; }

        /// <summary>Числовые отметки координатных плоскостей.</summary>
        public Material GridLabels { get; }

        /// <summary>Начало координат.</summary>
        public Material Origin { get; }

        /// <summary>Первая точка программы.</summary>
        public Material StartMarker { get; }

        /// <summary>Последняя точка программы.</summary>
        public Material EndMarker { get; }

        /// <summary>Точка смены типа перемещения.</summary>
        public Material TransitionMarker { get; }

        /// <summary>Рассеянный свет сцены.</summary>
        public Color Ambient { get; } = Color.FromRgb(80, 80, 80);

        private static Material Diffuse(Color color) => new DiffuseMaterial(new SolidColorBrush(color));

        /// <summary>Матовый цвет с подсветкой — так линия видна и в тени.</summary>
        private static Material Glowing(Color color, Color glow)
        {
            var material = new MaterialGroup();
            material.Children.Add(new DiffuseMaterial(new SolidColorBrush(color)));
            material.Children.Add(new EmissiveMaterial(new SolidColorBrush(glow)));
            return material;
        }

        /// <summary>На тёмном фоне цвет осветляется, на светлом остаётся прежним.</summary>
        private Color ForBackgroundContrast(Color color)
            => IsDarkBackground ? Lighten(color, AxisLightening) : color;

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
