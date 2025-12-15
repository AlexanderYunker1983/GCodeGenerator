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
        }

        private void Viewport_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isRotating || Camera == null) return;

            var currentPosition = e.GetPosition(this);
            var deltaX = currentPosition.X - _lastMousePosition.X;
            var deltaY = currentPosition.Y - _lastMousePosition.Y;

            // Rotate around model center (even small movements)
            if (Math.Abs(deltaX) > 0 || Math.Abs(deltaY) > 0)
            {
                RotateCamera(deltaX, deltaY);
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
        }

        private void RotateCamera(double deltaX, double deltaY)
        {
            if (Camera == null) return;

            // Get current camera distance
            var cameraOffset = Camera.Position - _modelCenter;
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

            // Update camera position
            var newOffset = new Vector3D(newX, newY, newZ);
            Camera.Position = _modelCenter + newOffset;

            // Update look direction
            Camera.LookDirection = _modelCenter - Camera.Position;

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
    }
}

