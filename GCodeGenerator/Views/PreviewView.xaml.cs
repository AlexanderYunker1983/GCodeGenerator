using System;
using System.Windows;
using System.Windows.Media.Media3D;
using GCodeGenerator.ViewModels;

namespace GCodeGenerator.Views
{
    public partial class PreviewView : Window
    {
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
                        var center = new Point3D(
                            (bounds.X + bounds.SizeX / 2),
                            (bounds.Y + bounds.SizeY / 2),
                            (bounds.Z + bounds.SizeZ / 2));
                        
                        var maxSize = Math.Max(Math.Max(bounds.SizeX, bounds.SizeY), bounds.SizeZ);
                        var distance = maxSize * 2;
                        
                        // Isometric view position
                        var isoX = center.X + distance * 0.707;
                        var isoY = center.Y + distance * 0.707;
                        var isoZ = center.Z + distance * 0.5;
                        
                        Camera.Position = new Point3D(isoX, isoY, isoZ);
                        Camera.LookDirection = new Vector3D(center.X - isoX, center.Y - isoY, center.Z - isoZ);
                        Camera.UpDirection = new Vector3D(0, 0, 1);
                    }
                }
            }
        }
    }
}

