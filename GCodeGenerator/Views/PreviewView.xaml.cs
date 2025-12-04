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
        private bool _isRotating;
        private Point _lastMousePosition;
        private double _cameraDistance = 100;

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
                        
                        // Isometric view position
                        var isoX = _modelCenter.X + _cameraDistance * 0.707;
                        var isoY = _modelCenter.Y + _cameraDistance * 0.707;
                        var isoZ = _modelCenter.Z + _cameraDistance * 0.5;
                        
                        Camera.Position = new Point3D(isoX, isoY, isoZ);
                        Camera.LookDirection = new Vector3D(_modelCenter.X - isoX, _modelCenter.Y - isoY, _modelCenter.Z - isoZ);
                        Camera.UpDirection = new Vector3D(0, 0, 1);
                        
                        // Update camera distance
                        var lookDir = Camera.LookDirection;
                        _cameraDistance = lookDir.Length;
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
                _lastMousePosition = e.GetPosition(Viewport);
                Viewport.CaptureMouse();
            }
        }

        private void Viewport_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isRotating || Camera == null) return;

            var currentPosition = e.GetPosition(Viewport);
            var deltaX = currentPosition.X - _lastMousePosition.X;
            var deltaY = currentPosition.Y - _lastMousePosition.Y;

            // Rotate around model center
            RotateCamera(-10.0*deltaX, -10.0*deltaY);

            _lastMousePosition = currentPosition;
        }

        private void Viewport_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Released)
            {
                _isRotating = false;
                Viewport.ReleaseMouseCapture();
            }
        }

        private void Viewport_MouseLeave(object sender, MouseEventArgs e)
        {
            _isRotating = false;
            Viewport.ReleaseMouseCapture();
        }

        private void RotateCamera(double deltaX, double deltaY)
        {
            // Get current camera position relative to model center
            var cameraOffset = Camera.Position - _modelCenter;
            var distance = cameraOffset.Length;
            
            if (distance < 0.001) return;

            // Normalize offset
            cameraOffset.Normalize();

            // Get right and up vectors
            var lookDir = -cameraOffset;
            var right = Vector3D.CrossProduct(lookDir, Camera.UpDirection);
            right.Normalize();
            var up = Vector3D.CrossProduct(right, lookDir);
            up.Normalize();

            // Rotation sensitivity
            var rotationSpeed = 0.5 * Math.PI / 180.0;

            // Horizontal rotation (around world up vector, which is Z)
            var worldUp = new Vector3D(0, 0, 1);
            var horizontalAngle = deltaX * rotationSpeed;
            var horizontalRotation = new AxisAngleRotation3D(worldUp, horizontalAngle);
            var horizontalTransform = new RotateTransform3D(horizontalRotation);
            var rotatedOffset = horizontalTransform.Transform(cameraOffset);

            // Vertical rotation (around right vector)
            var rightVector = Vector3D.CrossProduct(rotatedOffset, worldUp);
            if (rightVector.Length > 0.001)
            {
                rightVector.Normalize();
                var verticalAngle = -deltaY * rotationSpeed;
                var verticalRotation = new AxisAngleRotation3D(rightVector, verticalAngle);
                var verticalTransform = new RotateTransform3D(verticalRotation);
                rotatedOffset = verticalTransform.Transform(rotatedOffset);
            }

            // Update camera position
            rotatedOffset.Normalize();
            Camera.Position = _modelCenter + rotatedOffset * distance;
            Camera.LookDirection = _modelCenter - Camera.Position;
            
            // Update up direction
            var newUp = Vector3D.CrossProduct(rightVector, rotatedOffset);
            if (newUp.Length > 0.001)
            {
                newUp.Normalize();
                Camera.UpDirection = newUp;
            }
        }
    }
}

