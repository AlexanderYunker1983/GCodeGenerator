using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
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
        // Подписываемся на новый ViewModel
        _viewModel = DataContext as Preview2DViewModel;
        _isInitialized = false;
        
        if (_viewModel != null)
        {
            // Ждем следующего кадра для получения правильных размеров
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                TryInitializeOffset();
            }, Avalonia.Threading.DispatcherPriority.Loaded);
        }
        
        InvalidateVisual();
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

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        
        // Получаем фокус для корректной обработки событий
        Focus();
        
        var point = e.GetCurrentPoint(this);
        if (point.Properties.IsLeftButtonPressed)
        {
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
        
        // Обновляем координаты мыши в мировых координатах
        if (_viewModel != null)
        {
            var mousePosition = e.GetPosition(this);
            var inverseScale = 1.0 / _viewModel.Scale;
            var worldX = (mousePosition.X - _viewModel.Offset.X) * inverseScale;
            var worldY = (mousePosition.Y - _viewModel.Offset.Y) * inverseScale;
            _viewModel.MouseWorldCoordinates = new Point(worldX, worldY);
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
            var worldY = (mousePosition.Y - _viewModel.Offset.Y) * inverseScale;
            _viewModel.MouseWorldCoordinates = new Point(worldX, worldY);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        
        // Очищаем координаты мыши когда мышь покидает канвас
        if (_viewModel != null)
        {
            _viewModel.MouseWorldCoordinates = null;
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
        // Формула: world = (screen - offset) / scale
        var oldCenterX = _previousSize.Width / 2;
        var oldCenterY = _previousSize.Height / 2;
        var worldX = (oldCenterX - _viewModel.Offset.X) / _viewModel.Scale;
        var worldY = (oldCenterY - _viewModel.Offset.Y) / _viewModel.Scale;
        
        // Вычисляем новый offset так, чтобы эта же точка осталась в центре нового контрола
        // Формула: screen = world * scale + offset => offset = screen - world * scale
        var newCenterX = e.NewSize.Width / 2;
        var newCenterY = e.NewSize.Height / 2;
        _viewModel.Offset = new Point(
            newCenterX - worldX * _viewModel.Scale,
            newCenterY - worldY * _viewModel.Scale
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
            // Формула преобразования: screen = (world * scale) + offset
            // В Avalonia матрицы умножаются: A * B означает сначала B, потом A
            // Нам нужно: Scale * Translation = сначала Scale (world * scale), потом Translation (+ offset)
            using (context.PushTransform(
                Matrix.CreateScale(_viewModel.Scale, _viewModel.Scale) *
                Matrix.CreateTranslation(_viewModel.Offset.X, _viewModel.Offset.Y)))
            {
                DrawGrid(context, bounds, _viewModel);
            }
        }
    }

    private void DrawGrid(DrawingContext context, Rect bounds, Preview2DViewModel viewModel)
    {
        var inverseScale = 1.0 / viewModel.Scale;
        
        // Преобразуем границы экрана в логические координаты
        var worldLeft = (0 - viewModel.Offset.X) * inverseScale;
        var worldTop = (0 - viewModel.Offset.Y) * inverseScale;
        var worldRight = (bounds.Width - viewModel.Offset.X) * inverseScale;
        var worldBottom = (bounds.Height - viewModel.Offset.Y) * inverseScale;
        
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
}

