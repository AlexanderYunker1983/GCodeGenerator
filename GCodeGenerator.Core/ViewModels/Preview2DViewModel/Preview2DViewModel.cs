using System;
using System.Collections.ObjectModel;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using GCodeGenerator.Core.Interfaces;
using GCodeGenerator.Core.Models;

namespace GCodeGenerator.Core.ViewModels.Preview2DViewModel;

public partial class Preview2DViewModel : ViewModelBase, IHasDisplayName
{
    private double _scale = 10.0;
    private Point _offset;
    private Point? _mouseWorldCoordinates;

    /// <summary>
    /// Коллекция геометрических примитивов для отрисовки.
    /// Должна разделяться со списком примитивов в правой панели.
    /// </summary>
    public ObservableCollection<PrimitiveItem> Primitives { get; } = new();

    // Публичные свойства без автоматических уведомлений для производительности
    public double Scale
    {
        get => _scale;
        set => _scale = value;
    }

    public Point Offset
    {
        get => _offset;
        set => _offset = value;
    }

    [ObservableProperty]
    private Point? mouseWorldCoordinates;

    /// <summary>
    /// Примитив под курсором мыши (hover). Используется для подсветки и отображения имени.
    /// </summary>
    [ObservableProperty]
    private PrimitiveItem? hoveredPrimitive;

    /// <summary>
    /// Текущий выделенный примитив (для подсветки в 2D-предпросмотре).
    /// </summary>
    [ObservableProperty]
    private PrimitiveItem? selectedPrimitive;

    public event EventHandler? RedrawRequested;

    public Preview2DViewModel()
    {
        InitializeResources();
        // Offset будет установлен при первой загрузке View
    }

    public void InitializeOffset(double width, double height)
    {
        // Устанавливаем offset так, чтобы (0,0) был в центре экрана
        // Формула: screen = world * scale + offset
        // Для (0,0) в центре: center = 0 * scale + offset => offset = center
        _offset = new Point(width / 2, height / 2);
    }

    public void Zoom(double delta, Point mousePosition)
    {
        // Уменьшаем скорость масштабирования в 3 раза (было 1.1/0.9, стало ~1.033/0.967)
        var zoomFactor = delta > 0 ? 1.033 : 0.967;
        var newScale = _scale * zoomFactor;
        
        // Ограничиваем масштаб
        if (newScale < 0.1) newScale = 0.1;
        if (newScale > 250.0) newScale = 250.0;
        
        // Масштабируем относительно позиции мыши
        // Преобразуем экранную координату мыши в логическую
        var worldX = (mousePosition.X - _offset.X) / _scale;
        var worldY = (mousePosition.Y - _offset.Y) / _scale;
        
        // Вычисляем новый offset так, чтобы логическая точка осталась под мышью
        _offset = new Point(
            mousePosition.X - worldX * newScale,
            mousePosition.Y - worldY * newScale
        );
        
        _scale = newScale;
    }

    public void Pan(Point delta)
    {
        _offset = new Point(_offset.X + delta.X, _offset.Y + delta.Y);
    }

    /// <summary>
    /// Явно запросить перерисовку холста.
    /// Вызывает событие RedrawRequested, на которое подписан Preview2DCanvas.
    /// </summary>
    public void RequestRedraw()
    {
        RedrawRequested?.Invoke(this, EventArgs.Empty);
    }
}

