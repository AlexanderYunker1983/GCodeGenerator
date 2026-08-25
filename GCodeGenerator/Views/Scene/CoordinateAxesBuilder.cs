#nullable enable
using System.Windows.Media.Media3D;

namespace GCodeGenerator.Views.Scene
{
    /// <summary>
    /// Координатные оси сцены: три стрелки из начала координат, буквенные
    /// метки и шарик в нуле детали.
    ///
    /// Метки рисуются палочками, а не текстом: надпись в трёхмерной сцене
    /// требует отдельной поверхности с разворотом к камере, а различить три
    /// буквы достаточно и по форме.
    /// </summary>
    internal static class CoordinateAxesBuilder
    {
        /// <summary>Толщина палочек буквы относительно её размера.</summary>
        private const double LabelThicknessRatio = 0.15;

        /// <summary>Размер буквы относительно радиуса наконечника.</summary>
        private const double LabelSizeRatio = 1.5;

        public static void AddTo(Model3DGroup modelGroup, TrajectoryMeshes meshes, SceneMaterials materials)
        {
            var origin = new Point3D(0, 0, 0);
            var axisLength = meshes.AxisLength;
            var arrowLength = meshes.ArrowLength;
            var arrowRadius = meshes.ArrowRadius;
            var labelOffset = axisLength + arrowLength * 0.5;

            AddAxis(modelGroup, origin, new Point3D(axisLength, 0, 0), new Point3D(axisLength - arrowLength, 0, 0),
                new Point3D(labelOffset, 0, 0), AxisLabel.X, meshes.LineThickness, arrowRadius, materials.XAxis);

            AddAxis(modelGroup, origin, new Point3D(0, axisLength, 0), new Point3D(0, axisLength - arrowLength, 0),
                new Point3D(0, labelOffset, 0), AxisLabel.Y, meshes.LineThickness, arrowRadius, materials.YAxis);

            AddAxis(modelGroup, origin, new Point3D(0, 0, axisLength), new Point3D(0, 0, axisLength - arrowLength),
                new Point3D(0, 0, labelOffset), AxisLabel.Z, meshes.LineThickness, arrowRadius, materials.ZAxis);

            var originSphere = new MeshGeometry3D();
            SceneGeometry.AddSphere(originSphere, origin, meshes.LineThickness * 2);
            modelGroup.Children.Add(new GeometryModel3D(originSphere, materials.Origin));
        }

        private enum AxisLabel
        {
            X,
            Y,
            Z
        }

        private static void AddAxis(Model3DGroup modelGroup, Point3D origin, Point3D end, Point3D arrowStart,
            Point3D labelPosition, AxisLabel label, double thickness, double arrowRadius, Material material)
        {
            AddModel(modelGroup, SceneGeometry.CreateLine(origin, arrowStart, thickness), material);

            var arrow = new MeshGeometry3D();
            SceneGeometry.AddCone(arrow, arrowStart, end, arrowRadius);
            AddModel(modelGroup, arrow, material);

            AddModel(modelGroup, CreateLabel(label, labelPosition, arrowRadius * LabelSizeRatio), material);
        }

        private static void AddModel(Model3DGroup modelGroup, MeshGeometry3D mesh, Material material)
        {
            if (mesh.Positions == null || mesh.Positions.Count == 0)
                return;

            modelGroup.Children.Add(new GeometryModel3D(mesh, material) { BackMaterial = material });
        }

        /// <summary>Буква оси из отрезков в плоскости XY.</summary>
        private static MeshGeometry3D CreateLabel(AxisLabel label, Point3D center, double size)
        {
            var mesh = new MeshGeometry3D();
            var thickness = size * LabelThicknessRatio;
            var half = size * 0.5;

            switch (label)
            {
                case AxisLabel.X:
                    // Два перекрещенных штриха.
                    AddStroke(mesh, center, -half, -half, half, half, thickness);
                    AddStroke(mesh, center, -half, half, half, -half, thickness);
                    break;

                case AxisLabel.Y:
                    // Две верхние ветви сходятся в центре, от него — ножка вниз.
                    AddStroke(mesh, center, -half * 0.7, half, 0, 0, thickness);
                    AddStroke(mesh, center, half * 0.7, half, 0, 0, thickness);
                    AddStroke(mesh, center, 0, 0, 0, -half, thickness);
                    break;

                case AxisLabel.Z:
                    // Верхняя полка, диагональ, нижняя полка.
                    AddStroke(mesh, center, -half * 0.6, half * 0.5, half * 0.6, half * 0.5, thickness);
                    AddStroke(mesh, center, half * 0.6, half * 0.5, -half * 0.6, -half * 0.5, thickness);
                    AddStroke(mesh, center, -half * 0.6, -half * 0.5, half * 0.6, -half * 0.5, thickness);
                    break;
            }

            return mesh;
        }

        /// <summary>Штрих буквы: смещения задаются от её центра.</summary>
        private static void AddStroke(MeshGeometry3D mesh, Point3D center,
            double startX, double startY, double endX, double endY, double thickness)
        {
            SceneGeometry.AddLine(mesh,
                new Point3D(center.X + startX, center.Y + startY, center.Z),
                new Point3D(center.X + endX, center.Y + endY, center.Z),
                thickness);
        }
    }
}
