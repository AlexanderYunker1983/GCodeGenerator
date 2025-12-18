using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using GCodeGenerator.ViewModels;

namespace GCodeGenerator.Views
{
    public partial class PreviewView : Window
    {
        private Point3D _modelCenter = new Point3D(0, 0, 0);
        private Point3D _rotationPivot = new Point3D(0, 0, 0); // Точка поворота при правой кнопке мыши
        private bool _isRotating;
        private bool _isPanning;
        private Point _lastMousePosition;
        private double _cameraDistance = 100;
        private double _theta = 0; // Current horizontal angle (azimuth)
        private double _phi = Math.PI / 2; // Current vertical angle (elevation), start at top view

        public PreviewView()
        {
            InitializeComponent();
            DataContextChanged += PreviewView_DataContextChanged;
        }

        private void PreviewView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is PreviewViewModel vm)
            {
                vm.PropertyChanged += (s, args) =>
                {
                    if (args.PropertyName == nameof(PreviewViewModel.TrajectoryModel))
                    {
                        UpdateTrajectoryModel(vm.TrajectoryModel);
                    }
                };
                
                if (vm.TrajectoryModel != null)
                {
                    UpdateTrajectoryModel(vm.TrajectoryModel);
                }
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
                    if (!bounds.IsEmpty)
                    {
                        _modelCenter = new Point3D(
                            (bounds.X + bounds.SizeX / 2),
                            (bounds.Y + bounds.SizeY / 2),
                            (bounds.Z + bounds.SizeZ / 2));
                        
                        var maxSize = Math.Max(Math.Max(bounds.SizeX, bounds.SizeY), bounds.SizeZ);
                        _cameraDistance = maxSize * 2;
                        
                        // Top view (XY projection) - camera above the model looking down
                        var cameraX = _modelCenter.X;
                        var cameraY = _modelCenter.Y;
                        var cameraZ = _modelCenter.Z + _cameraDistance;
                        
                        Camera.Position = new Point3D(cameraX, cameraY, cameraZ);
                        Camera.LookDirection = new Vector3D(0, 0, -_cameraDistance); // Looking straight down
                        Camera.UpDirection = new Vector3D(0, 1, 0); // Y-axis up for correct orientation
                        
                        // Update camera distance
                        _cameraDistance = _cameraDistance;
                        
                        // Initialize spherical angles for top view
                        _theta = 0; // Start facing along positive X axis
                        _phi = Math.PI / 2; // Top view (90 degrees elevation)
                    }
                }
            }
        }

        private void Viewport_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Camera == null) return;

            var delta = e.Delta < 0 ? 1.1 : 0.9;
            _cameraDistance *= delta;

            // Clamp distance
            _cameraDistance = Math.Max(0.1, Math.Min(_cameraDistance, 10000));

            // Update camera position while maintaining look direction
            var lookDir = Camera.LookDirection;
            lookDir.Normalize();
            Camera.Position = _modelCenter - lookDir * _cameraDistance;
        }

        private void Viewport_MouseDown(object sender, MouseButtonEventArgs e)
        {
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

        private void Viewport_MouseMove(object sender, MouseEventArgs e)
        {
            if (Camera == null) return;

            var currentPosition = e.GetPosition(Viewport);
            var deltaX = currentPosition.X - _lastMousePosition.X;
            var deltaY = currentPosition.Y - _lastMousePosition.Y;

            if (_isPanning && e.RightButton == MouseButtonState.Pressed)
            {
                // Панорамирование при правой кнопке мыши
                PanCamera(deltaX, deltaY);
            }
            else if (_isRotating && e.LeftButton == MouseButtonState.Pressed)
            {
                // Поворот вокруг центра модели при левой кнопке мыши
                if (Math.Abs(deltaX) > 0 || Math.Abs(deltaY) > 0)
                {
                    RotateCamera(deltaX, deltaY, _modelCenter);
                }
            }
            else if (_isRotating && e.RightButton == MouseButtonState.Pressed)
            {
                // Поворот вокруг точки под курсором при правой кнопке мыши (с Shift)
                if (Math.Abs(deltaX) > 0 || Math.Abs(deltaY) > 0)
                {
                    RotateCamera(deltaX, deltaY, _rotationPivot);
                }
            }

            _lastMousePosition = currentPosition;
            e.Handled = true;
        }

        private void Viewport_MouseUp(object sender, MouseButtonEventArgs e)
        {
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

        private void RotateCamera(double deltaX, double deltaY, Point3D pivotPoint)
        {
            if (Camera == null) return;

            // Get current camera distance from pivot point
            var cameraOffset = Camera.Position - pivotPoint;
            var distance = cameraOffset.Length;
            
            if (distance < 0.001) return;

            // Rotation sensitivity (radians per pixel)
            var rotationSpeed = 0.01;

            // Update angles based on mouse movement (use stored angles to avoid jumps)
            _theta -= deltaX * rotationSpeed; // Horizontal rotation (inverted)
            _phi -= deltaY * rotationSpeed;   // Vertical rotation (inverted)

            // Clamp phi to prevent flipping (keep between 0.1 and PI - 0.1)
            _phi = Math.Max(0.1, Math.Min(Math.PI - 0.1, _phi));

            // Convert back to Cartesian coordinates using stored angles
            var newX = distance * Math.Sin(_phi) * Math.Cos(_theta);
            var newY = distance * Math.Sin(_phi) * Math.Sin(_theta);
            var newZ = distance * Math.Cos(_phi);

            // Update camera position relative to pivot point
            var newOffset = new Vector3D(newX, newY, newZ);
            Camera.Position = pivotPoint + newOffset;

            // Update look direction
            Camera.LookDirection = pivotPoint - Camera.Position;

            // Обновляем центр модели, сохраняя его относительное положение к точке поворота
            if (pivotPoint != _modelCenter)
            {
                var modelOffsetFromPivot = _modelCenter - pivotPoint;
                _modelCenter = pivotPoint + modelOffsetFromPivot;
            }

            // Update up direction (maintain Y-up orientation)
            var worldUp = new Vector3D(0, 0, 1);
            var right = Vector3D.CrossProduct(newOffset, worldUp);
            if (right.Length > 0.001)
            {
                right.Normalize();
                var newUp = Vector3D.CrossProduct(right, newOffset);
                if (newUp.Length > 0.001)
                {
                    newUp.Normalize();
                    Camera.UpDirection = newUp;
                }
                else
                {
                    Camera.UpDirection = new Vector3D(0, 1, 0);
                }
            }
            else
            {
                Camera.UpDirection = new Vector3D(0, 1, 0);
            }
        }

        private void PanCamera(double deltaX, double deltaY)
        {
            if (Camera == null) return;

            // Получаем векторы направления камеры
            var lookDir = Camera.LookDirection;
            lookDir.Normalize();
            
            var upDir = Camera.UpDirection;
            upDir.Normalize();
            
            // Вычисляем вектор "вправо" относительно камеры
            var rightDir = Vector3D.CrossProduct(lookDir, upDir);
            rightDir.Normalize();
            
            // Вычисляем реальный вектор "вверх" для панорамирования
            var panUpDir = Vector3D.CrossProduct(rightDir, lookDir);
            panUpDir.Normalize();

            // Чувствительность панорамирования (зависит от расстояния камеры)
            var panSpeed = _cameraDistance * 0.001;
            
            // Вычисляем смещение в пространстве
            var panOffset = rightDir * (-deltaX * panSpeed) + panUpDir * (deltaY * panSpeed);
            
            // Перемещаем камеру и центр модели вместе
            Camera.Position += panOffset;
            _modelCenter += panOffset;
            
            // Обновляем направление взгляда
            Camera.LookDirection = _modelCenter - Camera.Position;
        }

        private Point3D GetPointUnderCursor(Point mousePosition)
        {
            if (Camera == null || Viewport == null) return _modelCenter;

            // Получаем размеры viewport
            var viewportWidth = Viewport.ActualWidth;
            var viewportHeight = Viewport.ActualHeight;
            
            if (viewportWidth < 1 || viewportHeight < 1) return _modelCenter;

            // Вычисляем расстояние от камеры до центра модели
            var cameraToCenter = _modelCenter - Camera.Position;
            var distanceToCenter = cameraToCenter.Length;
            if (distanceToCenter < 0.001) return _modelCenter;

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
            var planePoint = _modelCenter;
            
            // Вычисляем расстояние от точки на луче до плоскости
            var distToPlane = Vector3D.DotProduct(pointOnRay - planePoint, planeNormal);
            
            // Проецируем на плоскость
            var projectedPoint = pointOnRay - planeNormal * distToPlane;
            
            return projectedPoint;
        }
    }
}

