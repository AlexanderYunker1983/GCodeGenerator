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

namespace GCodeGenerator.Views
{
    /// <summary>
    /// 2D preview of operations (plan item 6.3). The code-behind only
    /// renders the pure <see cref="OperationScene"/> from
    /// <see cref="OperationsPreviewViewModel"/> and handles the mouse;
    /// contour point generation lives in Core (<see cref="OperationSceneBuilder"/>).
    /// </summary>
    public partial class OperationsPreviewView : System.Windows.Controls.UserControl
    {
        private OperationsPreviewViewModel _vm;
        private double _zoom = 5.0; // pixels per mm
        private Point _offset;
        private bool _isPanning;
        private Point _lastMouse;
        private const double GridStepMm = 10.0;
        private const double FitPadding = 0.75; // 75% of available size
        private OperationBase _hoverOp;

        public OperationsPreviewView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            DataContextChanged += OnDataContextChanged;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _offset = new Point(PreviewCanvas.ActualWidth / 2.0, PreviewCanvas.ActualHeight / 2.0);
            HookVm();
            Redraw();
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            HookVm();
            Redraw();
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

        private void OnVmPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // Scene rebuild or selection change → redraw.
            if (e.PropertyName == nameof(OperationsPreviewViewModel.Scene) ||
                e.PropertyName == nameof(OperationsPreviewViewModel.SelectedOperation))
            {
                Redraw();
            }
        }

        private void OnShowAllRequested(object sender, EventArgs e)
        {
            FitAll();
        }

        private OperationBase GetOperationFromSource(object source)
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

            var bounds = _vm.Scene.Bounds;
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

            Redraw();
        }

        private void PreviewCanvas_OnMouseWheel(object sender, MouseWheelEventArgs e)
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

            Redraw();
        }

        private void PreviewCanvas_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                _isPanning = true;
                _lastMouse = e.GetPosition(PreviewCanvas);
                PreviewCanvas.CaptureMouse();
            }
        }

        private void PreviewCanvas_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
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

        private void PreviewCanvas_OnMouseMove(object sender, MouseEventArgs e)
        {
            if (_isPanning && e.LeftButton == MouseButtonState.Pressed)
            {
                var pos = e.GetPosition(PreviewCanvas);
                var delta = pos - _lastMouse;
                _offset = new Point(_offset.X + delta.X, _offset.Y + delta.Y);
                _lastMouse = pos;
                Redraw();
            }
            else
            {
                var op = GetOperationFromSource(e.OriginalSource);
                if (!ReferenceEquals(op, _hoverOp))
                {
                    _hoverOp = op;
                    Redraw();
                }
            }
        }

        private void PreviewCanvas_OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            _isPanning = false;
            PreviewCanvas.ReleaseMouseCapture();
        }

        private void PreviewCanvas_OnMouseLeave(object sender, MouseEventArgs e)
        {
            if (_hoverOp != null)
            {
                _hoverOp = null;
                Redraw();
            }
        }

        private void PreviewCanvas_OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (double.IsNaN(_offset.X) || double.IsNaN(_offset.Y))
                _offset = new Point(PreviewCanvas.ActualWidth / 2.0, PreviewCanvas.ActualHeight / 2.0);
            Redraw();
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            Redraw();
        }

        // ------------------------------------------------------------------
        // Rendering
        // ------------------------------------------------------------------

        private void Redraw()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(Redraw);
                return;
            }
            if (!IsLoaded || PreviewCanvas == null || _vm == null) return;
            PreviewCanvas.Children.Clear();

            DrawGrid();

            var selected = _vm.SelectedOperation;
            var hover = _hoverOp;

            foreach (var shape in _vm.Scene.Shapes)
            {
                var op = shape.Operation;

                Brush stroke;
                if (ReferenceEquals(op, selected))
                    stroke = Brushes.Red;
                else if (ReferenceEquals(op, hover))
                    stroke = Brushes.Orange;
                else
                    stroke = shape.Kind == OperationShapeKind.Point ? Brushes.SteelBlue : Brushes.DarkGreen;

                if (shape.Kind == OperationShapeKind.Point)
                {
                    var (x, y) = shape.Points[0];
                    DrawHole(x, y, stroke, op);
                }
                else
                {
                    DrawPolyline(shape.Points, stroke, op, shape.IsFilled);
                }
            }
        }

        private void DrawGrid()
        {
            var minX = (0 - _offset.X) / _zoom;
            var maxX = (PreviewCanvas.ActualWidth - _offset.X) / _zoom;
            var minY = (_offset.Y - PreviewCanvas.ActualHeight) / _zoom;
            var maxY = _offset.Y / _zoom;

            var startX = Math.Floor(minX / GridStepMm) * GridStepMm;
            var startY = Math.Floor(minY / GridStepMm) * GridStepMm;

            var gridBrushBase = TryFindResource("TextBrush") as Brush ?? Brushes.Gray;
            var gridBrush = gridBrushBase.CloneCurrentValue();

            for (double x = startX; x <= maxX; x += GridStepMm)
            {
                var p1 = WorldToScreen(new Point(x, minY));
                var p2 = WorldToScreen(new Point(x, maxY));
                var line = new Line
                {
                    X1 = p1.X,
                    Y1 = p1.Y,
                    X2 = p2.X,
                    Y2 = p2.Y,
                    Stroke = gridBrush,
                    StrokeThickness = 0.5,
                    StrokeDashArray = new DoubleCollection { 2, 2 },
                    Opacity = 0.6
                };
                PreviewCanvas.Children.Add(line);
            }

            for (double y = startY; y <= maxY; y += GridStepMm)
            {
                var p1 = WorldToScreen(new Point(minX, y));
                var p2 = WorldToScreen(new Point(maxX, y));
                var line = new Line
                {
                    X1 = p1.X,
                    Y1 = p1.Y,
                    X2 = p2.X,
                    Y2 = p2.Y,
                    Stroke = gridBrush,
                    StrokeThickness = 0.5,
                    StrokeDashArray = new DoubleCollection { 2, 2 },
                    Opacity = 0.6
                };
                PreviewCanvas.Children.Add(line);
            }

            // axes
            var origin = WorldToScreen(new Point(0, 0));
            var axisBrush = gridBrushBase.CloneCurrentValue();
            axisBrush.Opacity = 0.8;

            var axisX = new Line
            {
                X1 = 0,
                Y1 = origin.Y,
                X2 = PreviewCanvas.ActualWidth,
                Y2 = origin.Y,
                Stroke = axisBrush,
                StrokeThickness = 1
            };
            PreviewCanvas.Children.Add(axisX);

            var axisY = new Line
            {
                X1 = origin.X,
                Y1 = 0,
                X2 = origin.X,
                Y2 = PreviewCanvas.ActualHeight,
                Stroke = axisBrush,
                StrokeThickness = 1
            };
            PreviewCanvas.Children.Add(axisY);
        }

        private void DrawHole(double x, double y, Brush brush, OperationBase op)
        {
            var screen = WorldToScreen(new Point(x, y));
            var size = 5.0;
            var opacity = op != null && !op.IsEnabled ? 0.3 : 1.0;
            var ellipse = new Ellipse
            {
                Width = size,
                Height = size,
                Fill = brush,
                Tag = op,
                Opacity = opacity
            };
            ApplyTooltip(ellipse, op);
            Canvas.SetLeft(ellipse, screen.X - size / 2.0);
            Canvas.SetTop(ellipse, screen.Y - size / 2.0);
            PreviewCanvas.Children.Add(ellipse);
        }

        private void DrawPolyline(IReadOnlyList<(double X, double Y)> worldPoints, Brush stroke, OperationBase op, bool fill)
        {
            if (worldPoints == null || worldPoints.Count == 0)
                return;

            var screenPoints = new List<Point>(worldPoints.Count);
            foreach (var (x, y) in worldPoints)
                screenPoints.Add(WorldToScreen(new Point(x, y)));

            var baseOpacity = op != null && !op.IsEnabled ? 0.3 : 1.0;

            if (fill && screenPoints.Count >= 3)
            {
                var polygon = new Polygon
                {
                    Stroke = stroke,
                    Fill = stroke.Clone(),
                    Opacity = 0.25 * baseOpacity,
                    StrokeThickness = 1,
                    Points = new PointCollection(screenPoints),
                    Tag = op
                };
                ApplyTooltip(polygon, op);
                PreviewCanvas.Children.Add(polygon);
            }

            var poly = new Polyline
            {
                Stroke = stroke,
                StrokeThickness = 1,
                Opacity = baseOpacity,
                Points = new PointCollection(screenPoints),
                Tag = op
            };
            ApplyTooltip(poly, op);
            PreviewCanvas.Children.Add(poly);
        }

        private void ApplyTooltip(FrameworkElement element, OperationBase op)
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
