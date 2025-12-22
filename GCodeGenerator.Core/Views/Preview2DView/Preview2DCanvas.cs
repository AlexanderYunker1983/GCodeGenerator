using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using System.ComponentModel;
using GCodeGenerator.Core.Models;
using GCodeGenerator.Core.ViewModels.Preview2DViewModel;

namespace GCodeGenerator.Core.Views.Preview2DView;

public class Preview2DCanvas : Control
{
    /// <summary>
    /// Определяет свойство Background для hit-testing и отрисовки фона.
    /// </summary>
    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        Border.BackgroundProperty.AddOwner<Preview2DCanvas>();

    public IBrush? Background
    {
        get => GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }
    
    // Желаемая толщина линий в пикселях экрана (постоянная для всех масштабов)
    private const double GridLineThickness = 0.5;
    private const double MajorGridLineThickness = 1.0;
    private const double AxisLineThickness = 1.5;
    
    // Максимальное количество линий сетки для предотвращения зависания
    private const int MaxGridLines = 2000;
    
    private Point _lastMousePosition;
    private bool _isPanning;
    private bool _pendingRender;

    private Preview2DViewModel? _viewModel;
    private bool _isInitialized;
    private Size _previousSize;

    public Preview2DCanvas()
    {
        // Включаем обработку событий мыши
        Focusable = true;
        // Устанавливаем прозрачный фон, чтобы вся область участвовала в hit-testing
        Background = Brushes.Transparent;
        
        // Перерисовываем при изменении Background
        AffectsRender<Preview2DCanvas>(BackgroundProperty);
        
        // Подписываемся на изменения DataContext
        DataContextChanged += OnDataContextChanged;
    }
    
    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // Отписываемся от старого ViewModel
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.RedrawRequested -= OnViewModelRedrawRequested;
        }

        // Подписываемся на новый ViewModel
        _viewModel = DataContext as Preview2DViewModel;
        _isInitialized = false;
        
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _viewModel.RedrawRequested += OnViewModelRedrawRequested;

            // Ждем следующего кадра для получения правильных размеров
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                TryInitializeOffset();
            }, Avalonia.Threading.DispatcherPriority.Loaded);
        }
        
        InvalidateVisual();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Preview2DViewModel.SelectedPrimitive) ||
            e.PropertyName == nameof(Preview2DViewModel.HoveredPrimitive))
        {
            RequestRender();
        }
    }
    
    private void OnViewModelRedrawRequested(object? sender, EventArgs e)
    {
        RequestRender();
    }
    
    private void TryInitializeOffset()
    {
        if (_viewModel != null && !_isInitialized)
        {
            var bounds = Bounds;
            if (bounds.Width > 0 && bounds.Height > 0)
            {
                _viewModel.InitializeOffset(bounds.Width, bounds.Height);
                _previousSize = bounds.Size;
                _isInitialized = true;
                InvalidateVisual();
            }
        }
    }
    
    /// <summary>
    /// Запрашивает перерисовку с дедупликацией - несколько вызовов подряд
    /// приведут к одной перерисовке на следующем кадре.
    /// </summary>
    private void RequestRender()
    {
        if (_pendingRender)
            return;
            
        _pendingRender = true;
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            _pendingRender = false;
            InvalidateVisual();
        }, Avalonia.Threading.DispatcherPriority.Render);
    }

    private void UpdateHoveredPrimitive(Point worldPoint)
    {
        if (_viewModel == null)
            return;

        var toleranceWorld = 2.0 / _viewModel.Scale; // ~2 пикселя

        PrimitiveItem? hit = null;

        // Ищем сверху вниз — от последнего к первому
        for (int i = _viewModel.Primitives.Count - 1; i >= 0; i--)
        {
            var primitive = _viewModel.Primitives[i];
            if (IsPointNearPrimitive(worldPoint, primitive, toleranceWorld))
            {
                hit = primitive;
                break;
            }
        }

        _viewModel.HoveredPrimitive = hit;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        
        // Получаем фокус для корректной обработки событий
        Focus();
        
        var point = e.GetCurrentPoint(this);
        if (point.Properties.IsLeftButtonPressed)
        {
            // Если есть ViewModel, пробуем сначала выбрать примитив под курсором
            if (_viewModel != null)
            {
                var mousePosition = point.Position;
                var inverseScale = 1.0 / _viewModel.Scale;
                var worldX = (mousePosition.X - _viewModel.Offset.X) * inverseScale;
                var worldY = (_viewModel.Offset.Y - mousePosition.Y) * inverseScale;
                var worldPoint = new Point(worldX, worldY);

                var toleranceWorld = 2.0 / _viewModel.Scale;
                PrimitiveItem? hit = null;
                for (int i = _viewModel.Primitives.Count - 1; i >= 0; i--)
                {
                    var primitive = _viewModel.Primitives[i];
                    if (IsPointNearPrimitive(worldPoint, primitive, toleranceWorld))
                    {
                        hit = primitive;
                        break;
                    }
                }

                if (hit != null)
                {
                    _viewModel.SelectedPrimitive = hit;
                    e.Handled = true;
                    RequestRender();
                    return;
                }
            }

            // Если примитив не найден — начинаем панорамирование
            _isPanning = true;
            _lastMousePosition = point.Position;

            // Захватываем указатель для получения всех событий, даже вне контрола
            e.Pointer.Capture(this);
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        if (_isPanning && _viewModel != null && e.Pointer.Captured == this)
        {
            var currentPosition = e.GetPosition(this);
            var deltaX = currentPosition.X - _lastMousePosition.X;
            var deltaY = currentPosition.Y - _lastMousePosition.Y;
            
            if (Math.Abs(deltaX) > 0.5 || Math.Abs(deltaY) > 0.5)
            {
                // Обновляем напрямую через свойства (без PropertyChanged)
                _viewModel.Offset = new Point(
                    _viewModel.Offset.X + deltaX,
                    _viewModel.Offset.Y + deltaY
                );
                _lastMousePosition = currentPosition;
                RequestRender();
            }
            e.Handled = true;
            return;
        }
        
        // Обновляем координаты мыши в мировых координатах и подсветку примитивов
        if (_viewModel != null)
        {
            var mousePosition = e.GetPosition(this);
            var inverseScale = 1.0 / _viewModel.Scale;
            var worldX = (mousePosition.X - _viewModel.Offset.X) * inverseScale;
            var worldY = (_viewModel.Offset.Y - mousePosition.Y) * inverseScale;
            var worldPoint = new Point(worldX, worldY);

            _viewModel.MouseWorldCoordinates = worldPoint;
            UpdateHoveredPrimitive(worldPoint);
        }
        
        base.OnPointerMoved(e);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        
        if (_isPanning)
        {
            _isPanning = false;
            if (e.Pointer.Captured == this)
            {
                e.Pointer.Capture(null);
            }
            e.Handled = true;
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        
        // Сбрасываем состояние панорамирования при потере захвата
        if (_isPanning)
        {
            _isPanning = false;
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        
        // Обновляем координаты мыши при входе в канвас
        if (_viewModel != null)
        {
            var mousePosition = e.GetPosition(this);
            var inverseScale = 1.0 / _viewModel.Scale;
            var worldX = (mousePosition.X - _viewModel.Offset.X) * inverseScale;
            var worldY = (_viewModel.Offset.Y - mousePosition.Y) * inverseScale;
            _viewModel.MouseWorldCoordinates = new Point(worldX, worldY);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        
        // Очищаем координаты мыши и подсветку, когда мышь покидает канвас
        if (_viewModel != null)
        {
            _viewModel.MouseWorldCoordinates = null;
            _viewModel.HoveredPrimitive = null;
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        if (!_isPanning && _viewModel != null)
        {
            var mousePosition = e.GetPosition(this);
            var delta = e.Delta.Y;
            if (Math.Abs(delta) > 0.01)
            {
                _viewModel.Zoom(delta, mousePosition);
                RequestRender();
            }
            e.Handled = true;
            return;
        }
        base.OnPointerWheelChanged(e);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        
        if (_viewModel == null)
        {
            TryInitializeOffset();
            return;
        }
        
        // Если размер еще не был установлен, инициализируем offset
        if (!_isInitialized || _previousSize.Width <= 0 || _previousSize.Height <= 0)
        {
            TryInitializeOffset();
            return;
        }
        
        // Вычисляем точку в мировых координатах, которая была в центре старого контрола
        // Формула при текущем преобразовании: 
        // x_screen = x_world * scale + offset.X
        // y_screen = -y_world * scale + offset.Y
        var oldCenterX = _previousSize.Width / 2;
        var oldCenterY = _previousSize.Height / 2;
        var worldX = (oldCenterX - _viewModel.Offset.X) / _viewModel.Scale;
        var worldY = (_viewModel.Offset.Y - oldCenterY) / _viewModel.Scale;
        
        // Вычисляем новый offset так, чтобы эта же точка осталась в центре нового контрола
        // x_screen = x_world * scale + offset.X => offset.X = centerX - x_world * scale
        // y_screen = -y_world * scale + offset.Y => offset.Y = centerY + y_world * scale
        var newCenterX = e.NewSize.Width / 2;
        var newCenterY = e.NewSize.Height / 2;
        _viewModel.Offset = new Point(
            newCenterX - worldX * _viewModel.Scale,
            newCenterY + worldY * _viewModel.Scale
        );
        
        // Сохраняем новый размер для следующего изменения
        _previousSize = e.NewSize;
        
        RequestRender();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;
        
        // Рисуем фон (необходимо для hit-testing)
        if (Background != null)
        {
            context.FillRectangle(Background, new Rect(0, 0, bounds.Width, bounds.Height));
        }
        
        if (_viewModel == null)
            return;

        // Ограничиваем область отрисовки границами контрола для предотвращения выхода за границы
        using (context.PushClip(bounds))
        {
            // Применяем трансформацию
            // Формула преобразования:
            //   x_screen = x_world * scale + offset.X
            //   y_screen = -y_world * scale + offset.Y  (ось Y направлена вверх в мировых координатах)
            // В Avalonia матрицы умножаются: A * B означает сначала B, потом A
            // Нам нужно: сначала масштаб, затем смещение.
            using (context.PushTransform(
                Matrix.CreateScale(_viewModel.Scale, -_viewModel.Scale) *
                Matrix.CreateTranslation(_viewModel.Offset.X, _viewModel.Offset.Y)))
            {
                DrawGrid(context, bounds, _viewModel);
                DrawPrimitives(context, _viewModel);
            }
        }
    }

    private void DrawGrid(DrawingContext context, Rect bounds, Preview2DViewModel viewModel)
    {
        var inverseScale = 1.0 / viewModel.Scale;
        
        // Преобразуем границы экрана в логические координаты
        // x_screen = x_world * scale + offset.X
        // y_screen = -y_world * scale + offset.Y
        var worldLeft = (0 - viewModel.Offset.X) * inverseScale;
        var worldRight = (bounds.Width - viewModel.Offset.X) * inverseScale;
        var worldTop = (viewModel.Offset.Y - 0) * inverseScale;
        var worldBottom = (viewModel.Offset.Y - bounds.Height) * inverseScale;
        
        // Определяем видимую область
        var visibleLeft = Math.Min(worldLeft, worldRight);
        var visibleRight = Math.Max(worldLeft, worldRight);
        var visibleTop = Math.Min(worldTop, worldBottom);
        var visibleBottom = Math.Max(worldTop, worldBottom);
        
        var visibleWidth = visibleRight - visibleLeft;
        var visibleHeight = visibleBottom - visibleTop;
        
        // Динамически выбираем размер сетки на основе видимой области
        // Цель: примерно 10-50 линий на экране
        var effectiveGridSize = CalculateGridSize(Math.Max(visibleWidth, visibleHeight));
        
        // Добавляем небольшой запас для корректной отрисовки на границах
        var margin = effectiveGridSize;
        var drawLeft = visibleLeft - margin;
        var drawTop = visibleTop - margin;
        var drawRight = visibleRight + margin;
        var drawBottom = visibleBottom + margin;

        // Находим начальные координаты для отрисовки сетки
        var startX = Math.Floor(drawLeft / effectiveGridSize) * effectiveGridSize;
        var startY = Math.Floor(drawTop / effectiveGridSize) * effectiveGridSize;
        var endX = Math.Ceiling(drawRight / effectiveGridSize) * effectiveGridSize;
        var endY = Math.Ceiling(drawBottom / effectiveGridSize) * effectiveGridSize;
        
        // Проверяем количество линий и пропускаем, если слишком много
        var numLinesX = (int)((endX - startX) / effectiveGridSize) + 1;
        var numLinesY = (int)((endY - startY) / effectiveGridSize) + 1;
        
        if (numLinesX > MaxGridLines || numLinesY > MaxGridLines)
        {
            // Слишком много линий - увеличиваем шаг
            effectiveGridSize = Math.Max(visibleWidth, visibleHeight) / 20;
            startX = Math.Floor(drawLeft / effectiveGridSize) * effectiveGridSize;
            startY = Math.Floor(drawTop / effectiveGridSize) * effectiveGridSize;
            endX = Math.Ceiling(drawRight / effectiveGridSize) * effectiveGridSize;
            endY = Math.Ceiling(drawBottom / effectiveGridSize) * effectiveGridSize;
        }

        // Определяем цвета в зависимости от темы
        var isDark = Application.Current?.ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark;
        var gridColor = isDark ? Colors.Gray : Colors.LightGray;
        var majorGridColor = isDark ? Colors.Gray : Colors.LightGray;
        
        // Вычисляем толщину линий в мировых координатах для постоянной толщины в пикселях экрана
        // Толщина в мировых координатах = желаемая толщина в пикселях / scale
        var gridThickness = GridLineThickness / viewModel.Scale;
        var majorGridThickness = MajorGridLineThickness / viewModel.Scale;
        
        // Создаем перья динамически
        var gridPen = new Pen(new SolidColorBrush(gridColor), gridThickness);
        var majorGridPen = new Pen(new SolidColorBrush(majorGridColor), majorGridThickness);
        var majorGridStep = effectiveGridSize * 10;

        // Рисуем вертикальные линии
        for (var x = startX; x <= endX; x += effectiveGridSize)
        {
            var isMajorLine = Math.Abs(x % majorGridStep) < 0.001 * effectiveGridSize;
            context.DrawLine(
                isMajorLine ? majorGridPen : gridPen,
                new Point(x, drawTop),
                new Point(x, drawBottom)
            );
        }

        // Рисуем горизонтальные линии
        for (var y = startY; y <= endY; y += effectiveGridSize)
        {
            var isMajorLine = Math.Abs(y % majorGridStep) < 0.001 * effectiveGridSize;
            context.DrawLine(
                isMajorLine ? majorGridPen : gridPen,
                new Point(drawLeft, y),
                new Point(drawRight, y)
            );
        }

        // Рисуем оси координат
        // Толщина осей также должна быть постоянной в пикселях экрана
        var axisThickness = AxisLineThickness / viewModel.Scale;
        var axisPen = new Pen(new SolidColorBrush(Colors.Red), axisThickness);
        
        // Ось X (y = 0)
        if (visibleTop <= 0 && visibleBottom >= 0)
        {
            context.DrawLine(axisPen, new Point(drawLeft, 0), new Point(drawRight, 0));
        }

        // Ось Y (x = 0)
        if (visibleLeft <= 0 && visibleRight >= 0)
        {
            context.DrawLine(axisPen, new Point(0, drawTop), new Point(0, drawBottom));
        }
    }
    
    private static double CalculateGridSize(double visibleSize)
    {
        // Выбираем размер сетки так, чтобы было примерно 20-40 линий
        // Используем "красивые" числа: 1, 2, 5, 10, 20, 50, 100...
        var targetLines = 30.0;
        var rawSize = visibleSize / targetLines;
        
        // Округляем до ближайшего "красивого" числа
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(rawSize)));
        var normalized = rawSize / magnitude;
        
        double niceSize;
        if (normalized < 1.5)
            niceSize = 1;
        else if (normalized < 3.5)
            niceSize = 2;
        else if (normalized < 7.5)
            niceSize = 5;
        else
            niceSize = 10;
            
        return niceSize * magnitude;
    }

    private void DrawPrimitives(DrawingContext context, Preview2DViewModel viewModel)
    {
        if (viewModel.Primitives.Count == 0)
            return;

        // Толщина линии в мировых координатах, чтобы на экране она была тонкой (~1 пиксель)
        var strokeThickness = 1.0 / viewModel.Scale;
        var normalPen = new Pen(new SolidColorBrush(Colors.Blue), strokeThickness);
        var hoverPen = new Pen(new SolidColorBrush(Colors.Yellow), strokeThickness * 2);
        var selectedPen = new Pen(new SolidColorBrush(Colors.LimeGreen), strokeThickness * 2);

        var selected = viewModel.SelectedPrimitive;
        var hovered = viewModel.HoveredPrimitive;
        // Радиус точки в мировых координатах, чтобы на экране он был ~5 пикселей
        var pointRadiusWorld = 5.0 / viewModel.Scale;

        // Сначала рисуем все примитивы, кроме выделенного и подсвеченного
        foreach (var primitive in viewModel.Primitives)
        {
            if (ReferenceEquals(primitive, selected) || ReferenceEquals(primitive, hovered))
                continue;

            DrawPrimitive(context, normalPen, primitive, pointRadiusWorld);
        }

        // Затем поверх остальных — примитив под мышью (если есть и он не совпадает с выделенным)
        if (hovered != null && !ReferenceEquals(hovered, selected))
        {
            DrawPrimitive(context, hoverPen, hovered, pointRadiusWorld);
        }

        // И в самом верху — выделенный примитив (если есть)
        if (selected != null)
        {
            DrawPrimitive(context, selectedPen, selected, pointRadiusWorld);
        }
    }

    private static void DrawPrimitive(DrawingContext context, Pen pen, PrimitiveItem primitive, double pointRadiusWorld)
    {
        switch (primitive)
        {
            case PointPrimitive p:
                DrawPoint(context, pen, p, pointRadiusWorld);
                break;
            case LinePrimitive line:
                context.DrawLine(pen,
                    new Point(line.X1, line.Y1),
                    new Point(line.X2, line.Y2));
                break;
            case CirclePrimitive circle:
                DrawCircle(context, pen, circle.CenterX, circle.CenterY, circle.Radius);
                break;
            case RectanglePrimitive rect:
                DrawOrientedRectangle(context, pen,
                    rect.CenterX, rect.CenterY, rect.Width, rect.Height, rect.RotationAngle);
                break;
            case EllipsePrimitive ellipse:
                DrawOrientedEllipse(context, pen,
                    ellipse.CenterX, ellipse.CenterY,
                    ellipse.Radius1, ellipse.Radius2, ellipse.RotationAngle);
                break;
            case ArcPrimitive arc:
                DrawArc(context, pen, arc);
                break;
            case PolygonPrimitive polygon:
                DrawPolygon(context, pen, polygon);
                break;
            case DxfPrimitive dxf:
                DrawComposite(context, pen, pointRadiusWorld, dxf.InsertX, dxf.InsertY, dxf.RotationAngle, dxf.Children);
                break;
            case CompositePrimitive composite:
                DrawComposite(context, pen, pointRadiusWorld, composite.InsertX, composite.InsertY, composite.RotationAngle, composite.Children);
                break;
        }
    }

    private static bool IsPointNearPrimitive(Point worldPoint, PrimitiveItem primitive, double tolerance)
    {
        switch (primitive)
        {
            case PointPrimitive p:
                // Для точки считаем «примерное попадание» радиусом ~5 пикселей экрана.
                // Здесь tolerance передаётся уже в мировых координатах (~2 пикселя),
                // поэтому используем максимум между текущим допуском и радиусом точки.
                // Поскольку радиус точки в мировых координатах равен 5/Scale,
                // а tolerance = 2/Scale, то 2 * tolerance ≈ 4/Scale близко к нужному.
                var pointTolerance = Math.Max(tolerance, tolerance * 2.5);
                return Distance(worldPoint, new Point(p.X, p.Y)) <= pointTolerance;

            case LinePrimitive line:
                return IsPointNearSegment(worldPoint,
                    new Point(line.X1, line.Y1),
                    new Point(line.X2, line.Y2),
                    tolerance);

            case CirclePrimitive circle:
            {
                var center = new Point(circle.CenterX, circle.CenterY);
                var dist = Distance(worldPoint, center);
                return Math.Abs(dist - circle.Radius) <= tolerance;
            }

            case ArcPrimitive arc:
            {
                var center = new Point(arc.CenterX, arc.CenterY);
                var v = worldPoint - center;
                var dist = Math.Sqrt(v.X * v.X + v.Y * v.Y);
                if (Math.Abs(dist - arc.Radius) > tolerance)
                    return false;

                var angle = Math.Atan2(v.Y, v.X) * 180.0 / Math.PI;
                var start = arc.StartAngle;
                var end = arc.EndAngle;
                if (end < start)
                    (start, end) = (end, start);
                return angle >= start - 0.5 && angle <= end + 0.5;
            }

            case RectanglePrimitive rect:
                return IsPointNearOrientedRectEdges(worldPoint,
                    new Point(rect.CenterX, rect.CenterY),
                    rect.Width,
                    rect.Height,
                    rect.RotationAngle,
                    tolerance);

            case EllipsePrimitive ellipse:
                return IsPointNearOrientedEllipse(worldPoint,
                    new Point(ellipse.CenterX, ellipse.CenterY),
                    ellipse.Radius1,
                    ellipse.Radius2,
                    ellipse.RotationAngle,
                    tolerance);

            case PolygonPrimitive polygon:
                return IsPointNearPolygonEdges(worldPoint, polygon, tolerance);

            case DxfPrimitive dxf:
            {
                var localPoint = new Point(worldPoint.X - dxf.InsertX, worldPoint.Y - dxf.InsertY);
                foreach (var child in dxf.Children)
                {
                    if (IsPointNearPrimitive(localPoint, child, tolerance))
                        return true;
                }
                return false;
            }

            case CompositePrimitive composite:
            {
                var localPoint = new Point(worldPoint.X - composite.InsertX, worldPoint.Y - composite.InsertY);
                foreach (var child in composite.Children)
                {
                    if (IsPointNearPrimitive(localPoint, child, tolerance))
                        return true;
                }
                return false;
            }

            default:
                return false;
        }
    }

    private static bool IsPointNearSegment(Point p, Point a, Point b, double tolerance)
    {
        var ab = b - a;
        var abLen2 = ab.X * ab.X + ab.Y * ab.Y;
        if (abLen2 < double.Epsilon)
            return Distance(p, a) <= tolerance;

        var t = ((p.X - a.X) * ab.X + (p.Y - a.Y) * ab.Y) / abLen2;
        t = Math.Clamp(t, 0, 1);
        var proj = new Point(a.X + t * ab.X, a.Y + t * ab.Y);
        return Distance(p, proj) <= tolerance;
    }

    private static double Distance(Point a, Point b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static bool IsPointNearOrientedRectEdges(
        Point p,
        Point center,
        double width,
        double height,
        double angleDeg,
        double tolerance)
    {
        // Строим те же 4 угла, что и при отрисовке прямоугольника
        var hw = width / 2.0;
        var hh = height / 2.0;
        var pts = new[]
        {
            new Point(-hw, -hh),
            new Point(hw, -hh),
            new Point(hw, hh),
            new Point(-hw, hh)
        };

        var angleRad = angleDeg * Math.PI / 180.0;
        var cos = Math.Cos(angleRad);
        var sin = Math.Sin(angleRad);

        for (var i = 0; i < pts.Length; i++)
        {
            var x = pts[i].X;
            var y = pts[i].Y;
            var rx = x * cos - y * sin + center.X;
            var ry = x * sin + y * cos + center.Y;
            pts[i] = new Point(rx, ry);
        }

        // Проверяем близость к каждому ребру
        for (int i = 0; i < pts.Length; i++)
        {
            var a = pts[i];
            var b = pts[(i + 1) % pts.Length];
            if (IsPointNearSegment(p, a, b, tolerance))
                return true;
        }

        return false;
    }

    private static bool IsPointNearOrientedEllipse(
        Point p,
        Point center,
        double radius1,
        double radius2,
        double angleDeg,
        double tolerance)
    {
        if (radius1 <= 0 || radius2 <= 0)
            return false;

        var angleRad = -angleDeg * Math.PI / 180.0;
        var cos = Math.Cos(angleRad);
        var sin = Math.Sin(angleRad);

        var dx = p.X - center.X;
        var dy = p.Y - center.Y;
        var localX = dx * cos - dy * sin;
        var localY = dx * sin + dy * cos;

        var value = (localX * localX) / (radius1 * radius1) + (localY * localY) / (radius2 * radius2);

        // value == 1 — на линии эллипса, допускаем небольшое кольцо вокруг
        var band = tolerance / Math.Max(radius1, radius2);
        return Math.Abs(value - 1.0) <= band;
    }

    private static bool IsPointNearPolygonEdges(Point p, PolygonPrimitive polygon, double tolerance)
    {
        if (polygon.SidesCount < 3 || polygon.CircumscribedRadius <= 0)
            return false;

        var pts = new Point[polygon.SidesCount];
        for (int i = 0; i < polygon.SidesCount; i++)
        {
            var angle = 2 * Math.PI * i / polygon.SidesCount;
            var x = polygon.CenterX + polygon.CircumscribedRadius * Math.Cos(angle);
            var y = polygon.CenterY + polygon.CircumscribedRadius * Math.Sin(angle);
            pts[i] = new Point(x, y);
        }

        // Проверяем близость к рёбрам
        for (int i = 0; i < pts.Length; i++)
        {
            var a = pts[i];
            var b = pts[(i + 1) % pts.Length];
            if (IsPointNearSegment(p, a, b, tolerance))
                return true;
        }

        return false;
    }

    private static void DrawPoint(DrawingContext context, Pen pen, PointPrimitive p, double radiusWorld)
    {
        var r = radiusWorld;

        // Диагональная линия через окружность
        context.DrawLine(pen,
            new Point(p.X - r, p.Y - r),
            new Point(p.X + r, p.Y + r));

        // Окружность
        DrawCircle(context, pen, p.X, p.Y, r);
    }

    private static void DrawCircle(DrawingContext context, Pen pen, double centerX, double centerY, double radius)
    {
        var rect = new Rect(centerX - radius, centerY - radius, radius * 2, radius * 2);
        context.DrawEllipse(null, pen, rect.Center, radius, radius);
    }

    private static void DrawOrientedRectangle(
        DrawingContext context,
        Pen pen,
        double cx,
        double cy,
        double width,
        double height,
        double angleDeg)
    {
        // Четыре угла прямоугольника до поворота (относительно центра)
        var hw = width / 2.0;
        var hh = height / 2.0;
        var pts = new[]
        {
            new Point(-hw, -hh),
            new Point(hw, -hh),
            new Point(hw, hh),
            new Point(-hw, hh)
        };

        var angleRad = angleDeg * Math.PI / 180.0;
        var cos = Math.Cos(angleRad);
        var sin = Math.Sin(angleRad);

        // Поворот и перенос в мировые координаты
        for (var i = 0; i < pts.Length; i++)
        {
            var x = pts[i].X;
            var y = pts[i].Y;
            var rx = x * cos - y * sin + cx;
            var ry = x * sin + y * cos + cy;
            pts[i] = new Point(rx, ry);
        }

        DrawPolyline(context, pen, pts, true);
    }

    private static void DrawOrientedEllipse(
        DrawingContext context,
        Pen pen,
        double cx,
        double cy,
        double radius1,
        double radius2,
        double angleDeg)
    {
        // При нулевом угле можно использовать стандартный вызов
        if (Math.Abs(angleDeg) < 0.001)
        {
            context.DrawEllipse(null, pen, new Point(cx, cy), radius1, radius2);
            return;
        }

        // Для повёрнутого эллипса аппроксимируем линиями
        const int segments = 64;
        var angleRad = angleDeg * Math.PI / 180.0;
        var cosRot = Math.Cos(angleRad);
        var sinRot = Math.Sin(angleRad);

        var pts = new Point[segments];
        for (int i = 0; i < segments; i++)
        {
            var t = 2 * Math.PI * i / segments;
            var x = radius1 * Math.Cos(t);
            var y = radius2 * Math.Sin(t);

            // Поворот
            var rx = x * cosRot - y * sinRot + cx;
            var ry = x * sinRot + y * cosRot + cy;
            pts[i] = new Point(rx, ry);
        }

        DrawPolyline(context, pen, pts, true);
    }

    private static void DrawArc(DrawingContext context, Pen pen, ArcPrimitive arc)
    {
        const int segments = 32;
        var startRad = arc.StartAngle * Math.PI / 180.0;
        var endRad = arc.EndAngle * Math.PI / 180.0;
        var total = endRad - startRad;

        if (Math.Abs(total) < 0.0001)
            return;

        var steps = Math.Max(2, (int)(segments * Math.Abs(total) / (2 * Math.PI)));
        var pts = new Point[steps + 1];

        for (int i = 0; i <= steps; i++)
        {
            var t = startRad + total * i / steps;
            var x = arc.CenterX + arc.Radius * Math.Cos(t);
            var y = arc.CenterY + arc.Radius * Math.Sin(t);
            pts[i] = new Point(x, y);
        }

        DrawPolyline(context, pen, pts, false);
    }

    private static void DrawPolygon(DrawingContext context, Pen pen, PolygonPrimitive polygon)
    {
        if (polygon.SidesCount < 3 || polygon.CircumscribedRadius <= 0)
            return;

        var pts = new Point[polygon.SidesCount];
        for (int i = 0; i < polygon.SidesCount; i++)
        {
            var angle = 2 * Math.PI * i / polygon.SidesCount;
            var x = polygon.CenterX + polygon.CircumscribedRadius * Math.Cos(angle);
            var y = polygon.CenterY + polygon.CircumscribedRadius * Math.Sin(angle);
            pts[i] = new Point(x, y);
        }

        DrawPolyline(context, pen, pts, true);
    }

    private static void DrawComposite(
        DrawingContext context,
        Pen basePen,
        double pointRadiusWorld,
        double insertX,
        double insertY,
        double rotationAngle,
        System.Collections.Generic.IEnumerable<PrimitiveItem> children)
    {
        if (children == null)
            return;

        var angleRad = rotationAngle * Math.PI / 180.0;
        var cos = Math.Cos(angleRad);
        var sin = Math.Sin(angleRad);

        // Локальная функция для трансформации точки
        Point Transform(Point p) =>
            new(
                insertX + p.X * cos - p.Y * sin,
                insertY + p.X * sin + p.Y * cos);

        // Для простоты: временно меняем систему координат через PushPostTransform
        using (context.PushPostTransform(
                   Matrix.CreateTranslation(-insertX, -insertY) *
                   Matrix.CreateRotation(rotationAngle * Math.PI / 180.0) *
                   Matrix.CreateTranslation(insertX, insertY)))
        {
            // Рисуем детей как обычные примитивы в их локальной системе
            foreach (var child in children)
            {
                switch (child)
                {
                    case PointPrimitive p:
                        DrawPoint(context, basePen, p, pointRadiusWorld);
                        break;
                    case LinePrimitive line:
                        context.DrawLine(basePen,
                            new Point(line.X1 + insertX, line.Y1 + insertY),
                            new Point(line.X2 + insertX, line.Y2 + insertY));
                        break;
                    case CirclePrimitive circle:
                        DrawCircle(context, basePen,
                            circle.CenterX + insertX,
                            circle.CenterY + insertY,
                            circle.Radius);
                        break;
                    case RectanglePrimitive rect:
                        DrawOrientedRectangle(context, basePen,
                            rect.CenterX + insertX,
                            rect.CenterY + insertY,
                            rect.Width,
                            rect.Height,
                            rect.RotationAngle + rotationAngle);
                        break;
                    case EllipsePrimitive ellipse:
                        DrawOrientedEllipse(context, basePen,
                            ellipse.CenterX + insertX,
                            ellipse.CenterY + insertY,
                            ellipse.Radius1,
                            ellipse.Radius2,
                            ellipse.RotationAngle + rotationAngle);
                        break;
                    case ArcPrimitive arc:
                        DrawArc(context, basePen, new ArcPrimitive(
                            arc.Name,
                            arc.CenterX + insertX,
                            arc.CenterY + insertY,
                            arc.Radius,
                            arc.StartAngle + rotationAngle,
                            arc.EndAngle + rotationAngle));
                        break;
                    case PolygonPrimitive polygon:
                        DrawPolygon(context, basePen, new PolygonPrimitive(
                            polygon.Name,
                            polygon.CenterX + insertX,
                            polygon.CenterY + insertY,
                            polygon.CircumscribedRadius,
                            polygon.SidesCount));
                        break;
                }
            }
        }
    }

    private static void DrawPolyline(DrawingContext context, Pen pen, Point[] points, bool closed)
    {
        if (points.Length < 2)
            return;

        for (int i = 0; i < points.Length - 1; i++)
        {
            context.DrawLine(pen, points[i], points[i + 1]);
        }

        if (closed)
        {
            context.DrawLine(pen, points[^1], points[0]);
        }
    }
}

