using System.Linq;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using GCodeGenerator.Tests.Fixtures;
using GCodeGenerator.Views;
using GCodeGenerator.Views.Scene;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Отрисовка плоского предпросмотра разделена по стоимости: выделение и
    /// наведение перекрашивают готовые фигуры, и только изменение данных
    /// собирает слой заново. Прежде каждое движение мыши очищало холст и
    /// строило все фигуры и сетку с нуля — на большом проекте предпросмотр
    /// заметно вязнул.
    /// </summary>
    [TestClass]
    [SupportedOSPlatform("windows")]
    public class PreviewRenderingTests
    {
        /// <summary>
        /// Одно окно на все проверки: приложение WPF существует в единственном
        /// экземпляре на весь прогон (см. WindowContentReachableTests).
        /// </summary>
        [TestMethod]
        public void SelectionRecolorsInPlace_DataChangeRebuilds()
        {
            TestApplication.Run(() =>
            {
                var (main, _, _, _) = MainViewModelOperationEditTests.CreateMain();
                var profile = OperationFixtures.ProfileCircle();
                main.OperationsWorkspace.AllOperations.Add(profile);
                main.OperationsWorkspace.AllOperations.Add(OperationFixtures.DrillPoints());

                var view = new OperationsPreviewView
                {
                    DataContext = main.OperationsWorkspace.OperationsPreview,
                };
                var window = new Window
                {
                    Content = view,
                    Width = 500,
                    Height = 400,
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    ShowInTaskbar = false,
                    Left = -10000,
                    Top = -10000,
                };
                try
                {
                    window.Show();
                    window.UpdateLayout();
                    window.Dispatcher.Invoke(() => { }, DispatcherPriority.Loaded);

                    var canvas = (Canvas)view.FindName("PreviewCanvas");
                    Assert.IsNotNull(canvas, "Холст найден");

                    // Постоянные слои: сетка-прямоугольник, две оси и слой фигур.
                    Assert.AreEqual(4, canvas.Children.Count,
                        "Состав холста постоянен: сетка, оси и слой фигур");
                    var shapesLayer = canvas.Children.OfType<Canvas>().Single();
                    var before = shapesLayer.Children.Cast<UIElement>().ToArray();
                    Assert.IsTrue(before.Length > 0, "Фигуры операций построены");

                    // Выделение перекрашивает фигуры на месте: экземпляры те же.
                    main.OperationsWorkspace.OperationsPreview.SelectedOperation = profile;
                    var after = shapesLayer.Children.Cast<UIElement>().ToArray();
                    CollectionAssert.AreEqual(before, after,
                        "Смена выделения не пересоздаёт фигуры");

                    var palette = OperationPreviewPalette.ForCurrentTheme();
                    var selectedColor = ((SolidColorBrush)palette.Selected).Color;
                    var selectedOutline = after.OfType<Polyline>()
                        .Where(line => ReferenceEquals(line.Tag, profile))
                        .ToList();
                    Assert.IsTrue(selectedOutline.Count > 0, "У выделенной операции есть контур");
                    Assert.IsTrue(
                        selectedOutline.All(line => ((SolidColorBrush)line.Stroke).Color == selectedColor),
                        "Контур выделенной операции покрашен цветом выделения");

                    // Изменение данных собирает слой заново: фигур становится больше.
                    main.OperationsWorkspace.AllOperations.Add(OperationFixtures.PocketCircle());
                    Assert.IsTrue(shapesLayer.Children.Count > before.Length,
                        "Новая операция достроила слой фигур");
                }
                finally
                {
                    window.Close();
                }
            });
        }
    }
}
