#nullable enable
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using GCodeGenerator.Trajectory;
using GCodeGenerator.ViewModels;
using GCodeGenerator.Views.Scene;

namespace GCodeGenerator.Views
{
    public partial class PreviewView : Window
    {
        /// <summary>Палитра сцены: фон окна и цвета траектории берутся из одной темы.</summary>
        private readonly SceneMaterials _materials = SceneMaterials.ForCurrentTheme();

        private PreviewViewModel? _viewModel;
        private CoordinateGridModels? _coordinateGrids;

        /// <summary>
        /// Положение камеры и правила его изменения. Окно только передаёт
        /// сюда движения мыши и переносит результат в камеру разметки.
        /// </summary>
        private readonly OrbitCamera _camera = new OrbitCamera();

        private Point3D _rotationPivot = new Point3D(0, 0, 0); // Точка поворота при правой кнопке мыши
        private bool _isRotating;
        private bool _isPanning;
        private Point _lastMousePosition;

        public PreviewView()
        {
            InitializeComponent();
            MainGrid.Background = _materials.BackgroundBrush;
            DataContextChanged += PreviewView_DataContextChanged;
            Closed += PreviewView_Closed;
        }

        private void PreviewView_DataContextChanged(object? sender, DependencyPropertyChangedEventArgs e)
        {
            UnhookViewModel();

            _viewModel = e.NewValue as PreviewViewModel;
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged += OnViewModelPropertyChanged;

                if (_viewModel.Scene != null)
                {
                    RenderScene(_viewModel.Scene);
                }
            }
        }

        private void PreviewView_Closed(object? sender, EventArgs e)
        {
            UnhookViewModel();
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PreviewViewModel.Scene) &&
                sender is PreviewViewModel viewModel &&
                ReferenceEquals(viewModel, _viewModel))
            {
                RenderScene(viewModel.Scene);
            }
            else if ((e.PropertyName == nameof(PreviewViewModel.ShowXyGrid)
                      || e.PropertyName == nameof(PreviewViewModel.ShowXzGrid)
                      || e.PropertyName == nameof(PreviewViewModel.ShowYzGrid))
                     && ReferenceEquals(sender, _viewModel))
            {
                UpdateCoordinateGrids();
            }
        }

        private void RenderScene(TrajectoryScene? scene)
        {
            // Сцены может не быть, пока траектория не построена: тогда
            // окно показывает пустую модель, а не отказывается рисовать.
            var actualScene = scene ?? TrajectoryScene.Empty;
            _coordinateGrids = CoordinateGridBuilder.Build(actualScene, _materials);
            UpdateCoordinateGrids();
            UpdateTrajectoryModel(SceneRenderer.Render(actualScene, _materials));
        }

        private void UpdateCoordinateGrids()
        {
            if (_coordinateGrids == null)
                return;

            GridXyVisual.Content = _viewModel?.ShowXyGrid == true ? _coordinateGrids.Xy.Model : null;
            GridXzVisual.Content = _viewModel?.ShowXzGrid == true ? _coordinateGrids.Xz.Model : null;
            GridYzVisual.Content = _viewModel?.ShowYzGrid == true ? _coordinateGrids.Yz.Model : null;
        }

        private void UnhookViewModel()
        {
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
                _viewModel = null;
            }
        }

        private void UpdateTrajectoryModel(Model3DGroup model)
        {
            if (TrajectoryVisual != null)
            {
                TrajectoryVisual.Content = model ?? new Model3DGroup();
                
                // Auto-fit camera to model
                if (model != null && model.Children.Count > 0)
                {
                    var bounds = model.Bounds;
                    if (_coordinateGrids != null)
                    {
                        // Камера с самого начала знает полный диапазон сеток,
                        // поэтому их включение не обрезает отрицательные
                        // координаты и не требует сбрасывать ракурс.
                        bounds.Union(_coordinateGrids.Xy.Model.Bounds);
                        bounds.Union(_coordinateGrids.Xz.Model.Bounds);
                        bounds.Union(_coordinateGrids.Yz.Model.Bounds);
                    }
                    if (!bounds.IsEmpty)
                    {
                        // Программа целиком в кадре: камера встаёт над её
                        // серединой на удалении в два наибольших размера.
                        var center = new Point3D(
                            bounds.X + bounds.SizeX / 2,
                            bounds.Y + bounds.SizeY / 2,
                            bounds.Z + bounds.SizeZ / 2);
                        var maxSize = Math.Max(Math.Max(bounds.SizeX, bounds.SizeY), bounds.SizeZ);

                        _camera.LookAt(center, maxSize * 2);
                        ApplyCamera();
                    }
                }
            }
        }

        private void Viewport_MouseWheel(object? sender, MouseWheelEventArgs e)
        {
            if (Camera == null || IsToolbarInteraction(e.OriginalSource)) return;

            _camera.Zoom(closer: e.Delta > 0);
            ApplyCamera();
        }

        private void Viewport_MouseDown(object? sender, MouseButtonEventArgs e)
        {
            if (IsToolbarInteraction(e.OriginalSource))
                return;

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                _isRotating = true;
                _lastMousePosition = e.GetPosition(this);
                this.CaptureMouse();
                e.Handled = true;
            }
            else if (e.RightButton == MouseButtonState.Pressed)
            {
                var mousePos = e.GetPosition(Viewport);
                _rotationPivot = GetPointUnderCursor(mousePos); // Сохраняем точку для поворота
                
                // Если зажат Shift, начинаем поворот вокруг точки под курсором
                // Иначе - панорамирование
                if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                {
                    _isRotating = true;
                }
                else
                {
                    _isPanning = true;
                }
                
                _lastMousePosition = mousePos;
                this.CaptureMouse();
                e.Handled = true;
            }
        }

        private void Viewport_MouseMove(object? sender, MouseEventArgs e)
        {
            if (Camera == null) return;

            var currentPosition = e.GetPosition(Viewport);
            var deltaX = currentPosition.X - _lastMousePosition.X;
            var deltaY = currentPosition.Y - _lastMousePosition.Y;

            var handled = false;
            if (_isPanning && e.RightButton == MouseButtonState.Pressed)
            {
                // Панорамирование при правой кнопке мыши
                _camera.Pan(deltaX, deltaY);
                ApplyCamera();
                handled = true;
            }
            else if (_isRotating && e.LeftButton == MouseButtonState.Pressed)
            {
                // Поворот вокруг центра модели при левой кнопке мыши
                if (Math.Abs(deltaX) > 0 || Math.Abs(deltaY) > 0)
                {
                    _camera.Orbit(deltaX, deltaY, _camera.Target);
                    ApplyCamera();
                }
                handled = true;
            }
            else if (_isRotating && e.RightButton == MouseButtonState.Pressed)
            {
                // Поворот вокруг точки под курсором при правой кнопке мыши (с Shift)
                if (Math.Abs(deltaX) > 0 || Math.Abs(deltaY) > 0)
                {
                    _camera.Orbit(deltaX, deltaY, _rotationPivot);
                    ApplyCamera();
                }
                handled = true;
            }

            _lastMousePosition = currentPosition;
            if (handled)
                e.Handled = true;
        }

        private void Viewport_MouseUp(object? sender, MouseButtonEventArgs e)
        {
            if (IsToolbarInteraction(e.OriginalSource))
                return;

            if (e.LeftButton == MouseButtonState.Released)
            {
                _isRotating = false;
                this.ReleaseMouseCapture();
                e.Handled = true;
            }
            else if (e.RightButton == MouseButtonState.Released)
            {
                _isPanning = false;
                _isRotating = false;
                this.ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        /// <summary>
        /// Кнопки лежат поверх области вращения. Предварительные события
        /// окна приходят раньше ToggleButton, поэтому без этой проверки
        /// окно захватывало мышь и переключатель не получал Click.
        /// </summary>
        private static bool IsToolbarInteraction(object? originalSource)
        {
            var current = originalSource as DependencyObject;
            while (current != null)
            {
                if (current is ButtonBase)
                    return true;
                current = VisualTreeHelper.GetParent(current);
            }
            return false;
        }

        /// <summary>Переносит состояние камеры в камеру разметки.</summary>
        private void ApplyCamera()
        {
            if (Camera == null) return;

            Camera.Position = _camera.Position;
            Camera.LookDirection = _camera.LookDirection;
            Camera.UpDirection = _camera.UpDirection;
        }

        private Point3D GetPointUnderCursor(Point mousePosition)
        {
            if (Camera == null || Viewport == null) return _camera.Target;

            // Получаем размеры viewport
            var viewportWidth = Viewport.ActualWidth;
            var viewportHeight = Viewport.ActualHeight;
            
            if (viewportWidth < 1 || viewportHeight < 1) return _camera.Target;

            // Вычисляем расстояние от камеры до центра модели
            var cameraToCenter = _camera.Target - Camera.Position;
            var distanceToCenter = cameraToCenter.Length;
            if (distanceToCenter < 0.001) return _camera.Target;

            // Нормализуем координаты мыши в диапазон [-1, 1]
            var x = (mousePosition.X / viewportWidth) * 2.0 - 1.0;
            var y = 1.0 - (mousePosition.Y / viewportHeight) * 2.0; // Инвертируем Y

            // Вычисляем направление луча из камеры через точку на экране
            var lookDir = Camera.LookDirection;
            lookDir.Normalize();
            
            var upDir = Camera.UpDirection;
            upDir.Normalize();
            
            var rightDir = Vector3D.CrossProduct(lookDir, upDir);
            rightDir.Normalize();
            
            var realUpDir = Vector3D.CrossProduct(rightDir, lookDir);
            realUpDir.Normalize();

            // Вычисляем угол обзора
            var fov = Camera.FieldOfView * Math.PI / 180.0;
            var aspectRatio = viewportWidth / viewportHeight;
            
            var tanFov = Math.Tan(fov / 2.0);
            
            // Вычисляем направление луча через точку на экране
            var rayDir = lookDir + rightDir * (x * tanFov * aspectRatio) + realUpDir * (y * tanFov);
            rayDir.Normalize();
            
            // Находим точку на луче на расстоянии от камеры, равном расстоянию до центра модели
            // Это даст нам точку на плоскости, проходящей через центр модели
            var pointOnRay = Camera.Position + rayDir * distanceToCenter;
            
            // Проецируем эту точку на плоскость, проходящую через центр модели перпендикулярно направлению взгляда
            var planeNormal = lookDir;
            var planePoint = _camera.Target;
            
            // Вычисляем расстояние от точки на луче до плоскости
            var distToPlane = Vector3D.DotProduct(pointOnRay - planePoint, planeNormal);
            
            // Проецируем на плоскость
            var projectedPoint = pointOnRay - planeNormal * distToPlane;
            
            return projectedPoint;
        }
    }
}
