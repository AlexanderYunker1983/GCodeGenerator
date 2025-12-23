using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using System.ComponentModel;
using GCodeGenerator.Core.Models;
using GCodeGenerator.Core.ViewModels.Preview2DViewModel;

namespace GCodeGenerator.Core.Views.Preview2DView;

/// <summary>
/// Тип ручки примитива для перетаскивания.
/// </summary>
internal enum HandleType
{
    Center,           // Центральная ручка для перемещения всего примитива
    LineEnd1,         // Первый конец линии
    LineEnd2,         // Второй конец линии
    CircleRadius,     // Ручка для изменения радиуса круга (слева на оси X)
    RectWidth,        // Ручка для изменения ширины прямоугольника (справа, посередине)
    RectHeight,       // Ручка для изменения высоты прямоугольника (сверху, посередине)
    RectRotation,     // Ручка для поворота прямоугольника (в углу между правой и верхней сторонами)
    EllipseRadius1,   // Ручка для изменения первого радиуса эллипса (справа на оси X в неповернутом состоянии)
    EllipseRadius2,   // Ручка для изменения второго радиуса эллипса (сверху на оси Y в неповернутом состоянии)
    EllipseRotation,      // Ручка для поворота эллипса (в углу описанного прямоугольника)
    DxfRotation,          // Ручка для поворота DXF-объекта (на границе габарита справа)
    CompositeRotation,    // Ручка для поворота составного объекта (на границе габарита справа)
    PolygonRadius,       // Ручка для изменения радиуса многоугольника (в правой вершине)
    PolygonRotation,      // Ручка для поворота многоугольника (в углу габаритного квадрата)
    ArcStartAngle,       // Ручка для изменения начального угла дуги (в начале дуги)
    ArcEndAngle,         // Ручка для изменения конечного угла дуги (в конце дуги)
    ArcRadius            // Ручка для изменения радиуса дуги (посередине дуги)
}

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
    
    // Для поддержки pinch-to-zoom на тачпаде
    private readonly Dictionary<IPointer, Point> _activePointers = new();
    private double _initialDistance;
    private double _initialScale;
    private Point _initialOffset;
    private Point _initialCenter;
    
    // Для перетаскивания примитивов за ручку
    private bool _isDragging;
    private Point _dragStartWorld;
    private Point _dragStartCenter;
    private PrimitiveItem? _draggedPrimitive;
    private HandleType _draggedHandleType = HandleType.Center;
    
    // Для отслеживания наведения мыши на ручку
    private HandleType? _hoveredHandleType;
    
    // Для перетаскивания прямоугольника - начальные значения
    private double _dragStartWidth;
    private double _dragStartHeight;
    private double _dragStartRotationAngle;
    
    // Для перетаскивания эллипса - начальные значения
    private double _dragStartRadius1;
    private double _dragStartRadius2;
    private double _dragStartEllipseRotationAngle;
    
    // Для перетаскивания DXF и Composite - начальный угол поворота
    private double _dragStartDxfRotationAngle;
    private double _dragStartCompositeRotationAngle;
    
    // Для перетаскивания многоугольника - начальные значения
    private double _dragStartPolygonRadius;
    private double _dragStartPolygonRotationAngle;
    
    // Для перетаскивания дуги - начальные значения
    private double _dragStartArcRadius;
    private double _dragStartArcStartAngle;
    private double _dragStartArcEndAngle;

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
            // Сбрасываем наведенную ручку при изменении выделенного примитива
            if (e.PropertyName == nameof(Preview2DViewModel.SelectedPrimitive))
            {
                _hoveredHandleType = null;
            }
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

    /// <summary>
    /// Получает центр примитива в мировых координатах.
    /// </summary>
    private static Point GetPrimitiveCenter(PrimitiveItem primitive)
    {
        return primitive switch
        {
            PointPrimitive p => new Point(p.X, p.Y),
            LinePrimitive line => new Point((line.X1 + line.X2) / 2.0, (line.Y1 + line.Y2) / 2.0),
            CirclePrimitive circle => new Point(circle.CenterX, circle.CenterY),
            RectanglePrimitive rect => new Point(rect.CenterX, rect.CenterY),
            EllipsePrimitive ellipse => new Point(ellipse.CenterX, ellipse.CenterY),
            ArcPrimitive arc => new Point(arc.CenterX, arc.CenterY),
            PolygonPrimitive polygon => new Point(polygon.CenterX, polygon.CenterY),
            DxfPrimitive dxf => new Point(dxf.InsertX, dxf.InsertY),
            CompositePrimitive composite => new Point(composite.InsertX, composite.InsertY),
            _ => new Point(0, 0)
        };
    }

    /// <summary>
    /// Вычисляет габарит (bounding box) для коллекции примитивов в их локальной системе координат.
    /// </summary>
    private static (double minX, double minY, double maxX, double maxY) GetBoundingBox(
        System.Collections.Generic.IEnumerable<PrimitiveItem> children)
    {
        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;
        bool hasPoints = false;

        foreach (var child in children)
        {
            switch (child)
            {
                case PointPrimitive p:
                    minX = Math.Min(minX, p.X);
                    minY = Math.Min(minY, p.Y);
                    maxX = Math.Max(maxX, p.X);
                    maxY = Math.Max(maxY, p.Y);
                    hasPoints = true;
                    break;
                case LinePrimitive line:
                    minX = Math.Min(minX, Math.Min(line.X1, line.X2));
                    minY = Math.Min(minY, Math.Min(line.Y1, line.Y2));
                    maxX = Math.Max(maxX, Math.Max(line.X1, line.X2));
                    maxY = Math.Max(maxY, Math.Max(line.Y1, line.Y2));
                    hasPoints = true;
                    break;
                case CirclePrimitive circle:
                    minX = Math.Min(minX, circle.CenterX - circle.Radius);
                    minY = Math.Min(minY, circle.CenterY - circle.Radius);
                    maxX = Math.Max(maxX, circle.CenterX + circle.Radius);
                    maxY = Math.Max(maxY, circle.CenterY + circle.Radius);
                    hasPoints = true;
                    break;
                case RectanglePrimitive rect:
                    var hw = rect.Width / 2.0;
                    var hh = rect.Height / 2.0;
                    var angleRad = rect.RotationAngle * Math.PI / 180.0;
                    var cos = Math.Cos(angleRad);
                    var sin = Math.Sin(angleRad);
                    var corners = new[]
                    {
                        new Point(-hw, -hh),
                        new Point(hw, -hh),
                        new Point(hw, hh),
                        new Point(-hw, hh)
                    };
                    foreach (var corner in corners)
                    {
                        var x = corner.X * cos - corner.Y * sin + rect.CenterX;
                        var y = corner.X * sin + corner.Y * cos + rect.CenterY;
                        minX = Math.Min(minX, x);
                        minY = Math.Min(minY, y);
                        maxX = Math.Max(maxX, x);
                        maxY = Math.Max(maxY, y);
                    }
                    hasPoints = true;
                    break;
                case EllipsePrimitive ellipse:
                    minX = Math.Min(minX, ellipse.CenterX - ellipse.Radius1);
                    minY = Math.Min(minY, ellipse.CenterY - ellipse.Radius2);
                    maxX = Math.Max(maxX, ellipse.CenterX + ellipse.Radius1);
                    maxY = Math.Max(maxY, ellipse.CenterY + ellipse.Radius2);
                    hasPoints = true;
                    break;
                case ArcPrimitive arc:
                    // Для дуги используем простую аппроксимацию
                    minX = Math.Min(minX, arc.CenterX - arc.Radius);
                    minY = Math.Min(minY, arc.CenterY - arc.Radius);
                    maxX = Math.Max(maxX, arc.CenterX + arc.Radius);
                    maxY = Math.Max(maxY, arc.CenterY + arc.Radius);
                    hasPoints = true;
                    break;
                case PolygonPrimitive polygon:
                    var radius = polygon.CircumscribedRadius;
                    minX = Math.Min(minX, polygon.CenterX - radius);
                    minY = Math.Min(minY, polygon.CenterY - radius);
                    maxX = Math.Max(maxX, polygon.CenterX + radius);
                    maxY = Math.Max(maxY, polygon.CenterY + radius);
                    hasPoints = true;
                    break;
            }
        }

        if (!hasPoints)
        {
            return (0, 0, 0, 0);
        }

        return (minX, minY, maxX, maxY);
    }

    /// <summary>
    /// Вычисляет позицию ручки поворота для DXF или Composite примитива.
    /// Ручка находится на правой границе габарита в неповернутом состоянии,
    /// на пересечении с горизонтальной линией через центр габарита.
    /// </summary>
    private static Point GetCompositeRotationHandle(DxfPrimitive dxf)
    {
        var insertPoint = new Point(dxf.InsertX, dxf.InsertY);
        if (dxf.Children.Count == 0)
        {
            return insertPoint;
        }

        var (minX, minY, maxX, maxY) = GetBoundingBox(dxf.Children);
        var centerY = (minY + maxY) / 2.0;
        
        // В неповернутом состоянии ручка на правой границе габарита
        // относительно центра габарита (не InsertX/InsertY)
        var handleLocalX = maxX;
        var handleLocalY = centerY;
        
        // Применяем поворот относительно InsertX/InsertY
        var angleRad = dxf.RotationAngle * Math.PI / 180.0;
        var cos = Math.Cos(angleRad);
        var sin = Math.Sin(angleRad);
        
        var handleX = handleLocalX * cos - handleLocalY * sin + insertPoint.X;
        var handleY = handleLocalX * sin + handleLocalY * cos + insertPoint.Y;
        
        return new Point(handleX, handleY);
    }

    /// <summary>
    /// Вычисляет позицию ручки поворота для Composite примитива.
    /// </summary>
    private static Point GetCompositeRotationHandle(CompositePrimitive composite)
    {
        var insertPoint = new Point(composite.InsertX, composite.InsertY);
        if (composite.Children.Count == 0)
        {
            return insertPoint;
        }

        var (minX, minY, maxX, maxY) = GetBoundingBox(composite.Children);
        var centerY = (minY + maxY) / 2.0;
        
        // В неповернутом состоянии ручка на правой границе габарита
        // относительно центра габарита (не InsertX/InsertY)
        var handleLocalX = maxX;
        var handleLocalY = centerY;
        
        // Применяем поворот относительно InsertX/InsertY
        var angleRad = composite.RotationAngle * Math.PI / 180.0;
        var cos = Math.Cos(angleRad);
        var sin = Math.Sin(angleRad);
        
        var handleX = handleLocalX * cos - handleLocalY * sin + insertPoint.X;
        var handleY = handleLocalX * sin + handleLocalY * cos + insertPoint.Y;
        
        return new Point(handleX, handleY);
    }

    /// <summary>
    /// Нормализует углы дуги так, чтобы дуга всегда строилась против часовой стрелки.
    /// Если EndAngle < StartAngle, добавляет 360 к EndAngle.
    /// </summary>
    private static (double normalizedStartAngle, double normalizedEndAngle) NormalizeArcAngles(
        double startAngle, double endAngle)
    {
        var normalizedStart = startAngle;
        var normalizedEnd = endAngle;
        
        // Нормализуем углы в диапазон [0, 360)
        while (normalizedStart < 0) normalizedStart += 360;
        while (normalizedStart >= 360) normalizedStart -= 360;
        while (normalizedEnd < 0) normalizedEnd += 360;
        while (normalizedEnd >= 360) normalizedEnd -= 360;
        
        // Если EndAngle < StartAngle, добавляем 360 к EndAngle для построения против часовой стрелки
        if (normalizedEnd < normalizedStart)
        {
            normalizedEnd += 360;
        }
        
        return (normalizedStart, normalizedEnd);
    }

    /// <summary>
    /// Вычисляет позиции ручек дуги.
    /// </summary>
    private static (Point center, Point startAngleHandle, Point endAngleHandle, Point radiusHandle) GetArcHandles(
        ArcPrimitive arc)
    {
        var center = new Point(arc.CenterX, arc.CenterY);
        
        // Нормализуем углы для правильного вычисления ручек
        var (normalizedStart, normalizedEnd) = NormalizeArcAngles(arc.StartAngle, arc.EndAngle);
        
        // Ручка начального угла: в точке дуги с углом StartAngle
        var startAngleRad = normalizedStart * Math.PI / 180.0;
        var startHandleX = center.X + arc.Radius * Math.Cos(startAngleRad);
        var startHandleY = center.Y + arc.Radius * Math.Sin(startAngleRad);
        var startAngleHandle = new Point(startHandleX, startHandleY);
        
        // Ручка конечного угла: в точке дуги с углом EndAngle
        // Используем нормализованный EndAngle, но если он > 360, берем его по модулю 360 для отображения ручки
        var endAngleForHandle = normalizedEnd;
        if (endAngleForHandle >= 360)
        {
            endAngleForHandle = endAngleForHandle % 360;
        }
        var endAngleForHandleRad = endAngleForHandle * Math.PI / 180.0;
        var endHandleX = center.X + arc.Radius * Math.Cos(endAngleForHandleRad);
        var endHandleY = center.Y + arc.Radius * Math.Sin(endAngleForHandleRad);
        var endAngleHandle = new Point(endHandleX, endHandleY);
        
        // Ручка радиуса: посередине дуги (средний угол между нормализованными StartAngle и EndAngle)
        var endAngleRad = normalizedEnd * Math.PI / 180.0;
        var midAngleRad = (startAngleRad + endAngleRad) / 2.0;
        var radiusHandleX = center.X + arc.Radius * Math.Cos(midAngleRad);
        var radiusHandleY = center.Y + arc.Radius * Math.Sin(midAngleRad);
        var radiusHandle = new Point(radiusHandleX, radiusHandleY);
        
        return (center, startAngleHandle, endAngleHandle, radiusHandle);
    }

    /// <summary>
    /// Вычисляет позиции ручек многоугольника с учетом поворота.
    /// </summary>
    private static (Point center, Point radiusHandle, Point rotationHandle) GetPolygonHandles(
        PolygonPrimitive polygon)
    {
        var center = new Point(polygon.CenterX, polygon.CenterY);
        var angleRad = polygon.RotationAngle * Math.PI / 180.0;
        var cos = Math.Cos(angleRad);
        var sin = Math.Sin(angleRad);

        // Ручка радиуса: в правой вершине (угол = 0 в неповернутом состоянии)
        var radiusHandleLocal = new Point(polygon.CircumscribedRadius, 0);
        var radiusHandleX = radiusHandleLocal.X * cos - radiusHandleLocal.Y * sin + center.X;
        var radiusHandleY = radiusHandleLocal.X * sin + radiusHandleLocal.Y * cos + center.Y;
        var radiusHandle = new Point(radiusHandleX, radiusHandleY);

        // Ручка поворота: в углу габаритного квадрата (справа-снизу, т.е. (radius, radius) в неповернутом состоянии)
        var rotationHandleLocal = new Point(polygon.CircumscribedRadius, polygon.CircumscribedRadius);
        var rotationHandleX = rotationHandleLocal.X * cos - rotationHandleLocal.Y * sin + center.X;
        var rotationHandleY = rotationHandleLocal.X * sin + rotationHandleLocal.Y * cos + center.Y;
        var rotationHandle = new Point(rotationHandleX, rotationHandleY);

        return (center, radiusHandle, rotationHandle);
    }

    /// <summary>
    /// Вычисляет позиции ручек эллипса с учетом поворота.
    /// </summary>
    private static (Point center, Point radius1Handle, Point radius2Handle, Point rotationHandle) GetEllipseHandles(
        EllipsePrimitive ellipse)
    {
        var center = new Point(ellipse.CenterX, ellipse.CenterY);
        var angleRad = ellipse.RotationAngle * Math.PI / 180.0;
        var cos = Math.Cos(angleRad);
        var sin = Math.Sin(angleRad);

        // Ручка первого радиуса: справа на оси X (в неповернутом состоянии)
        var radius1HandleLocal = new Point(ellipse.Radius1, 0);
        var radius1HandleX = radius1HandleLocal.X * cos - radius1HandleLocal.Y * sin + center.X;
        var radius1HandleY = radius1HandleLocal.X * sin + radius1HandleLocal.Y * cos + center.Y;
        var radius1Handle = new Point(radius1HandleX, radius1HandleY);

        // Ручка второго радиуса: сверху на оси Y (в неповернутом состоянии)
        var radius2HandleLocal = new Point(0, -ellipse.Radius2);
        var radius2HandleX = radius2HandleLocal.X * cos - radius2HandleLocal.Y * sin + center.X;
        var radius2HandleY = radius2HandleLocal.X * sin + radius2HandleLocal.Y * cos + center.Y;
        var radius2Handle = new Point(radius2HandleX, radius2HandleY);

        // Ручка поворота: в углу описанного прямоугольника (справа-сверху в неповернутом состоянии)
        var rotationHandleLocal = new Point(ellipse.Radius1, -ellipse.Radius2);
        var rotationHandleX = rotationHandleLocal.X * cos - rotationHandleLocal.Y * sin + center.X;
        var rotationHandleY = rotationHandleLocal.X * sin + rotationHandleLocal.Y * cos + center.Y;
        var rotationHandle = new Point(rotationHandleX, rotationHandleY);

        return (center, radius1Handle, radius2Handle, rotationHandle);
    }

    /// <summary>
    /// Вычисляет позиции ручек прямоугольника с учетом поворота.
    /// </summary>
    private static (Point center, Point widthHandle, Point heightHandle, Point rotationHandle) GetRectangleHandles(
        RectanglePrimitive rect)
    {
        var center = new Point(rect.CenterX, rect.CenterY);
        var hw = rect.Width / 2.0;
        var hh = rect.Height / 2.0;
        var angleRad = rect.RotationAngle * Math.PI / 180.0;
        var cos = Math.Cos(angleRad);
        var sin = Math.Sin(angleRad);

        // Ручка ширины: справа, посередине (в неповернутом состоянии)
        var widthHandleLocal = new Point(hw, 0);
        var widthHandleX = widthHandleLocal.X * cos - widthHandleLocal.Y * sin + center.X;
        var widthHandleY = widthHandleLocal.X * sin + widthHandleLocal.Y * cos + center.Y;
        var widthHandle = new Point(widthHandleX, widthHandleY);

        // Ручка высоты: сверху, посередине (в неповернутом состоянии)
        var heightHandleLocal = new Point(0, -hh);
        var heightHandleX = heightHandleLocal.X * cos - heightHandleLocal.Y * sin + center.X;
        var heightHandleY = heightHandleLocal.X * sin + heightHandleLocal.Y * cos + center.Y;
        var heightHandle = new Point(heightHandleX, heightHandleY);

        // Ручка поворота: в углу между правой и верхней сторонами (в неповернутом состоянии)
        var rotationHandleLocal = new Point(hw, -hh);
        var rotationHandleX = rotationHandleLocal.X * cos - rotationHandleLocal.Y * sin + center.X;
        var rotationHandleY = rotationHandleLocal.X * sin + rotationHandleLocal.Y * cos + center.Y;
        var rotationHandle = new Point(rotationHandleX, rotationHandleY);

        return (center, widthHandle, heightHandle, rotationHandle);
    }

    /// <summary>
    /// Проверяет, находится ли точка на ручке примитива (квадрат 5x5 пикселей).
    /// Возвращает тип ручки или null, если точка не на ручке.
    /// </summary>
    private HandleType? GetHandleAtPoint(Point worldPoint, PrimitiveItem primitive, double scale)
    {
        if (_viewModel == null)
            return null;

        // Размер ручки в мировых координатах (5 пикселей независимо от масштаба)
        var handleSizeWorld = 5.0 / scale;
        var halfSize = handleSizeWorld / 2.0;

        // Для линии проверяем ручки на концах
        if (primitive is LinePrimitive line)
        {
            // Проверяем первый конец
            var end1 = new Point(line.X1, line.Y1);
            if (worldPoint.X >= end1.X - halfSize &&
                worldPoint.X <= end1.X + halfSize &&
                worldPoint.Y >= end1.Y - halfSize &&
                worldPoint.Y <= end1.Y + halfSize)
            {
                return HandleType.LineEnd1;
            }

            // Проверяем второй конец
            var end2 = new Point(line.X2, line.Y2);
            if (worldPoint.X >= end2.X - halfSize &&
                worldPoint.X <= end2.X + halfSize &&
                worldPoint.Y >= end2.Y - halfSize &&
                worldPoint.Y <= end2.Y + halfSize)
            {
                return HandleType.LineEnd2;
            }

            // Проверяем центральную ручку
            var center = GetPrimitiveCenter(primitive);
            if (worldPoint.X >= center.X - halfSize &&
                worldPoint.X <= center.X + halfSize &&
                worldPoint.Y >= center.Y - halfSize &&
                worldPoint.Y <= center.Y + halfSize)
            {
                return HandleType.Center;
            }

            return null;
        }

        // Для прямоугольника проверяем ручки с учетом поворота
        if (primitive is RectanglePrimitive rect)
        {
            var (center, widthHandle, heightHandle, rotationHandle) = GetRectangleHandles(rect);
            
            // Проверяем ручку ширины (справа, посередине)
            if (worldPoint.X >= widthHandle.X - halfSize &&
                worldPoint.X <= widthHandle.X + halfSize &&
                worldPoint.Y >= widthHandle.Y - halfSize &&
                worldPoint.Y <= widthHandle.Y + halfSize)
            {
                return HandleType.RectWidth;
            }

            // Проверяем ручку высоты (сверху, посередине)
            if (worldPoint.X >= heightHandle.X - halfSize &&
                worldPoint.X <= heightHandle.X + halfSize &&
                worldPoint.Y >= heightHandle.Y - halfSize &&
                worldPoint.Y <= heightHandle.Y + halfSize)
            {
                return HandleType.RectHeight;
            }

            // Проверяем ручку поворота (в углу)
            if (worldPoint.X >= rotationHandle.X - halfSize &&
                worldPoint.X <= rotationHandle.X + halfSize &&
                worldPoint.Y >= rotationHandle.Y - halfSize &&
                worldPoint.Y <= rotationHandle.Y + halfSize)
            {
                return HandleType.RectRotation;
            }

            // Проверяем центральную ручку
            if (worldPoint.X >= center.X - halfSize &&
                worldPoint.X <= center.X + halfSize &&
                worldPoint.Y >= center.Y - halfSize &&
                worldPoint.Y <= center.Y + halfSize)
            {
                return HandleType.Center;
            }

            return null;
        }

        // Для эллипса проверяем ручки с учетом поворота
        if (primitive is EllipsePrimitive ellipse)
        {
            var (center, radius1Handle, radius2Handle, rotationHandle) = GetEllipseHandles(ellipse);
            
            // Проверяем ручку первого радиуса (справа на оси X в неповернутом состоянии)
            if (worldPoint.X >= radius1Handle.X - halfSize &&
                worldPoint.X <= radius1Handle.X + halfSize &&
                worldPoint.Y >= radius1Handle.Y - halfSize &&
                worldPoint.Y <= radius1Handle.Y + halfSize)
            {
                return HandleType.EllipseRadius1;
            }

            // Проверяем ручку второго радиуса (сверху на оси Y в неповернутом состоянии)
            if (worldPoint.X >= radius2Handle.X - halfSize &&
                worldPoint.X <= radius2Handle.X + halfSize &&
                worldPoint.Y >= radius2Handle.Y - halfSize &&
                worldPoint.Y <= radius2Handle.Y + halfSize)
            {
                return HandleType.EllipseRadius2;
            }

            // Проверяем ручку поворота (в углу описанного прямоугольника)
            if (worldPoint.X >= rotationHandle.X - halfSize &&
                worldPoint.X <= rotationHandle.X + halfSize &&
                worldPoint.Y >= rotationHandle.Y - halfSize &&
                worldPoint.Y <= rotationHandle.Y + halfSize)
            {
                return HandleType.EllipseRotation;
            }

            // Проверяем центральную ручку
            if (worldPoint.X >= center.X - halfSize &&
                worldPoint.X <= center.X + halfSize &&
                worldPoint.Y >= center.Y - halfSize &&
                worldPoint.Y <= center.Y + halfSize)
            {
                return HandleType.Center;
            }

            return null;
        }

        // Для круга проверяем ручку радиуса (слева на оси X) и центральную ручку
        if (primitive is CirclePrimitive circle)
        {
            // Проверяем ручку радиуса (слева от центра на оси X)
            var radiusHandlePoint = new Point(circle.CenterX - circle.Radius, circle.CenterY);
            if (worldPoint.X >= radiusHandlePoint.X - halfSize &&
                worldPoint.X <= radiusHandlePoint.X + halfSize &&
                worldPoint.Y >= radiusHandlePoint.Y - halfSize &&
                worldPoint.Y <= radiusHandlePoint.Y + halfSize)
            {
                return HandleType.CircleRadius;
            }

            // Проверяем центральную ручку
            var center = GetPrimitiveCenter(primitive);
            if (worldPoint.X >= center.X - halfSize &&
                worldPoint.X <= center.X + halfSize &&
                worldPoint.Y >= center.Y - halfSize &&
                worldPoint.Y <= center.Y + halfSize)
            {
                return HandleType.Center;
            }

            return null;
        }

        // Для дуги проверяем ручки
        if (primitive is ArcPrimitive arc)
        {
            var (center, startAngleHandle, endAngleHandle, radiusHandle) = GetArcHandles(arc);
            
            // Проверяем ручку начального угла (в начале дуги)
            if (worldPoint.X >= startAngleHandle.X - halfSize &&
                worldPoint.X <= startAngleHandle.X + halfSize &&
                worldPoint.Y >= startAngleHandle.Y - halfSize &&
                worldPoint.Y <= startAngleHandle.Y + halfSize)
            {
                return HandleType.ArcStartAngle;
            }

            // Проверяем ручку конечного угла (в конце дуги)
            if (worldPoint.X >= endAngleHandle.X - halfSize &&
                worldPoint.X <= endAngleHandle.X + halfSize &&
                worldPoint.Y >= endAngleHandle.Y - halfSize &&
                worldPoint.Y <= endAngleHandle.Y + halfSize)
            {
                return HandleType.ArcEndAngle;
            }

            // Проверяем ручку радиуса (посередине дуги)
            if (worldPoint.X >= radiusHandle.X - halfSize &&
                worldPoint.X <= radiusHandle.X + halfSize &&
                worldPoint.Y >= radiusHandle.Y - halfSize &&
                worldPoint.Y <= radiusHandle.Y + halfSize)
            {
                return HandleType.ArcRadius;
            }

            // Проверяем центральную ручку
            if (worldPoint.X >= center.X - halfSize &&
                worldPoint.X <= center.X + halfSize &&
                worldPoint.Y >= center.Y - halfSize &&
                worldPoint.Y <= center.Y + halfSize)
            {
                return HandleType.Center;
            }

            return null;
        }

        // Для многоугольника проверяем ручки с учетом поворота
        if (primitive is PolygonPrimitive polygon)
        {
            var (center, radiusHandle, rotationHandle) = GetPolygonHandles(polygon);
            
            // Проверяем ручку радиуса (в правой вершине)
            if (worldPoint.X >= radiusHandle.X - halfSize &&
                worldPoint.X <= radiusHandle.X + halfSize &&
                worldPoint.Y >= radiusHandle.Y - halfSize &&
                worldPoint.Y <= radiusHandle.Y + halfSize)
            {
                return HandleType.PolygonRadius;
            }

            // Проверяем ручку поворота (в углу габаритного квадрата)
            if (worldPoint.X >= rotationHandle.X - halfSize &&
                worldPoint.X <= rotationHandle.X + halfSize &&
                worldPoint.Y >= rotationHandle.Y - halfSize &&
                worldPoint.Y <= rotationHandle.Y + halfSize)
            {
                return HandleType.PolygonRotation;
            }

            // Проверяем центральную ручку
            if (worldPoint.X >= center.X - halfSize &&
                worldPoint.X <= center.X + halfSize &&
                worldPoint.Y >= center.Y - halfSize &&
                worldPoint.Y <= center.Y + halfSize)
            {
                return HandleType.Center;
            }

            return null;
        }

        // Для DXF-объекта проверяем ручку поворота и центральную ручку
        if (primitive is DxfPrimitive dxf)
        {
            var center = GetPrimitiveCenter(primitive);
            var rotationHandle = GetCompositeRotationHandle(dxf);
            
            // Проверяем ручку поворота
            if (worldPoint.X >= rotationHandle.X - halfSize &&
                worldPoint.X <= rotationHandle.X + halfSize &&
                worldPoint.Y >= rotationHandle.Y - halfSize &&
                worldPoint.Y <= rotationHandle.Y + halfSize)
            {
                return HandleType.DxfRotation;
            }

            // Проверяем центральную ручку
            if (worldPoint.X >= center.X - halfSize &&
                worldPoint.X <= center.X + halfSize &&
                worldPoint.Y >= center.Y - halfSize &&
                worldPoint.Y <= center.Y + halfSize)
            {
                return HandleType.Center;
            }

            return null;
        }

        // Для составного объекта проверяем ручку поворота и центральную ручку
        if (primitive is CompositePrimitive composite)
        {
            var center = GetPrimitiveCenter(primitive);
            var rotationHandle = GetCompositeRotationHandle(composite);
            
            // Проверяем ручку поворота
            if (worldPoint.X >= rotationHandle.X - halfSize &&
                worldPoint.X <= rotationHandle.X + halfSize &&
                worldPoint.Y >= rotationHandle.Y - halfSize &&
                worldPoint.Y <= rotationHandle.Y + halfSize)
            {
                return HandleType.CompositeRotation;
            }

            // Проверяем центральную ручку
            if (worldPoint.X >= center.X - halfSize &&
                worldPoint.X <= center.X + halfSize &&
                worldPoint.Y >= center.Y - halfSize &&
                worldPoint.Y <= center.Y + halfSize)
            {
                return HandleType.Center;
            }

            return null;
        }

        // Для остальных примитивов проверяем только центральную ручку
        var centerPoint = GetPrimitiveCenter(primitive);
        if (worldPoint.X >= centerPoint.X - halfSize &&
            worldPoint.X <= centerPoint.X + halfSize &&
            worldPoint.Y >= centerPoint.Y - halfSize &&
            worldPoint.Y <= centerPoint.Y + halfSize)
        {
            return HandleType.Center;
        }

        return null;
    }

    /// <summary>
    /// Перемещает примитив на указанное смещение.
    /// </summary>
    private static void MovePrimitive(PrimitiveItem primitive, double deltaX, double deltaY)
    {
        switch (primitive)
        {
            case PointPrimitive p:
                p.X += deltaX;
                p.Y += deltaY;
                break;
            case LinePrimitive line:
                line.X1 += deltaX;
                line.Y1 += deltaY;
                line.X2 += deltaX;
                line.Y2 += deltaY;
                break;
            case CirclePrimitive circle:
                circle.CenterX += deltaX;
                circle.CenterY += deltaY;
                break;
            case RectanglePrimitive rect:
                rect.CenterX += deltaX;
                rect.CenterY += deltaY;
                break;
            case EllipsePrimitive ellipse:
                ellipse.CenterX += deltaX;
                ellipse.CenterY += deltaY;
                break;
            case ArcPrimitive arc:
                arc.CenterX += deltaX;
                arc.CenterY += deltaY;
                break;
            case PolygonPrimitive polygon:
                polygon.CenterX += deltaX;
                polygon.CenterY += deltaY;
                break;
            case DxfPrimitive dxf:
                dxf.InsertX += deltaX;
                dxf.InsertY += deltaY;
                break;
            case CompositePrimitive composite:
                composite.InsertX += deltaX;
                composite.InsertY += deltaY;
                break;
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        
        // Получаем фокус для корректной обработки событий
        Focus();
        
        var point = e.GetCurrentPoint(this);
        var position = point.Position;
        
        // Обработка pinch-to-zoom: отслеживаем указатели по их ID
        if (!_activePointers.ContainsKey(e.Pointer))
        {
            _activePointers[e.Pointer] = position;
            e.Pointer.Capture(this);
            
            // Если это второй указатель - начинаем pinch-to-zoom
            if (_activePointers.Count == 2 && _viewModel != null)
            {
                var pointers = new List<Point>(_activePointers.Values);
                _initialDistance = Distance(pointers[0], pointers[1]);
                _initialScale = _viewModel.Scale;
                _initialOffset = _viewModel.Offset;
                _initialCenter = new Point(
                    (pointers[0].X + pointers[1].X) / 2,
                    (pointers[0].Y + pointers[1].Y) / 2
                );
                e.Handled = true;
                return;
            }
        }
        
        // Не обрабатываем клики и панорамирование, если уже есть два указателя (pinch-to-zoom)
        if (_activePointers.Count >= 2)
        {
            return;
        }
        
        if (point.Properties.IsLeftButtonPressed)
        {
            // Если есть ViewModel, проверяем, нажата ли ручка выделенного примитива
            if (_viewModel != null && _viewModel.SelectedPrimitive != null)
            {
                var mousePosition = point.Position;
                var inverseScale = 1.0 / _viewModel.Scale;
                var worldX = (mousePosition.X - _viewModel.Offset.X) * inverseScale;
                var worldY = (_viewModel.Offset.Y - mousePosition.Y) * inverseScale;
                var worldPoint = new Point(worldX, worldY);

                // Проверяем, нажата ли ручка выделенного примитива
                var handleType = GetHandleAtPoint(worldPoint, _viewModel.SelectedPrimitive, _viewModel.Scale);
                if (handleType.HasValue)
                {
                    _isDragging = true;
                    _dragStartWorld = worldPoint;
                    _draggedHandleType = handleType.Value;
                    _hoveredHandleType = null; // Сбрасываем наведенную ручку при начале перетаскивания
                    
                    // Сохраняем начальную позицию в зависимости от типа ручки
                    if (_viewModel.SelectedPrimitive is LinePrimitive line)
                    {
                        if (handleType.Value == HandleType.LineEnd1)
                            _dragStartCenter = new Point(line.X1, line.Y1);
                        else if (handleType.Value == HandleType.LineEnd2)
                            _dragStartCenter = new Point(line.X2, line.Y2);
                        else
                            _dragStartCenter = GetPrimitiveCenter(_viewModel.SelectedPrimitive);
                    }
                    else if (_viewModel.SelectedPrimitive is CirclePrimitive circle)
                    {
                        if (handleType.Value == HandleType.CircleRadius)
                        {
                            // Сохраняем начальную позицию ручки радиуса и начальный радиус
                            _dragStartCenter = new Point(circle.CenterX - circle.Radius, circle.CenterY);
                        }
                        else
                        {
                            _dragStartCenter = GetPrimitiveCenter(_viewModel.SelectedPrimitive);
                        }
                    }
                    else if (_viewModel.SelectedPrimitive is RectanglePrimitive rect)
                    {
                        // Сохраняем начальные значения прямоугольника
                        _dragStartWidth = rect.Width;
                        _dragStartHeight = rect.Height;
                        _dragStartRotationAngle = rect.RotationAngle;
                        
                        var (center, widthHandle, heightHandle, rotationHandle) = GetRectangleHandles(rect);
                        if (handleType.Value == HandleType.RectWidth)
                            _dragStartCenter = widthHandle;
                        else if (handleType.Value == HandleType.RectHeight)
                            _dragStartCenter = heightHandle;
                        else if (handleType.Value == HandleType.RectRotation)
                            _dragStartCenter = rotationHandle;
                        else
                            _dragStartCenter = center;
                    }
                    else if (_viewModel.SelectedPrimitive is EllipsePrimitive ellipse)
                    {
                        // Сохраняем начальные значения эллипса
                        _dragStartRadius1 = ellipse.Radius1;
                        _dragStartRadius2 = ellipse.Radius2;
                        _dragStartEllipseRotationAngle = ellipse.RotationAngle;
                        
                        var (center, radius1Handle, radius2Handle, rotationHandle) = GetEllipseHandles(ellipse);
                        if (handleType.Value == HandleType.EllipseRadius1)
                            _dragStartCenter = radius1Handle;
                        else if (handleType.Value == HandleType.EllipseRadius2)
                            _dragStartCenter = radius2Handle;
                        else if (handleType.Value == HandleType.EllipseRotation)
                            _dragStartCenter = rotationHandle;
                        else
                            _dragStartCenter = center;
                    }
                    else if (_viewModel.SelectedPrimitive is DxfPrimitive dxf)
                    {
                        // Сохраняем начальный угол поворота DXF
                        _dragStartDxfRotationAngle = dxf.RotationAngle;
                        
                        var center = GetPrimitiveCenter(_viewModel.SelectedPrimitive);
                        if (handleType.Value == HandleType.DxfRotation)
                            _dragStartCenter = GetCompositeRotationHandle(dxf);
                        else
                            _dragStartCenter = center;
                    }
                    else if (_viewModel.SelectedPrimitive is CompositePrimitive composite)
                    {
                        // Сохраняем начальный угол поворота Composite
                        _dragStartCompositeRotationAngle = composite.RotationAngle;
                        
                        var center = GetPrimitiveCenter(_viewModel.SelectedPrimitive);
                        if (handleType.Value == HandleType.CompositeRotation)
                            _dragStartCenter = GetCompositeRotationHandle(composite);
                        else
                            _dragStartCenter = center;
                    }
                    else if (_viewModel.SelectedPrimitive is PolygonPrimitive polygon)
                    {
                        // Сохраняем начальные значения многоугольника
                        _dragStartPolygonRadius = polygon.CircumscribedRadius;
                        _dragStartPolygonRotationAngle = polygon.RotationAngle;
                        
                        var (center, radiusHandle, rotationHandle) = GetPolygonHandles(polygon);
                        if (handleType.Value == HandleType.PolygonRadius)
                            _dragStartCenter = radiusHandle;
                        else if (handleType.Value == HandleType.PolygonRotation)
                            _dragStartCenter = rotationHandle;
                        else
                            _dragStartCenter = center;
                    }
                    else if (_viewModel.SelectedPrimitive is ArcPrimitive arc)
                    {
                        // Сохраняем начальные значения дуги
                        _dragStartArcRadius = arc.Radius;
                        _dragStartArcStartAngle = arc.StartAngle;
                        _dragStartArcEndAngle = arc.EndAngle;
                        
                        var (center, startAngleHandle, endAngleHandle, radiusHandle) = GetArcHandles(arc);
                        if (handleType.Value == HandleType.ArcStartAngle)
                            _dragStartCenter = startAngleHandle;
                        else if (handleType.Value == HandleType.ArcEndAngle)
                            _dragStartCenter = endAngleHandle;
                        else if (handleType.Value == HandleType.ArcRadius)
                            _dragStartCenter = radiusHandle;
                        else
                            _dragStartCenter = center;
                    }
                    else
                    {
                        _dragStartCenter = GetPrimitiveCenter(_viewModel.SelectedPrimitive);
                    }
                    
                    _draggedPrimitive = _viewModel.SelectedPrimitive;
                    e.Pointer.Capture(this);
                    e.Handled = true;
                    RequestRender();
                    return;
                }
            }

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
        var currentPosition = e.GetPosition(this);
        
        // Обработка перетаскивания примитива за ручку
        if (_isDragging && _draggedPrimitive != null && _viewModel != null && e.Pointer.Captured == this)
        {
            var inverseScale = 1.0 / _viewModel.Scale;
            var worldX = (currentPosition.X - _viewModel.Offset.X) * inverseScale;
            var worldY = (_viewModel.Offset.Y - currentPosition.Y) * inverseScale;
            var currentWorldPoint = new Point(worldX, worldY);
            
            // Вычисляем смещение от начальной позиции
            var deltaX = currentWorldPoint.X - _dragStartWorld.X;
            var deltaY = currentWorldPoint.Y - _dragStartWorld.Y;
            
            // Перемещаем примитив в зависимости от типа ручки
            if (_draggedPrimitive is LinePrimitive line && _draggedHandleType != HandleType.Center)
            {
                // Для линии перемещаем только соответствующий конец
                if (_draggedHandleType == HandleType.LineEnd1)
                {
                    line.X1 = _dragStartCenter.X + deltaX;
                    line.Y1 = _dragStartCenter.Y + deltaY;
                }
                else if (_draggedHandleType == HandleType.LineEnd2)
                {
                    line.X2 = _dragStartCenter.X + deltaX;
                    line.Y2 = _dragStartCenter.Y + deltaY;
                }
            }
            else if (_draggedPrimitive is CirclePrimitive circle && _draggedHandleType == HandleType.CircleRadius)
            {
                // Для круга изменяем радиус на основе расстояния от центра до текущей позиции мыши
                // Ручка находится слева на оси X, поэтому используем только X координату
                var newRadius = circle.CenterX - currentWorldPoint.X;
                // Ограничиваем минимальный радиус
                if (newRadius > 0)
                {
                    circle.Radius = newRadius;
                }
            }
            else if (_draggedPrimitive is RectanglePrimitive rect)
            {
                var center = new Point(rect.CenterX, rect.CenterY);
                
                if (_draggedHandleType == HandleType.RectWidth)
                {
                    // Изменяем ширину: вычисляем расстояние от центра до текущей позиции мыши
                    // вдоль оси ширины (справа в неповернутом состоянии)
                    var angleRad = _dragStartRotationAngle * Math.PI / 180.0;
                    var cos = Math.Cos(angleRad);
                    var sin = Math.Sin(angleRad);
                    
                    // Вектор от центра до текущей позиции мыши
                    var dx = currentWorldPoint.X - center.X;
                    var dy = currentWorldPoint.Y - center.Y;
                    
                    // Проецируем на ось ширины (направление (cos, sin) в неповернутом состоянии)
                    var projection = dx * cos + dy * sin;
                    var newWidth = Math.Abs(projection) * 2.0;
                    
                    // Ограничиваем минимальную ширину
                    if (newWidth > 0.1)
                    {
                        rect.Width = newWidth;
                    }
                }
                else if (_draggedHandleType == HandleType.RectHeight)
                {
                    // Изменяем высоту: вычисляем расстояние от центра до текущей позиции мыши
                    // вдоль оси высоты (сверху в неповернутом состоянии)
                    var angleRad = _dragStartRotationAngle * Math.PI / 180.0;
                    var cos = Math.Cos(angleRad);
                    var sin = Math.Sin(angleRad);
                    
                    // Вектор от центра до текущей позиции мыши
                    var dx = currentWorldPoint.X - center.X;
                    var dy = currentWorldPoint.Y - center.Y;
                    
                    // Проецируем на ось высоты (направление (-sin, cos) в неповернутом состоянии)
                    var projection = -dx * sin + dy * cos;
                    var newHeight = Math.Abs(projection) * 2.0;
                    
                    // Ограничиваем минимальную высоту
                    if (newHeight > 0.1)
                    {
                        rect.Height = newHeight;
                    }
                }
                else if (_draggedHandleType == HandleType.RectRotation)
                {
                    // Изменяем угол поворота: вычисляем угол между центром и текущей позицией мыши
                    var dx = currentWorldPoint.X - center.X;
                    var dy = currentWorldPoint.Y - center.Y;
                    var currentAngleRad = Math.Atan2(dy, dx);
                    
                    // Вычисляем начальный угол ручки относительно центра
                    var startDx = _dragStartCenter.X - center.X;
                    var startDy = _dragStartCenter.Y - center.Y;
                    var startAngleRad = Math.Atan2(startDy, startDx);
                    
                    // Вычисляем изменение угла
                    var deltaAngleRad = currentAngleRad - startAngleRad;
                    var deltaAngleDeg = deltaAngleRad * 180.0 / Math.PI;
                    
                    // Применяем изменение к начальному углу поворота
                    rect.RotationAngle = _dragStartRotationAngle + deltaAngleDeg;
                }
                else
                {
                    // Перемещаем весь прямоугольник
                    MovePrimitive(_draggedPrimitive, deltaX, deltaY);
                }
            }
            else if (_draggedPrimitive is EllipsePrimitive ellipse)
            {
                var center = new Point(ellipse.CenterX, ellipse.CenterY);
                
                if (_draggedHandleType == HandleType.EllipseRadius1)
                {
                    // Изменяем первый радиус: вычисляем расстояние от центра до текущей позиции мыши
                    // вдоль первой оси (справа на оси X в неповернутом состоянии)
                    var angleRad = _dragStartEllipseRotationAngle * Math.PI / 180.0;
                    var cos = Math.Cos(angleRad);
                    var sin = Math.Sin(angleRad);
                    
                    // Вектор от центра до текущей позиции мыши
                    var dx = currentWorldPoint.X - center.X;
                    var dy = currentWorldPoint.Y - center.Y;
                    
                    // Проецируем на первую ось (направление (cos, sin) в неповернутом состоянии)
                    var projection = dx * cos + dy * sin;
                    var newRadius1 = Math.Abs(projection);
                    
                    // Ограничиваем минимальный радиус
                    if (newRadius1 > 0.1)
                    {
                        ellipse.Radius1 = newRadius1;
                    }
                }
                else if (_draggedHandleType == HandleType.EllipseRadius2)
                {
                    // Изменяем второй радиус: вычисляем расстояние от центра до текущей позиции мыши
                    // вдоль второй оси (сверху на оси Y в неповернутом состоянии)
                    var angleRad = _dragStartEllipseRotationAngle * Math.PI / 180.0;
                    var cos = Math.Cos(angleRad);
                    var sin = Math.Sin(angleRad);
                    
                    // Вектор от центра до текущей позиции мыши
                    var dx = currentWorldPoint.X - center.X;
                    var dy = currentWorldPoint.Y - center.Y;
                    
                    // Проецируем на вторую ось (направление (-sin, cos) в неповернутом состоянии)
                    var projection = -dx * sin + dy * cos;
                    var newRadius2 = Math.Abs(projection);
                    
                    // Ограничиваем минимальный радиус
                    if (newRadius2 > 0.1)
                    {
                        ellipse.Radius2 = newRadius2;
                    }
                }
                else if (_draggedHandleType == HandleType.EllipseRotation)
                {
                    // Изменяем угол поворота: вычисляем угол между центром и текущей позицией мыши
                    var dx = currentWorldPoint.X - center.X;
                    var dy = currentWorldPoint.Y - center.Y;
                    var currentAngleRad = Math.Atan2(dy, dx);
                    
                    // Вычисляем начальный угол ручки относительно центра
                    var startDx = _dragStartCenter.X - center.X;
                    var startDy = _dragStartCenter.Y - center.Y;
                    var startAngleRad = Math.Atan2(startDy, startDx);
                    
                    // Вычисляем изменение угла
                    var deltaAngleRad = currentAngleRad - startAngleRad;
                    var deltaAngleDeg = deltaAngleRad * 180.0 / Math.PI;
                    
                    // Применяем изменение к начальному углу поворота
                    ellipse.RotationAngle = _dragStartEllipseRotationAngle + deltaAngleDeg;
                }
                else
                {
                    // Перемещаем весь эллипс
                    MovePrimitive(_draggedPrimitive, deltaX, deltaY);
                }
            }
            else if (_draggedPrimitive is DxfPrimitive dxf && _draggedHandleType == HandleType.DxfRotation)
            {
                // Изменяем угол поворота DXF-объекта
                var center = GetPrimitiveCenter(dxf);
                var dx = currentWorldPoint.X - center.X;
                var dy = currentWorldPoint.Y - center.Y;
                var currentAngleRad = Math.Atan2(dy, dx);
                
                // Вычисляем начальный угол ручки относительно центра
                var startDx = _dragStartCenter.X - center.X;
                var startDy = _dragStartCenter.Y - center.Y;
                var startAngleRad = Math.Atan2(startDy, startDx);
                
                // Вычисляем изменение угла
                var deltaAngleRad = currentAngleRad - startAngleRad;
                var deltaAngleDeg = deltaAngleRad * 180.0 / Math.PI;
                
                // Применяем изменение к начальному углу поворота
                dxf.RotationAngle = _dragStartDxfRotationAngle + deltaAngleDeg;
            }
            else if (_draggedPrimitive is CompositePrimitive composite && _draggedHandleType == HandleType.CompositeRotation)
            {
                // Изменяем угол поворота составного объекта
                var center = GetPrimitiveCenter(composite);
                var dx = currentWorldPoint.X - center.X;
                var dy = currentWorldPoint.Y - center.Y;
                var currentAngleRad = Math.Atan2(dy, dx);
                
                // Вычисляем начальный угол ручки относительно центра
                var startDx = _dragStartCenter.X - center.X;
                var startDy = _dragStartCenter.Y - center.Y;
                var startAngleRad = Math.Atan2(startDy, startDx);
                
                // Вычисляем изменение угла
                var deltaAngleRad = currentAngleRad - startAngleRad;
                var deltaAngleDeg = deltaAngleRad * 180.0 / Math.PI;
                
                // Применяем изменение к начальному углу поворота
                composite.RotationAngle = _dragStartCompositeRotationAngle + deltaAngleDeg;
            }
            else if (_draggedPrimitive is PolygonPrimitive polygon)
            {
                var center = new Point(polygon.CenterX, polygon.CenterY);
                
                if (_draggedHandleType == HandleType.PolygonRadius)
                {
                    // Изменяем радиус: вычисляем расстояние от центра до текущей позиции мыши
                    // вдоль направления правой вершины (угол = 0 в неповернутом состоянии)
                    var angleRad = _dragStartPolygonRotationAngle * Math.PI / 180.0;
                    var cos = Math.Cos(angleRad);
                    var sin = Math.Sin(angleRad);
                    
                    // Вектор от центра до текущей позиции мыши
                    var dx = currentWorldPoint.X - center.X;
                    var dy = currentWorldPoint.Y - center.Y;
                    
                    // Проецируем на направление правой вершины (направление (cos, sin) в неповернутом состоянии)
                    var projection = dx * cos + dy * sin;
                    var newRadius = Math.Abs(projection);
                    
                    // Ограничиваем минимальный радиус
                    if (newRadius > 0.1)
                    {
                        polygon.CircumscribedRadius = newRadius;
                    }
                }
                else if (_draggedHandleType == HandleType.PolygonRotation)
                {
                    // Изменяем угол поворота: вычисляем угол между центром и текущей позицией мыши
                    var dx = currentWorldPoint.X - center.X;
                    var dy = currentWorldPoint.Y - center.Y;
                    var currentAngleRad = Math.Atan2(dy, dx);
                    
                    // Вычисляем начальный угол ручки относительно центра
                    var startDx = _dragStartCenter.X - center.X;
                    var startDy = _dragStartCenter.Y - center.Y;
                    var startAngleRad = Math.Atan2(startDy, startDx);
                    
                    // Вычисляем изменение угла
                    var deltaAngleRad = currentAngleRad - startAngleRad;
                    var deltaAngleDeg = deltaAngleRad * 180.0 / Math.PI;
                    
                    // Применяем изменение к начальному углу поворота
                    polygon.RotationAngle = _dragStartPolygonRotationAngle + deltaAngleDeg;
                }
                else
                {
                    // Перемещаем весь многоугольник
                    MovePrimitive(_draggedPrimitive, deltaX, deltaY);
                }
            }
            else if (_draggedPrimitive is ArcPrimitive arc)
            {
                var center = new Point(arc.CenterX, arc.CenterY);
                
                if (_draggedHandleType == HandleType.ArcStartAngle)
                {
                    // Изменяем начальный угол: вычисляем угол между центром и текущей позицией мыши
                    var dx = currentWorldPoint.X - center.X;
                    var dy = currentWorldPoint.Y - center.Y;
                    var currentAngleRad = Math.Atan2(dy, dx);
                    var currentAngleDeg = currentAngleRad * 180.0 / Math.PI;
                    
                    // Нормализуем угол в диапазон [0, 360)
                    while (currentAngleDeg < 0) currentAngleDeg += 360;
                    while (currentAngleDeg >= 360) currentAngleDeg -= 360;
                    
                    arc.StartAngle = currentAngleDeg;
                    
                    // Убеждаемся, что дуга строится против часовой стрелки
                    // Если EndAngle < StartAngle, добавляем 360 к EndAngle
                    var normalizedEnd = arc.EndAngle;
                    while (normalizedEnd < 0) normalizedEnd += 360;
                    while (normalizedEnd >= 360) normalizedEnd -= 360;
                    
                    if (normalizedEnd < currentAngleDeg)
                    {
                        normalizedEnd += 360;
                    }
                    arc.EndAngle = normalizedEnd;
                }
                else if (_draggedHandleType == HandleType.ArcEndAngle)
                {
                    // Изменяем конечный угол: вычисляем угол между центром и текущей позицией мыши
                    var dx = currentWorldPoint.X - center.X;
                    var dy = currentWorldPoint.Y - center.Y;
                    var currentAngleRad = Math.Atan2(dy, dx);
                    var currentAngleDeg = currentAngleRad * 180.0 / Math.PI;
                    
                    // Нормализуем угол в диапазон [0, 360)
                    while (currentAngleDeg < 0) currentAngleDeg += 360;
                    while (currentAngleDeg >= 360) currentAngleDeg -= 360;
                    
                    // Убеждаемся, что дуга строится против часовой стрелки
                    // Если новый EndAngle < StartAngle, добавляем 360 к EndAngle
                    var normalizedStart = arc.StartAngle;
                    while (normalizedStart < 0) normalizedStart += 360;
                    while (normalizedStart >= 360) normalizedStart -= 360;
                    
                    var normalizedEnd = currentAngleDeg;
                    if (normalizedEnd < normalizedStart)
                    {
                        normalizedEnd += 360;
                    }
                    
                    arc.EndAngle = normalizedEnd;
                }
                else if (_draggedHandleType == HandleType.ArcRadius)
                {
                    // Изменяем радиус: вычисляем расстояние от центра до текущей позиции мыши
                    var dx = currentWorldPoint.X - center.X;
                    var dy = currentWorldPoint.Y - center.Y;
                    var distance = Math.Sqrt(dx * dx + dy * dy);
                    
                    // Ограничиваем минимальный радиус
                    if (distance > 0.1)
                    {
                        arc.Radius = distance;
                    }
                }
                else
                {
                    // Перемещаем всю дугу
                    MovePrimitive(_draggedPrimitive, deltaX, deltaY);
                }
            }
            else
            {
                // Для остальных примитивов перемещаем весь примитив
                MovePrimitive(_draggedPrimitive, deltaX, deltaY);
            }
            
            // Уведомляем об изменении свойств примитива для обновления панели свойств
            _viewModel.NotifyPrimitivePropertyChanged();
            
            // Обновляем начальную позицию для следующего движения
            _dragStartWorld = currentWorldPoint;
            
            // Обновляем начальную позицию ручки
            if (_draggedPrimitive is LinePrimitive linePrimitive)
            {
                if (_draggedHandleType == HandleType.LineEnd1)
                    _dragStartCenter = new Point(linePrimitive.X1, linePrimitive.Y1);
                else if (_draggedHandleType == HandleType.LineEnd2)
                    _dragStartCenter = new Point(linePrimitive.X2, linePrimitive.Y2);
                else
                    _dragStartCenter = GetPrimitiveCenter(_draggedPrimitive);
            }
            else if (_draggedPrimitive is CirclePrimitive circlePrimitive)
            {
                if (_draggedHandleType == HandleType.CircleRadius)
                {
                    // Обновляем позицию ручки радиуса
                    _dragStartCenter = new Point(circlePrimitive.CenterX - circlePrimitive.Radius, circlePrimitive.CenterY);
                }
                else
                {
                    _dragStartCenter = GetPrimitiveCenter(_draggedPrimitive);
                }
            }
            else if (_draggedPrimitive is RectanglePrimitive rectPrimitive)
            {
                var (center, widthHandle, heightHandle, rotationHandle) = GetRectangleHandles(rectPrimitive);
                if (_draggedHandleType == HandleType.RectWidth)
                    _dragStartCenter = widthHandle;
                else if (_draggedHandleType == HandleType.RectHeight)
                    _dragStartCenter = heightHandle;
                else if (_draggedHandleType == HandleType.RectRotation)
                    _dragStartCenter = rotationHandle;
                else
                    _dragStartCenter = center;
            }
            else if (_draggedPrimitive is EllipsePrimitive ellipsePrimitive)
            {
                var (center, radius1Handle, radius2Handle, rotationHandle) = GetEllipseHandles(ellipsePrimitive);
                if (_draggedHandleType == HandleType.EllipseRadius1)
                    _dragStartCenter = radius1Handle;
                else if (_draggedHandleType == HandleType.EllipseRadius2)
                    _dragStartCenter = radius2Handle;
                else if (_draggedHandleType == HandleType.EllipseRotation)
                    _dragStartCenter = rotationHandle;
                else
                    _dragStartCenter = center;
            }
            else if (_draggedPrimitive is DxfPrimitive dxfPrimitive)
            {
                var center = GetPrimitiveCenter(_draggedPrimitive);
                if (_draggedHandleType == HandleType.DxfRotation)
                    _dragStartCenter = GetCompositeRotationHandle(dxfPrimitive);
                else
                    _dragStartCenter = center;
            }
            else if (_draggedPrimitive is CompositePrimitive compositePrimitive)
            {
                var center = GetPrimitiveCenter(_draggedPrimitive);
                if (_draggedHandleType == HandleType.CompositeRotation)
                    _dragStartCenter = GetCompositeRotationHandle(compositePrimitive);
                else
                    _dragStartCenter = center;
            }
            else if (_draggedPrimitive is PolygonPrimitive polygonPrimitive)
            {
                var (center, radiusHandle, rotationHandle) = GetPolygonHandles(polygonPrimitive);
                if (_draggedHandleType == HandleType.PolygonRadius)
                    _dragStartCenter = radiusHandle;
                else if (_draggedHandleType == HandleType.PolygonRotation)
                    _dragStartCenter = rotationHandle;
                else
                    _dragStartCenter = center;
            }
            else if (_draggedPrimitive is ArcPrimitive arcPrimitive)
            {
                var (center, startAngleHandle, endAngleHandle, radiusHandle) = GetArcHandles(arcPrimitive);
                if (_draggedHandleType == HandleType.ArcStartAngle)
                    _dragStartCenter = startAngleHandle;
                else if (_draggedHandleType == HandleType.ArcEndAngle)
                    _dragStartCenter = endAngleHandle;
                else if (_draggedHandleType == HandleType.ArcRadius)
                    _dragStartCenter = radiusHandle;
                else
                    _dragStartCenter = center;
            }
            else
            {
                _dragStartCenter = GetPrimitiveCenter(_draggedPrimitive);
            }
            
            e.Handled = true;
            RequestRender();
            return;
        }
        
        // Обработка pinch-to-zoom: обновляем позицию указателя
        if (_activePointers.Count == 2 && _viewModel != null && _activePointers.ContainsKey(e.Pointer))
        {
            // Обновляем позицию текущего указателя
            _activePointers[e.Pointer] = currentPosition;
            
            // Вычисляем новое расстояние между двумя указателями
            var pointers = new List<Point>(_activePointers.Values);
            var currentDistance = Distance(pointers[0], pointers[1]);
            
            if (_initialDistance > 0 && Math.Abs(currentDistance - _initialDistance) > 1.0)
            {
                // Уменьшаем чувствительность pinch-to-zoom в 3 раза
                // Если расстояние изменилось в 2 раза, масштаб изменится в 1.33 раза вместо 2
                var rawScaleFactor = currentDistance / _initialDistance;
                var scaleFactor = 1.0 + (rawScaleFactor - 1.0) / 3.0;
                var newScale = _initialScale * scaleFactor;
                
                // Ограничиваем масштаб
                if (newScale < 0.1) newScale = 0.1;
                if (newScale > 250.0) newScale = 250.0;
                
                // Вычисляем новый центр
                var currentCenter = new Point(
                    (pointers[0].X + pointers[1].X) / 2,
                    (pointers[0].Y + pointers[1].Y) / 2
                );
                
                // Масштабируем относительно центра жеста
                var worldX = (currentCenter.X - _initialOffset.X) / _initialScale;
                var worldY = (_initialOffset.Y - currentCenter.Y) / _initialScale;
                
                _viewModel.Scale = newScale;
                _viewModel.Offset = new Point(
                    currentCenter.X - worldX * newScale,
                    currentCenter.Y + worldY * newScale
                );
                
                RequestRender();
            }
            
            e.Handled = true;
            return;
        }
        
        // Обновляем позицию указателя, если он отслеживается
        if (_activePointers.ContainsKey(e.Pointer))
        {
            _activePointers[e.Pointer] = currentPosition;
        }
        
        // Не обрабатываем панорамирование, если есть два указателя (pinch-to-zoom)
        if (_isPanning && _activePointers.Count < 2 && _viewModel != null && e.Pointer.Captured == this)
        {
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
            
            // Проверяем, находится ли мышь над ручкой выделенного примитива
            HandleType? newHoveredHandle = null;
            if (_viewModel.SelectedPrimitive != null && !_isDragging)
            {
                newHoveredHandle = GetHandleAtPoint(worldPoint, _viewModel.SelectedPrimitive, _viewModel.Scale);
            }
            
            // Обновляем наведенную ручку и перерисовываем, если изменилось
            if (_hoveredHandleType != newHoveredHandle)
            {
                _hoveredHandleType = newHoveredHandle;
                RequestRender();
            }
        }
        
        base.OnPointerMoved(e);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        
        // Обработка завершения перетаскивания
        if (_isDragging)
        {
            _isDragging = false;
            _draggedPrimitive = null;
            if (e.Pointer.Captured == this)
            {
                e.Pointer.Capture(null);
            }
            e.Handled = true;
            RequestRender();
            return;
        }
        
        // Обработка завершения pinch-to-zoom: удаляем указатель из отслеживания
        if (_activePointers.ContainsKey(e.Pointer))
        {
            _activePointers.Remove(e.Pointer);
            if (e.Pointer.Captured == this)
            {
                e.Pointer.Capture(null);
            }
            
            // Если остался только один указатель или ни одного - сбрасываем состояние
            if (_activePointers.Count < 2)
            {
                _initialDistance = 0;
            }
            
            // Если это был последний указатель, освобождаем все
            if (_activePointers.Count == 0)
            {
                _activePointers.Clear();
            }
            
            e.Handled = true;
            return;
        }
        
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
        
        // Сбрасываем состояние перетаскивания при потере захвата
        if (_isDragging)
        {
            _isDragging = false;
            _draggedPrimitive = null;
        }
        
        // Сбрасываем состояние панорамирования и pinch-to-zoom при потере захвата
        if (_isPanning)
        {
            _isPanning = false;
        }
        
        // Удаляем все указатели при потере захвата
        _activePointers.Clear();
        _initialDistance = 0;
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
        
        // Сбрасываем наведенную ручку
        if (_hoveredHandleType != null)
        {
            _hoveredHandleType = null;
            RequestRender();
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
            
            // Рисуем ручку в центре выделенного примитива (квадрат 5x5 пикселей)
            DrawHandle(context, selected, viewModel.Scale);
        }
    }

    /// <summary>
    /// Рисует ручку в указанной точке (квадрат 5x5 пикселей независимо от масштаба).
    /// </summary>
    private static void DrawHandleAtPoint(
        DrawingContext context,
        Point worldPoint,
        double scale,
        bool isHovered = false,
        bool isActive = false)
    {
        // Размер ручки в пикселях экрана (постоянный 5x5)
        const double handleSizePixels = 5.0;
        var halfSizePixels = handleSizePixels / 2.0;
        
        // Преобразуем размер в мировые координаты для отрисовки
        var halfSizeWorld = halfSizePixels / scale;
        
        // Создаем квадрат для ручки
        var handleRect = new Rect(
            worldPoint.X - halfSizeWorld,
            worldPoint.Y - halfSizeWorld,
            handleSizePixels / scale,
            handleSizePixels / scale
        );
        
        // Если ручка под мышью или активно перетаскивается, рисуем цветной контур
        // (квадрат 7x7 пикселей, прилегающий к внешнему черному контуру)
        if (isHovered || isActive)
        {
            const double hoverOutlineSizePixels = 7.0; // 5 + 2 пикселя с каждой стороны
            var halfHoverSizePixels = hoverOutlineSizePixels / 2.0;
            var halfHoverSizeWorld = halfHoverSizePixels / scale;
            
            var hoverRect = new Rect(
                worldPoint.X - halfHoverSizeWorld,
                worldPoint.Y - halfHoverSizeWorld,
                hoverOutlineSizePixels / scale,
                hoverOutlineSizePixels / scale
            );
            
            // Рисуем контур (2 пикселя толщиной):
            // - зеленый при наведении
            // - красный при зажатой/перетаскиваемой ручке
            var outlineColor = isActive ? Colors.Red : Colors.Green;
            var hoverPen = new Pen(new SolidColorBrush(outlineColor), 2.0 / scale);
            context.DrawRectangle(null, hoverPen, hoverRect);
        }
        
        // Рисуем заливку и контур ручки
        var handleBrush = new SolidColorBrush(Colors.White);
        var handlePen = new Pen(new SolidColorBrush(Colors.Black), 1.0 / scale);
        context.DrawRectangle(handleBrush, handlePen, handleRect);
    }

    /// <summary>
    /// Рисует ручки для примитива (квадрат 5x5 пикселей независимо от масштаба).
    /// Для линии рисует ручки на концах и в центре, для остальных - только в центре.
    /// </summary>
    private void DrawHandle(DrawingContext context, PrimitiveItem primitive, double scale)
    {
        if (primitive is LinePrimitive line)
        {
            // Для линии рисуем ручки на концах и в центре
            var isActive = _isDragging && ReferenceEquals(_draggedPrimitive, primitive);
            DrawHandleAtPoint(
                context,
                new Point(line.X1, line.Y1),
                scale,
                _hoveredHandleType == HandleType.LineEnd1,
                isActive && _draggedHandleType == HandleType.LineEnd1);
            DrawHandleAtPoint(
                context,
                new Point(line.X2, line.Y2),
                scale,
                _hoveredHandleType == HandleType.LineEnd2,
                isActive && _draggedHandleType == HandleType.LineEnd2);
            DrawHandleAtPoint(
                context,
                GetPrimitiveCenter(primitive),
                scale,
                _hoveredHandleType == HandleType.Center,
                isActive && _draggedHandleType == HandleType.Center);
        }
        else if (primitive is CirclePrimitive circle)
        {
            // Для круга рисуем ручку радиуса (слева на оси X) и центральную ручку
            var isActive = _isDragging && ReferenceEquals(_draggedPrimitive, primitive);
            DrawHandleAtPoint(
                context,
                new Point(circle.CenterX - circle.Radius, circle.CenterY),
                scale,
                _hoveredHandleType == HandleType.CircleRadius,
                isActive && _draggedHandleType == HandleType.CircleRadius);
            DrawHandleAtPoint(
                context,
                GetPrimitiveCenter(primitive),
                scale,
                _hoveredHandleType == HandleType.Center,
                isActive && _draggedHandleType == HandleType.Center);
        }
        else if (primitive is RectanglePrimitive rect)
        {
            // Для прямоугольника рисуем ручки ширины, высоты, поворота и центральную
            var (center, widthHandle, heightHandle, rotationHandle) = GetRectangleHandles(rect);
            var isActive = _isDragging && ReferenceEquals(_draggedPrimitive, primitive);
            DrawHandleAtPoint(
                context,
                widthHandle,
                scale,
                _hoveredHandleType == HandleType.RectWidth,
                isActive && _draggedHandleType == HandleType.RectWidth);
            DrawHandleAtPoint(
                context,
                heightHandle,
                scale,
                _hoveredHandleType == HandleType.RectHeight,
                isActive && _draggedHandleType == HandleType.RectHeight);
            DrawHandleAtPoint(
                context,
                rotationHandle,
                scale,
                _hoveredHandleType == HandleType.RectRotation,
                isActive && _draggedHandleType == HandleType.RectRotation);
            DrawHandleAtPoint(
                context,
                center,
                scale,
                _hoveredHandleType == HandleType.Center,
                isActive && _draggedHandleType == HandleType.Center);
        }
        else if (primitive is EllipsePrimitive ellipse)
        {
            // Для эллипса рисуем ручки первого радиуса, второго радиуса, поворота и центральную
            var (center, radius1Handle, radius2Handle, rotationHandle) = GetEllipseHandles(ellipse);
            var isActive = _isDragging && ReferenceEquals(_draggedPrimitive, primitive);
            DrawHandleAtPoint(
                context,
                radius1Handle,
                scale,
                _hoveredHandleType == HandleType.EllipseRadius1,
                isActive && _draggedHandleType == HandleType.EllipseRadius1);
            DrawHandleAtPoint(
                context,
                radius2Handle,
                scale,
                _hoveredHandleType == HandleType.EllipseRadius2,
                isActive && _draggedHandleType == HandleType.EllipseRadius2);
            DrawHandleAtPoint(
                context,
                rotationHandle,
                scale,
                _hoveredHandleType == HandleType.EllipseRotation,
                isActive && _draggedHandleType == HandleType.EllipseRotation);
            DrawHandleAtPoint(
                context,
                center,
                scale,
                _hoveredHandleType == HandleType.Center,
                isActive && _draggedHandleType == HandleType.Center);
        }
        else if (primitive is PolygonPrimitive polygon)
        {
            // Для многоугольника рисуем ручку радиуса, ручку поворота и центральную
            var (center, radiusHandle, rotationHandle) = GetPolygonHandles(polygon);
            var isActive = _isDragging && ReferenceEquals(_draggedPrimitive, primitive);
            DrawHandleAtPoint(
                context,
                radiusHandle,
                scale,
                _hoveredHandleType == HandleType.PolygonRadius,
                isActive && _draggedHandleType == HandleType.PolygonRadius);
            DrawHandleAtPoint(
                context,
                rotationHandle,
                scale,
                _hoveredHandleType == HandleType.PolygonRotation,
                isActive && _draggedHandleType == HandleType.PolygonRotation);
            DrawHandleAtPoint(
                context,
                center,
                scale,
                _hoveredHandleType == HandleType.Center,
                isActive && _draggedHandleType == HandleType.Center);
        }
        else if (primitive is ArcPrimitive arc)
        {
            // Для дуги рисуем ручки начального угла, конечного угла, радиуса и центральную
            var (center, startAngleHandle, endAngleHandle, radiusHandle) = GetArcHandles(arc);
            var isActive = _isDragging && ReferenceEquals(_draggedPrimitive, primitive);
            DrawHandleAtPoint(
                context,
                startAngleHandle,
                scale,
                _hoveredHandleType == HandleType.ArcStartAngle,
                isActive && _draggedHandleType == HandleType.ArcStartAngle);
            DrawHandleAtPoint(
                context,
                endAngleHandle,
                scale,
                _hoveredHandleType == HandleType.ArcEndAngle,
                isActive && _draggedHandleType == HandleType.ArcEndAngle);
            DrawHandleAtPoint(
                context,
                radiusHandle,
                scale,
                _hoveredHandleType == HandleType.ArcRadius,
                isActive && _draggedHandleType == HandleType.ArcRadius);
            DrawHandleAtPoint(
                context,
                center,
                scale,
                _hoveredHandleType == HandleType.Center,
                isActive && _draggedHandleType == HandleType.Center);
        }
        else if (primitive is DxfPrimitive dxf)
        {
            // Для DXF-объекта рисуем ручку поворота и центральную ручку
            var center = GetPrimitiveCenter(primitive);
            var rotationHandle = GetCompositeRotationHandle(dxf);
            var isActive = _isDragging && ReferenceEquals(_draggedPrimitive, primitive);
            DrawHandleAtPoint(
                context,
                rotationHandle,
                scale,
                _hoveredHandleType == HandleType.DxfRotation,
                isActive && _draggedHandleType == HandleType.DxfRotation);
            DrawHandleAtPoint(
                context,
                center,
                scale,
                _hoveredHandleType == HandleType.Center,
                isActive && _draggedHandleType == HandleType.Center);
        }
        else if (primitive is CompositePrimitive composite)
        {
            // Для составного объекта рисуем ручку поворота и центральную ручку
            var center = GetPrimitiveCenter(primitive);
            var rotationHandle = GetCompositeRotationHandle(composite);
            var isActive = _isDragging && ReferenceEquals(_draggedPrimitive, primitive);
            DrawHandleAtPoint(
                context,
                rotationHandle,
                scale,
                _hoveredHandleType == HandleType.CompositeRotation,
                isActive && _draggedHandleType == HandleType.CompositeRotation);
            DrawHandleAtPoint(
                context,
                center,
                scale,
                _hoveredHandleType == HandleType.Center,
                isActive && _draggedHandleType == HandleType.Center);
        }
        else
        {
            // Для остальных примитивов рисуем только центральную ручку
            var center = GetPrimitiveCenter(primitive);
            var isActive = _isDragging && ReferenceEquals(_draggedPrimitive, primitive);
            DrawHandleAtPoint(
                context,
                center,
                scale,
                _hoveredHandleType == HandleType.Center,
                isActive && _draggedHandleType == HandleType.Center);
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
                // Нормализуем угол в диапазон [0, 360)
                while (angle < 0) angle += 360;
                while (angle >= 360) angle -= 360;
                
                // Нормализуем углы дуги для проверки против часовой стрелки
                var (normalizedStart, normalizedEnd) = NormalizeArcAngles(arc.StartAngle, arc.EndAngle);
                
                // Проверяем, находится ли угол в диапазоне дуги
                if (normalizedEnd <= 360)
                {
                    // Обычный случай: дуга не пересекает 0 градусов
                    return angle >= normalizedStart - 0.5 && angle <= normalizedEnd + 0.5;
                }
                else
                {
                    // Дуга пересекает 0 градусов (EndAngle был нормализован добавлением 360)
                    // Проверяем два диапазона: [start, 360) и [0, end-360]
                    return (angle >= normalizedStart - 0.5 && angle <= 360) ||
                           (angle >= 0 && angle <= (normalizedEnd - 360) + 0.5);
                }
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
        var angleRad = polygon.RotationAngle * Math.PI / 180.0;
        var cosRot = Math.Cos(angleRad);
        var sinRot = Math.Sin(angleRad);

        for (int i = 0; i < polygon.SidesCount; i++)
        {
            var angle = 2 * Math.PI * i / polygon.SidesCount;
            var x = polygon.CircumscribedRadius * Math.Cos(angle);
            var y = polygon.CircumscribedRadius * Math.Sin(angle);

            // Применяем поворот
            var rx = x * cosRot - y * sinRot + polygon.CenterX;
            var ry = x * sinRot + y * cosRot + polygon.CenterY;
            pts[i] = new Point(rx, ry);
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
        
        // Нормализуем углы так, чтобы дуга всегда строилась против часовой стрелки
        var (normalizedStart, normalizedEnd) = NormalizeArcAngles(arc.StartAngle, arc.EndAngle);
        
        var startRad = normalizedStart * Math.PI / 180.0;
        var endRad = normalizedEnd * Math.PI / 180.0;
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
        var angleRad = polygon.RotationAngle * Math.PI / 180.0;
        var cosRot = Math.Cos(angleRad);
        var sinRot = Math.Sin(angleRad);

        for (int i = 0; i < polygon.SidesCount; i++)
        {
            var angle = 2 * Math.PI * i / polygon.SidesCount;
            var x = polygon.CircumscribedRadius * Math.Cos(angle);
            var y = polygon.CircumscribedRadius * Math.Sin(angle);

            // Применяем поворот
            var rx = x * cosRot - y * sinRot + polygon.CenterX;
            var ry = x * sinRot + y * cosRot + polygon.CenterY;
            pts[i] = new Point(rx, ry);
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
                            polygon.SidesCount,
                            polygon.RotationAngle + rotationAngle));
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

