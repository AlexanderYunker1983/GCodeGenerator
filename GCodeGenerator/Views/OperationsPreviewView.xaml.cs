using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Shapes;
using System.Windows.Controls.Primitives;
using GCodeGenerator.Models;
using GCodeGenerator.ViewModels;

namespace GCodeGenerator.Views
{
    public partial class OperationsPreviewView : System.Windows.Controls.UserControl
    {
        private MainViewModel _mainVm;
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
            UnhookVm();
            HookVm();
            Redraw();
        }

        private void HookVm()
        {
            _mainVm = DataContext as MainViewModel;
            if (_mainVm != null)
            {
                _mainVm.OperationsChanged += Redraw;
                (_mainVm.AllOperations as INotifyCollectionChanged).CollectionChanged += OnOperationsCollectionChanged;
                _mainVm.ShowAllRequested += FitAll;
            }
        }

        private void UnhookVm()
        {
            if (_mainVm != null)
            {
                _mainVm.OperationsChanged -= Redraw;
                (_mainVm.AllOperations as INotifyCollectionChanged).CollectionChanged -= OnOperationsCollectionChanged;
                _mainVm.ShowAllRequested -= FitAll;
            }
            _mainVm = null;
        }

        private void OnOperationsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            Redraw();
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

        public void FitAll()
        {
            if (_mainVm == null || PreviewCanvas == null || PreviewCanvas.ActualWidth < 1 || PreviewCanvas.ActualHeight < 1)
                return;

            var points = new List<Point>();
            foreach (var op in _mainVm.AllOperations)
            {
                if (op is DrillPointsOperation drillOp)
                {
                    points.AddRange(drillOp.Holes.Select(h => new Point(h.X, h.Y)));
                }
                else if (op is ProfileRectangleOperation rectOp)
                {
                    points.AddRange(GetRectanglePoints(rectOp));
                }
                else if (op is ProfileRoundedRectangleOperation rrectOp)
                {
                    points.AddRange(GetRoundedRectanglePoints(rrectOp));
                }
                else if (op is ProfileCircleOperation circleOp)
                {
                    points.AddRange(GetCirclePoints(circleOp));
                }
                else if (op is ProfileEllipseOperation ellipseOp)
                {
                    points.AddRange(GetEllipsePoints(ellipseOp));
                }
                else if (op is ProfilePolygonOperation polyOp)
                {
                    points.AddRange(GetPolygonPoints(polyOp));
                }
                else if (op is PocketRectangleOperation pocketOp)
                {
                    var rect = new ProfileRectangleOperation
                    {
                        Width = pocketOp.Width,
                        Height = pocketOp.Height,
                        RotationAngle = pocketOp.RotationAngle,
                        ReferencePointX = pocketOp.ReferencePointX,
                        ReferencePointY = pocketOp.ReferencePointY,
                        ReferencePointType = pocketOp.ReferencePointType
                    };
                    points.AddRange(GetRectanglePoints(rect));
                }
            }

            if (points.Count == 0)
                return;

            var minX = points.Min(p => p.X);
            var maxX = points.Max(p => p.X);
            var minY = points.Min(p => p.Y);
            var maxY = points.Max(p => p.Y);

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
            var op = GetOperationFromSource(e.OriginalSource);
            if (op == null) return;

            _mainVm.SelectedOperation = op;

            if (e.ClickCount >= 2 && _mainVm.EditOperationCommand?.CanExecute(null) == true)
            {
                _mainVm.EditOperationCommand.Execute(null);
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

        private void Redraw()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(Redraw);
                return;
            }
            if (!IsLoaded || PreviewCanvas == null) return;
            PreviewCanvas.Children.Clear();

            DrawGrid();
            if (_mainVm == null) return;

            var selected = _mainVm.SelectedOperation;
            var hover = _hoverOp;

            foreach (var op in _mainVm.AllOperations)
            {
                Brush StrokeFor(OperationBase operation, Brush normal)
                {
                    if (ReferenceEquals(operation, selected)) return Brushes.Red;
                    if (ReferenceEquals(operation, hover)) return Brushes.Orange;
                    return normal;
                }

                if (op is DrillPointsOperation drillOp)
                {
                    var holeBrush = StrokeFor(op, Brushes.SteelBlue);
                    foreach (var hole in drillOp.Holes)
                    {
                        DrawHole(hole.X, hole.Y, holeBrush, op);
                    }
                }
                else if (op is ProfileRectangleOperation rectOp)
                {
                    DrawPolyline(GetRectanglePoints(rectOp), StrokeFor(op, Brushes.DarkGreen), op);
                }
                else if (op is ProfileRoundedRectangleOperation rrectOp)
                {
                    DrawPolyline(GetRoundedRectanglePoints(rrectOp), StrokeFor(op, Brushes.DarkGreen), op);
                }
                else if (op is ProfileCircleOperation circleOp)
                {
                    DrawPolyline(GetCirclePoints(circleOp), StrokeFor(op, Brushes.DarkGreen), op);
                }
                else if (op is ProfileEllipseOperation ellipseOp)
                {
                    DrawPolyline(GetEllipsePoints(ellipseOp), StrokeFor(op, Brushes.DarkGreen), op);
                }
                else if (op is ProfilePolygonOperation polyOp)
                {
                    DrawPolyline(GetPolygonPoints(polyOp), StrokeFor(op, Brushes.DarkGreen), op);
                }
                else if (op is PocketRectangleOperation pocketRect)
                {
                    DrawPolyline(GetRectanglePoints(new ProfileRectangleOperation
                    {
                        Width = pocketRect.Width,
                        Height = pocketRect.Height,
                        RotationAngle = pocketRect.RotationAngle,
                        ReferencePointX = pocketRect.ReferencePointX,
                        ReferencePointY = pocketRect.ReferencePointY,
                        ReferencePointType = pocketRect.ReferencePointType
                    }), StrokeFor(op, Brushes.DarkGreen), op);
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
                    Stroke = Brushes.Black,
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
                    Stroke = Brushes.Black,
                    StrokeThickness = 0.5,
                    StrokeDashArray = new DoubleCollection { 2, 2 },
                    Opacity = 0.6
                };
                PreviewCanvas.Children.Add(line);
            }

            // axes
            var origin = WorldToScreen(new Point(0, 0));
            var axisX = new Line
            {
                X1 = 0,
                Y1 = origin.Y,
                X2 = PreviewCanvas.ActualWidth,
                Y2 = origin.Y,
                Stroke = Brushes.Gray,
                StrokeThickness = 1
            };
            PreviewCanvas.Children.Add(axisX);

            var axisY = new Line
            {
                X1 = origin.X,
                Y1 = 0,
                X2 = origin.X,
                Y2 = PreviewCanvas.ActualHeight,
                Stroke = Brushes.Gray,
                StrokeThickness = 1
            };
            PreviewCanvas.Children.Add(axisY);
        }

        private void DrawHole(double x, double y, Brush brush, OperationBase op)
        {
            var screen = WorldToScreen(new Point(x, y));
            var size = 5.0;
            var ellipse = new Ellipse
            {
                Width = size,
                Height = size,
                Fill = brush,
                Tag = op
            };
            ApplyTooltip(ellipse, op);
            Canvas.SetLeft(ellipse, screen.X - size / 2.0);
            Canvas.SetTop(ellipse, screen.Y - size / 2.0);
            PreviewCanvas.Children.Add(ellipse);
        }

        private void DrawPolyline(IList<Point> worldPoints, Brush stroke, OperationBase op)
        {
            if (worldPoints == null || worldPoints.Count == 0)
                return;

            var poly = new Polyline
            {
                Stroke = stroke,
                StrokeThickness = 1,
                Points = new PointCollection(worldPoints.Select(WorldToScreen)),
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

        private List<Point> GetRectanglePoints(ProfileRectangleOperation op)
        {
            GetCenter(op.ReferencePointType, op.ReferencePointX, op.ReferencePointY, op.Width, op.Height, out var cx, out var cy);
            var halfW = op.Width / 2.0;
            var halfH = op.Height / 2.0;
            var rad = op.RotationAngle * Math.PI / 180.0;
            var cos = Math.Cos(rad);
            var sin = Math.Sin(rad);

            var corners = new[]
            {
                new Point(-halfW, -halfH),
                new Point(halfW, -halfH),
                new Point(halfW, halfH),
                new Point(-halfW, halfH),
                new Point(-halfW, -halfH)
            };

            return corners.Select(p => new Point(
                cx + p.X * cos - p.Y * sin,
                cy + p.X * sin + p.Y * cos)).ToList();
        }

        private List<Point> GetRoundedRectanglePoints(ProfileRoundedRectangleOperation op)
        {
            GetCenter(op.ReferencePointType, op.ReferencePointX, op.ReferencePointY, op.Width, op.Height, out var cx, out var cy);
            var halfW = op.Width / 2.0;
            var halfH = op.Height / 2.0;
            var rad = op.RotationAngle * Math.PI / 180.0;
            var cos = Math.Cos(rad);
            var sin = Math.Sin(rad);

            double Clamp(double r) => Math.Min(Math.Max(0, r), Math.Min(halfW, halfH));
            var radii = new[]
            {
                Clamp(op.RadiusTopLeft),
                Clamp(op.RadiusTopRight),
                Clamp(op.RadiusBottomRight),
                Clamp(op.RadiusBottomLeft)
            };

            (double X, double Y) Corner(int idx)
            {
                switch (idx)
                {
                    case 0: return (-halfW, -halfH);
                    case 1: return (halfW, -halfH);
                    case 2: return (halfW, halfH);
                    default: return (-halfW, halfH);
                }
            }

            (double X, double Y) ArcCenter(int idx, double r)
            {
                switch (idx)
                {
                    case 0: return (-halfW + r, -halfH + r);
                    case 1: return (halfW - r, -halfH + r);
                    case 2: return (halfW - r, halfH - r);
                    default: return (-halfW + r, halfH - r);
                }
            }

            var maxSeg = Math.Max(0.001, op.MaxSegmentLength);

            var corners = new[] { 0, 1, 2, 3 }.Select(Corner).ToArray();
            var trims = new (double sx, double sy, double ex, double ey)[4];
            for (int i = 0; i < 4; i++)
            {
                var next = (i + 1) % 4;
                var start = corners[i];
                var end = corners[next];
                var rStart = radii[i];
                var rEnd = radii[next];
                var dx = end.X - start.X;
                var dy = end.Y - start.Y;
                var len = Math.Sqrt(dx * dx + dy * dy);
                if (len < 1e-6) len = 1e-6;
                var ux = dx / len;
                var uy = dy / len;
                var trimStart = (start.X + ux * rStart, start.Y + uy * rStart);
                var trimEnd = (end.X - ux * rEnd, end.Y - uy * rEnd);
                trims[i] = (trimStart.Item1, trimStart.Item2, trimEnd.Item1, trimEnd.Item2);
            }

            var pts = new List<Point>();
            // start with first trimmed point
            pts.Add(RotateTranslate(trims[0].sx, trims[0].sy, cx, cy, cos, sin));

            for (int i = 0; i < 4; i++)
            {
                var next = (i + 1) % 4;
                var trimEnd = trims[i];
                var trimStartNext = trims[next];
                var arcRadius = radii[next];

                pts.Add(RotateTranslate(trimEnd.ex, trimEnd.ey, cx, cy, cos, sin));

                if (arcRadius > 0)
                {
                    var center = ArcCenter(next, arcRadius);
                    var startDir = (trimEnd.ex - center.X, trimEnd.ey - center.Y);
                    var endDir = (trimStartNext.sx - center.X, trimStartNext.sy - center.Y);
                    var angleStart = Math.Atan2(startDir.Item2, startDir.Item1);
                    var angleEnd = Math.Atan2(endDir.Item2, endDir.Item1);
                    var sweep = angleEnd - angleStart;
                    while (sweep > Math.PI) sweep -= 2 * Math.PI;
                    while (sweep < -Math.PI) sweep += 2 * Math.PI;

                    var arcLen = Math.Abs(sweep) * arcRadius;
                    var steps = Math.Max(1, (int)Math.Ceiling(arcLen / maxSeg));
                    var stepSweep = sweep / steps;
                    for (int s = 1; s <= steps; s++)
                    {
                        var ang = angleStart + stepSweep * s;
                        var ax = center.X + arcRadius * Math.Cos(ang);
                        var ay = center.Y + arcRadius * Math.Sin(ang);
                        pts.Add(RotateTranslate(ax, ay, cx, cy, cos, sin));
                    }
                }
            }

            if (pts.Count > 0)
                pts.Add(pts[0]);
            return pts;
        }

        private List<Point> GetCirclePoints(ProfileCircleOperation op)
        {
            var maxSeg = Math.Max(0.001, op.MaxSegmentLength);
            var circumference = 2 * Math.PI * op.Radius;
            var steps = Math.Max(12, (int)Math.Ceiling(circumference / maxSeg));
            var pts = new List<Point>(steps + 1);
            for (int i = 0; i <= steps; i++)
            {
                var ang = 2 * Math.PI * i / steps;
                pts.Add(new Point(op.CenterX + op.Radius * Math.Cos(ang), op.CenterY + op.Radius * Math.Sin(ang)));
            }
            return pts;
        }

        private List<Point> GetEllipsePoints(ProfileEllipseOperation op)
        {
            var h = Math.Pow(op.RadiusX - op.RadiusY, 2) / Math.Pow(op.RadiusX + op.RadiusY, 2);
            var perimeter = Math.PI * (op.RadiusX + op.RadiusY) * (1 + 3 * h / (10 + Math.Sqrt(4 - 3 * h)));
            var maxSeg = Math.Max(0.001, op.MaxSegmentLength);
            var steps = Math.Max(16, (int)Math.Ceiling(perimeter / maxSeg));
            var rad = op.RotationAngle * Math.PI / 180.0;
            var cos = Math.Cos(rad);
            var sin = Math.Sin(rad);
            var pts = new List<Point>(steps + 1);
            for (int i = 0; i <= steps; i++)
            {
                var ang = 2 * Math.PI * i / steps;
                var x = op.RadiusX * Math.Cos(ang);
                var y = op.RadiusY * Math.Sin(ang);
                var xr = x * cos - y * sin + op.CenterX;
                var yr = x * sin + y * cos + op.CenterY;
                pts.Add(new Point(xr, yr));
            }
            return pts;
        }

        private List<Point> GetPolygonPoints(ProfilePolygonOperation op)
        {
            var pts = new List<Point>();
            var rad = op.RotationAngle * Math.PI / 180.0;
            for (int i = 0; i <= op.NumberOfSides; i++)
            {
                var ang = rad + 2 * Math.PI * i / op.NumberOfSides;
                pts.Add(new Point(op.CenterX + op.Radius * Math.Cos(ang), op.CenterY + op.Radius * Math.Sin(ang)));
            }
            return pts;
        }

        private void GetCenter(ReferencePointType type, double refX, double refY, double width, double height, out double cx, out double cy)
        {
            switch (type)
            {
                case ReferencePointType.Center:
                    cx = refX;
                    cy = refY;
                    break;
                case ReferencePointType.TopLeft:
                    cx = refX + width / 2.0;
                    cy = refY - height / 2.0;
                    break;
                case ReferencePointType.TopRight:
                    cx = refX - width / 2.0;
                    cy = refY - height / 2.0;
                    break;
                case ReferencePointType.BottomLeft:
                    cx = refX + width / 2.0;
                    cy = refY + height / 2.0;
                    break;
                case ReferencePointType.BottomRight:
                    cx = refX - width / 2.0;
                    cy = refY + height / 2.0;
                    break;
                default:
                    cx = refX;
                    cy = refY;
                    break;
            }
        }

        private Point RotateTranslate(double x, double y, double cx, double cy, double cos, double sin)
        {
            var xr = x * cos - y * sin + cx;
            var yr = x * sin + y * cos + cy;
            return new Point(xr, yr);
        }
    }
}


