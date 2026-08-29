#nullable enable
using System.Windows.Media.Media3D;
using GCodeGenerator.Trajectory;
using GCodeGenerator.Views.Scene;

namespace GCodeGenerator.Views
{
    /// <summary>
    /// Собирает трёхмерную модель окна превью из траектории программы
    /// (<see cref="TrajectoryScene"/> — данные ядра).
    ///
    /// Отвечает только за сборку: геометрию строит
    /// <see cref="TrajectoryMeshBuilder"/>, цвета даёт <see cref="SceneMaterials"/>,
    /// оси — <see cref="CoordinateAxesBuilder"/>. Прежде все три задачи
    /// решались здесь же, вперемешку с формулами вершин.
    /// </summary>
    internal static class SceneRenderer
    {
        /// <summary>Строит модель сцены в заданной палитре.</summary>
        public static Model3DGroup Render(TrajectoryScene scene, SceneMaterials materials)
        {
            var modelGroup = new Model3DGroup();
            var meshes = TrajectoryMeshBuilder.Build(scene);

            // Оси показываются всегда — даже для пустой программы.
            CoordinateAxesBuilder.AddTo(modelGroup, meshes, materials);

            AddMesh(modelGroup, meshes.Rapid, materials.Rapid);
            AddMesh(modelGroup, meshes.Linear, materials.Linear);
            AddMesh(modelGroup, meshes.ArcCW, materials.ArcCW);
            AddMesh(modelGroup, meshes.ArcCCW, materials.ArcCCW);

            foreach (var marker in meshes.Markers)
            {
                var sphere = new MeshGeometry3D();
                SceneGeometry.AddSphere(sphere, marker.Position, marker.Radius);
                modelGroup.Children.Add(new GeometryModel3D(sphere, MarkerMaterial(marker.Role, materials)));
            }

            modelGroup.Children.Add(new AmbientLight(materials.Ambient));
            modelGroup.Freeze();
            return modelGroup;
        }

        private static void AddMesh(Model3DGroup modelGroup, MeshGeometry3D mesh, Material material)
        {
            if (mesh.Positions == null || mesh.Positions.Count == 0)
                return;

            modelGroup.Children.Add(new GeometryModel3D(mesh, material) { BackMaterial = material });
        }

        private static Material MarkerMaterial(MarkerRole role, SceneMaterials materials)
            => role switch
            {
                MarkerRole.Start => materials.StartMarker,
                MarkerRole.End => materials.EndMarker,
                _ => materials.TransitionMarker
            };
    }
}
