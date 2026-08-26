#nullable enable
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Shapes;
using GCodeGenerator.Models;
using GCodeGenerator.Preview;
using GCodeGenerator.ViewModels;
using GCodeGenerator.Views.Scene;

namespace GCodeGenerator.Views
{
    /// <summary>
    /// 2D preview of operations (plan item 6.3). The code-behind only
    /// renders the pure <see cref="OperationScene"/> from
    /// <see cref="OperationsPreviewViewModel"/> and handles the mouse;
    /// contour point generation lives in Core (<see cref="OperationSceneBuilder"/>).
    ///
    /// Отрисовка разделена по стоимости: фигуры собираются заново только
    /// когда меняется их состав или масштаб — сцена, зум, тема, — а пан
    /// сдвигает готовый слой трансформацией, сетку — вьюпортом плиточной
    /// кисти, и наведение с выделением перекрашивают фигуры на месте.
    /// Прежде каждое движение мыши очищало холст и строило всё заново.
    /// </summary>
    public partial class OperationsPreviewView : System.Windows.Controls.UserControl
    {
        private OperationsPreviewViewModel? _vm;
        private double _zoom = 5.0; // pixels per mm
        private Point _offset;
        private bool _isPanning;
        private Point _lastMouse;
        private const double GridStepMm = 10.0;
        private const double FitPadding = 0.75; // 75% of available size
        private OperationBase? _hoverOp;

        /// <summary>
        /// Цвета схемы для действующей темы. Пересобираются при её смене:
        /// на тёмном фоне линии светлее, иначе они с ним сливаются.
        /// </summary>
        private OperationPreviewPalette _palette = OperationPreviewPalette.ForCurrentTheme();

        /// <summary>Пан готового слоя фигур: сдвиг вместо пересборки.</summary>
        private readonly TranslateTransform _panTransform = new TranslateTransform();

        /// <summary>Слой фигур; его дети собраны при пане <see cref="_builtOffset"/>.</summary>
        private readonly Canvas _shapesLayer = new Canvas();

        /// <summary>Сетка — плиточная кисть: пан двигает её вьюпорт.</summary>
        private readonly Rectangle _gridRect = new Rectangle { IsHitTestVisible = false, Opacity = 0.6 };

        private readonly Line _axisX = new Line { StrokeThickness = 1, IsHitTestVisible = false };
        private readonly Line _axisY = new Line { StrokeThickness = 1, IsHitTestVisible = false };

        private DrawingBrush? _gridBrush;

        /// <summary>Пан на момент сборки слоя фигур.</summary>
        private Point _builtOffset;

        /// <summary>Фигуры сцены и их визуальные элементы — для перекраски на месте.</summary>
        private readonly List<(OperationShape Shape, Shape[] Visuals)> _visuals =
            new List<(OperationShape, Shape[])>();

        public OperationsPreviewView()
        {
            InitializeComponent();

            // Постоянные слои: сетка и оси под фигурами. Состав детей холста
            // больше не меняется — меняются дети слоя фигур.
            _shapesLayer.RenderTransform = _panTransform;
            PreviewCanvas.Children.Add(_gridRect);
            PreviewCanvas.Children.Add(_axisX);
            PreviewCanvas.Children.Add(_axisY);
            PreviewCanvas.Children.Add(_shapesLayer);

            Loaded += OnLoaded;
            DataContextChanged += OnDataContextChanged;
        }

        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            _offset = new Point(PreviewCanvas.ActualWidth / 2.0, PreviewCanvas.ActualHeight / 2.0);
            HookVm();
            RebuildScene();
        }

        private void OnDataContextChanged(object? sender, DependencyPropertyChangedEventArgs e)
        {
            HookVm();
            RebuildScene();
        }

        private void HookVm()
        {
            var viewModel = DataContext as OperationsPreviewViewModel;
            if (ReferenceEquals(viewModel, _vm))
                return; // уже подписаны: разметка задаёт источник до загрузки элемента

            UnhookVm();
            _vm = viewModel;
            if (_vm != null)
            {
                _vm.PropertyChanged += OnVmPropertyChanged;
                _vm.ShowAllRequested += OnShowAllRequested;
                // Пункт 7.5 плана: тема — через VM (ранее статический ThemeHelper).
                _vm.ThemeChanged += OnThemeChanged;
            }
        }

        private void UnhookVm()
        {
            if (_vm != null)
            {
                _vm.PropertyChanged -= OnVmPropertyChanged;
                _vm.ShowAllRequested -= OnShowAllRequested;
                _vm.ThemeChanged -= OnThemeChanged;
            }
            _vm = null;
        }

        private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(OperationsPreviewViewModel.Scene))
            {
                RebuildScene();
            }
            else if (e.PropertyName == nameof(OperationsPreviewViewModel.SelectedOperation))
            {
                // Состав фигур прежний — меняется только их цвет.
                UpdateBrushes();
            }
        }

        private void OnShowAllRequested(object? sender, EventArgs e)
        {
            FitAll();
        }

        private OperationBase? GetOperationFromSource(object source)
        {
            var fe = source as FrameworkElement;
            while (fe != null)
            {
                if (fe.Tag is OperationBase op)
                    return op;
                fe = VisualTreeHelper.GetParent(fe) as FrameworkElement;
            }
            return null;
        }

        /// <summary>Fits the view to the scene bounds.</summary>
        public void FitAll()
        {
            if (_vm == null || PreviewCanvas == null || PreviewCanvas.ActualWidth < 1 || PreviewCanvas.ActualHeight < 1)
                return;

            var bounds = _vm.Scene?.Bounds;
            if (bounds == null)
                return;

            var (minX, minY, maxX, maxY) = bounds.Value;

            var width = maxX - minX;
            var height = maxY - minY;
            if (width < 1e-6) width = 1;
            if (height < 1e-6) height = 1;

            var scaleX = (PreviewCanvas.ActualWidth * FitPadding) / width;
            var scaleY = (PreviewCanvas.ActualHeight * FitPadding) / height;
            _zoom = Math.Min(scaleX, scaleY);

            _offset = new Point(
                PreviewCanvas.ActualWidth / 2.0 - (minX + maxX) / 2.0 * _zoom,
                PreviewCanvas.ActualHeight / 2.0 + (minY + maxY) / 2.0 * _zoom);

            RebuildScene();
        }

        private void PreviewCanvas_OnMouseWheel(object? sender, MouseWheelEventArgs e)
        {
            if (PreviewCanvas.ActualWidth < 1 || PreviewCanvas.ActualHeight < 1) return;

            var mousePos = e.GetPosition(PreviewCanvas);
            var worldBefore = ScreenToWorld(mousePos);

            var factor = e.Delta > 0 ? 1.1 : 1.0 / 1.1;
            _zoom = Math.Max(0.1, Math.Min(200, _zoom * factor));

            var worldAfter = worldBefore;
            var screenAfter = WorldToScreen(worldAfter);
            var dx = mousePos.X - screenAfter.X;
            var dy = mousePos.Y - screenAfter.Y;
            _offset = new Point(_offset.X + dx, _offset.Y + dy);

            // Зум меняет экранные координаты всех точек — слой собирается заново.
            RebuildScene();
        }

        private void PreviewCanvas_OnMouseDown(object? sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                _isPanning = true;
                _lastMouse = e.GetPosition(PreviewCanvas);
                PreviewCanvas.CaptureMouse();
            }
        }

        private void PreviewCanvas_OnMouseLeftButtonDown(object? sender, MouseButtonEventArgs e)
        {
            if (_vm == null) return;

            var op = GetOperationFromSource(e.OriginalSource);
            if (op == null) return;

            _vm.SelectedOperation = op;

            // DoD фазы 7: редактирование — через событие VM (без ссылки на MainViewModel);
            // проверку CanExecute выполняет MainViewModel (SelectedOperation != null).
            if (e.ClickCount >= 2)
            {
                _vm.RequestEdit();
            }
        }

        private void PreviewCanvas_OnMouseMove(object? sender, MouseEventArgs e)
        {
            if (_isPanning && e.LeftButton == MouseButtonState.Pressed)
            {
                var pos = e.GetPosition(PreviewCanvas);
                var delta = pos - _lastMouse;
                _offset = new Point(_offset.X + delta.X, _offset.Y + delta.Y);
                _lastMouse = pos;

                // Пан не пересобирает фигуры: слой сдвигается трансформацией,
                // сетка — вьюпортом кисти, оси — двумя координатами.
                UpdateViewport();
            }
            else
            {
                var op = GetOperationFromSource(e.OriginalSource);
                if (!ReferenceEquals(op, _hoverOp))
                {
                    _hoverOp = op;
                    UpdateBrushes();
                }
            }
        }

        private void PreviewCanvas_OnMouseUp(object? sender, MouseButtonEventArgs e)
        {
            _isPanning = false;
            PreviewCanvas.ReleaseMouseCapture();
        }

        private void PreviewCanvas_OnMouseLeave(object? sender, MouseEventArgs e)
        {
            if (_hoverOp != null)
            {
                _hoverOp = null;
                UpdateBrushes();
            }
        }

        private void PreviewCanvas_OnSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            if (double.IsNaN(_offset.X) || double.IsNaN(_offset.Y))
                _offset = new Point(PreviewCanvas.ActualWidth / 2.0, PreviewCanvas.ActualHeight / 2.0);

            // Фигуры от размера окна не зависят — обновляются сетка и оси.
            UpdateViewport();
        }

        private void OnThemeChanged(object? sender, EventArgs e)
        {
            _palette = OperationPreviewPalette.ForCurrentTheme();
            RebuildScene();
        }

        // ------------------------------------------------------------------
        // Rendering
        // ------------------------------------------------------------------

        /// <summary>
        /// Полная пересборка слоя фигур: сцена, зум, тема. Пан и подкраска
        /// выделения обходятся без неё.
        /// </summary>
        private void RebuildScene()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(RebuildScene);
                return;
            }
            if (!IsLoaded || PreviewCanvas == null || _vm == null) return;

            _shapesLayer.Children.Clear();
            _visuals.Clear();
            _builtOffset = _offset;

            RebuildGridResources();
            UpdateViewport();

            foreach (var shape in _vm.Scene?.Shapes ?? (IReadOnlyList<OperationShape>)System.Array.Empty<OperationShape>())
            {
                var visuals = shape.Kind == OperationShapeKind.Point
                    ? BuildHole(shape)
                    : BuildPolyline(shape);
                if (visuals.Length > 0)
                    _visuals.Add((shape, visuals));
            }

            UpdateBrushes();
        }

        /// <summary>
        /// Пан: сдвиг слоя фигур, вьюпорта сетки и осей. Ни одна фигура не
        /// создаётся заново.
        /// </summary>
        private void UpdateViewport()
        {
            if (PreviewCanvas == null) return;

            _panTransform.X = _offset.X - _builtOffset.X;
            _panTransform.Y = _offset.Y - _builtOffset.Y;

            _gridRect.Width = Math.Max(0, PreviewCanvas.ActualWidth);
            _gridRect.Height = Math.Max(0, PreviewCanvas.ActualHeight);
            if (_gridBrush != null)
            {
                var step = GridStepMm * _zoom;
                _gridBrush.Viewport = new Rect(Mod(_offset.X, step), Mod(_offset.Y, step), step, step);
            }

            var origin = WorldToScreen(new Point(0, 0));
            _axisX.X1 = 0;
            _axisX.Y1 = origin.Y;
            _axisX.X2 = PreviewCanvas.ActualWidth;
            _axisX.Y2 = origin.Y;
            _axisY.X1 = origin.X;
            _axisY.Y1 = 0;
            _axisY.X2 = origin.X;
            _axisY.Y2 = PreviewCanvas.ActualHeight;
        }

        /// <summary>
        /// Кисть сетки под текущий шаг и палитру: одна плитка с двумя
        /// штриховыми линиями вместо десятков элементов Line на холсте.
        /// </summary>
        private void RebuildGridResources()
        {
            var step = GridStepMm * _zoom;

            var pen = new Pen(_palette.Grid, 0.5)
            {
                DashStyle = new DashStyle(new double[] { 2, 2 }, 0),
            };
            pen.Freeze();

            var lines = new GeometryGroup();
            lines.Children.Add(new LineGeometry(new Point(0, 0), new Point(step, 0)));
            lines.Children.Add(new LineGeometry(new Point(0, 0), new Point(0, step)));

            var drawing = new GeometryDrawing(null, pen, lines);
            drawing.Freeze();

            // Кисть не замораживается: пан двигает её вьюпорт.
            _gridBrush = new DrawingBrush(drawing)
            {
                TileMode = TileMode.Tile,
                ViewportUnits = BrushMappingMode.Absolute,
                Stretch = Stretch.None,
            };
            _gridRect.Fill = _gridBrush;

            // Оси заметнее сетки: тот же цвет, но менее прозрачный.
            var axisBrush = new SolidColorBrush(((SolidColorBrush)_palette.Grid).Color) { Opacity = 0.8 };
            axisBrush.Freeze();
            _axisX.Stroke = axisBrush;
            _axisY.Stroke = axisBrush;
        }

        /// <summary>Остаток по модулю, всегда неотрицательный: для вьюпорта плитки.</summary>
        private static double Mod(double value, double step)
        {
            if (step <= 0) return 0;
            var remainder = value % step;
            return remainder < 0 ? remainder + step : remainder;
        }

        /// <summary>
        /// Подкраска фигур по выделению и наведению — на месте, без
        /// пересборки: кисти палитры заморожены, присваивание дёшево.
        /// </summary>
        private void UpdateBrushes()
        {
            var selected = _vm?.SelectedOperation;
            var hover = _hoverOp;

            foreach (var (shape, visuals) in _visuals)
            {
                var op = shape.Operation;

                Brush stroke;
                if (ReferenceEquals(op, selected))
                    stroke = _palette.Selected;
                else if (ReferenceEquals(op, hover))
                    stroke = _palette.Hovered;
                else
                    stroke = _palette.ForShape(shape.Kind);

                foreach (var visual in visuals)
                {
                    switch (visual)
                    {
                        case Ellipse hole:
                            hole.Fill = stroke;
                            break;
                        case Polygon filled:
                            filled.Stroke = stroke;
                            filled.Fill = stroke;
                            break;
                        case Polyline outline:
                            outline.Stroke = stroke;
                            break;
                    }
                }
            }
        }

        private Shape[] BuildHole(OperationShape shape)
        {
            var op = shape.Operation;
            var (x, y) = shape.Points[0];
            var screen = WorldToScreen(new Point(x, y));
            var size = 5.0;
            var opacity = op != null && !op.IsEnabled ? 0.3 : 1.0;
            var ellipse = new Ellipse
            {
                Width = size,
                Height = size,
                Tag = op,
                Opacity = opacity
            };
            ApplyTooltip(ellipse, op);
            Canvas.SetLeft(ellipse, screen.X - size / 2.0);
            Canvas.SetTop(ellipse, screen.Y - size / 2.0);
            _shapesLayer.Children.Add(ellipse);
            return new Shape[] { ellipse };
        }

        private Shape[] BuildPolyline(OperationShape shape)
        {
            var worldPoints = shape.Points;
            if (worldPoints == null || worldPoints.Count == 0)
                return Array.Empty<Shape>();

            var op = shape.Operation;
            var screenPoints = new List<Point>(worldPoints.Count);
            foreach (var (x, y) in worldPoints)
                screenPoints.Add(WorldToScreen(new Point(x, y)));

            var baseOpacity = op != null && !op.IsEnabled ? 0.3 : 1.0;
            var visuals = new List<Shape>(2);

            if (shape.IsFilled && screenPoints.Count >= 3)
            {
                // Заливка и её контур полупрозрачны целиком; кисть общая с
                // обводкой — прежний клон кисти на каждый полигон не нужен,
                // палитра заморожена и безопасно разделяется.
                var polygon = new Polygon
                {
                    Opacity = 0.25 * baseOpacity,
                    StrokeThickness = 1,
                    Points = new PointCollection(screenPoints),
                    Tag = op
                };
                ApplyTooltip(polygon, op);
                _shapesLayer.Children.Add(polygon);
                visuals.Add(polygon);
            }

            var poly = new Polyline
            {
                StrokeThickness = 1,
                Opacity = baseOpacity,
                Points = new PointCollection(screenPoints),
                Tag = op
            };
            if (shape.Kind == OperationShapeKind.RapidMove)
                poly.StrokeDashArray = new DoubleCollection { 4, 3 };
            ApplyTooltip(poly, op);
            _shapesLayer.Children.Add(poly);
            visuals.Add(poly);

            return visuals.ToArray();
        }

        private void ApplyTooltip(FrameworkElement element, OperationBase? op)
        {
            if (op == null) return;
            element.ToolTip = op.Name;
            ToolTipService.SetInitialShowDelay(element, 0);
            ToolTipService.SetShowDuration(element, 60000);
            ToolTipService.SetPlacement(element, PlacementMode.Mouse);
        }

        private Point WorldToScreen(Point world)
        {
            return new Point(world.X * _zoom + _offset.X, _offset.Y - world.Y * _zoom);
        }

        private Point ScreenToWorld(Point screen)
        {
            return new Point((screen.X - _offset.X) / _zoom, (_offset.Y - screen.Y) / _zoom);
        }
    }
}
