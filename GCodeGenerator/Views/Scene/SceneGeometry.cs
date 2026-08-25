using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace GCodeGenerator.Views.Scene
{
    /// <summary>
    /// Примитивы трёхмерной сцены: отрезок заданной толщины, штриховой
    /// отрезок, конус и шар.
    ///
    /// Линия рисуется коробкой прямоугольного сечения, потому что WPF не
    /// умеет толстые трёхмерные линии. Все построения добавляют вершины в
    /// уже существующий меш: тысячи отрезков траектории собираются в
    /// несколько мешей, иначе на каждый отрезок пришлось бы по объекту сцены.
    /// </summary>
    internal static class SceneGeometry
    {
        /// <summary>Ниже этой длины отрезок считается вырожденным.</summary>
        private const double DegenerateLength = 0.0001;

        /// <summary>Число граней боковой поверхности конуса.</summary>
        private const int ConeSegments = 16;

        /// <summary>Разбиение шара: меридианы и параллели.</summary>
        private const int SphereSegments = 12;
        private const int SphereRings = 8;

        /// <summary>Отрезок толщиной <paramref name="thickness"/> в новом меше.</summary>
        public static MeshGeometry3D CreateLine(Point3D start, Point3D end, double thickness)
        {
            var mesh = new MeshGeometry3D();
            AddLine(mesh, start, end, thickness);
            return mesh;
        }

        /// <summary>Добавляет отрезок толщиной <paramref name="thickness"/> в меш.</summary>
        public static void AddLine(MeshGeometry3D mesh, Point3D start, Point3D end, double thickness)
        {
            var direction = end - start;
            var length = direction.Length;
            if (length < DegenerateLength)
                return;

            direction /= length;
            if (!TryGetPerpendiculars(direction, out var perp1, out var perp2))
                return;

            var halfThickness = thickness * 0.5;
            perp1 *= halfThickness;
            perp2 *= halfThickness;

            // Восемь вершин коробки: четыре у начала отрезка, четыре у конца.
            var corners = new List<Point3D>
            {
                start + perp1 + perp2,
                start + perp1 - perp2,
                start - perp1 - perp2,
                start - perp1 + perp2,
                end + perp1 + perp2,
                end + perp1 - perp2,
                end - perp1 - perp2,
                end - perp1 + perp2
            };

            AppendMesh(mesh, corners, BoxIndices, CalculateNormals(corners, BoxIndices));
        }

        /// <summary>
        /// Добавляет штриховой отрезок: чередование штрихов и промежутков
        /// заданной длины. Так изображаются холостые перемещения.
        /// </summary>
        public static void AddDashedLine(MeshGeometry3D mesh, Point3D start, Point3D end,
            double thickness, double dashLength, double gapLength)
        {
            var direction = end - start;
            var totalLength = direction.Length;
            if (totalLength < DegenerateLength)
                return;

            direction.Normalize();
            var distance = 0.0;
            var isDash = true;

            while (distance < totalLength)
            {
                var pieceLength = Math.Min(isDash ? dashLength : gapLength, totalLength - distance);

                if (isDash && pieceLength > 0.001)
                    AddLine(mesh, start + direction * distance, start + direction * (distance + pieceLength), thickness);

                distance += pieceLength;
                isDash = !isDash;
            }
        }

        /// <summary>Конус от основания к вершине — наконечник стрелки оси.</summary>
        public static void AddCone(MeshGeometry3D mesh, Point3D baseCenter, Point3D tip, double radius)
        {
            var direction = tip - baseCenter;
            var length = direction.Length;
            if (length < DegenerateLength)
                return;

            direction.Normalize();
            if (!TryGetPerpendiculars(direction, out var perp1, out var perp2))
                return;

            var positions = new List<Point3D> { tip };
            var normals = new List<Vector3D> { direction };
            var indices = new List<int>();

            for (var i = 0; i <= ConeSegments; i++)
            {
                var angle = i * 2 * Math.PI / ConeSegments;
                var point = baseCenter + perp1 * (radius * Math.Cos(angle)) + perp2 * (radius * Math.Sin(angle));
                positions.Add(point);

                var toPoint = point - baseCenter;
                var sideNormal = Vector3D.CrossProduct(Vector3D.CrossProduct(direction, toPoint), direction - toPoint);
                sideNormal.Normalize();
                normals.Add(sideNormal);
            }

            var baseCenterIndex = positions.Count;
            positions.Add(baseCenter);
            normals.Add(-direction);

            for (var i = 1; i <= ConeSegments; i++)
            {
                // Боковая грань.
                indices.Add(0);
                indices.Add(i);
                indices.Add(i + 1);

                // Донышко.
                indices.Add(baseCenterIndex);
                indices.Add(i + 1);
                indices.Add(i);
            }

            AppendMesh(mesh, positions, indices, normals);
        }

        /// <summary>Шар — маркер точки и начала координат.</summary>
        public static void AddSphere(MeshGeometry3D mesh, Point3D center, double radius)
        {
            var positions = new List<Point3D>();
            var normals = new List<Vector3D>();
            var indices = new List<int>();

            for (var ring = 0; ring <= SphereRings; ring++)
            {
                var theta = ring * Math.PI / SphereRings;
                var sinTheta = Math.Sin(theta);
                var cosTheta = Math.Cos(theta);

                for (var segment = 0; segment <= SphereSegments; segment++)
                {
                    var phi = segment * 2 * Math.PI / SphereSegments;
                    var normal = new Vector3D(sinTheta * Math.Cos(phi), sinTheta * Math.Sin(phi), cosTheta);

                    positions.Add(center + normal * radius);
                    normals.Add(normal);
                }
            }

            for (var ring = 0; ring < SphereRings; ring++)
            {
                for (var segment = 0; segment < SphereSegments; segment++)
                {
                    var current = ring * (SphereSegments + 1) + segment;
                    var next = current + SphereSegments + 1;

                    indices.Add(current);
                    indices.Add(next);
                    indices.Add(current + 1);

                    indices.Add(current + 1);
                    indices.Add(next);
                    indices.Add(next + 1);
                }
            }

            AppendMesh(mesh, positions, indices, normals);
        }

        /// <summary>
        /// Добавляет вершины, грани и нормали в конец меша. Индексы граней
        /// сдвигаются на число уже имеющихся вершин.
        /// </summary>
        private static void AppendMesh(MeshGeometry3D mesh,
            IEnumerable<Point3D> positions, IEnumerable<int> indices, IEnumerable<Vector3D> normals)
        {
            mesh.Positions ??= new Point3DCollection();
            mesh.TriangleIndices ??= new Int32Collection();
            mesh.Normals ??= new Vector3DCollection();

            var baseIndex = mesh.Positions.Count;

            foreach (var position in positions)
                mesh.Positions.Add(position);

            if (normals != null)
            {
                foreach (var normal in normals)
                    mesh.Normals.Add(normal);
            }

            if (indices != null)
            {
                foreach (var index in indices)
                    mesh.TriangleIndices.Add(index + baseIndex);
            }
        }

        /// <summary>
        /// Два взаимно перпендикулярных вектора, перпендикулярных направлению.
        /// Опорная ось выбирается по наименьшей компоненте направления, иначе
        /// векторное произведение вырождается для осевых отрезков.
        /// </summary>
        private static bool TryGetPerpendiculars(Vector3D direction, out Vector3D perp1, out Vector3D perp2)
        {
            var absX = Math.Abs(direction.X);
            var absY = Math.Abs(direction.Y);
            var absZ = Math.Abs(direction.Z);

            Vector3D axis;
            if (absX <= absY && absX <= absZ)
                axis = new Vector3D(1, 0, 0);
            else if (absY <= absX && absY <= absZ)
                axis = new Vector3D(0, 1, 0);
            else
                axis = new Vector3D(0, 0, 1);

            perp1 = Vector3D.CrossProduct(direction, axis);
            perp2 = default;

            if (perp1.Length < DegenerateLength)
                return false;

            perp1.Normalize();
            perp2 = Vector3D.CrossProduct(direction, perp1);
            perp2.Normalize();
            return true;
        }

        /// <summary>
        /// Нормали вершин как усреднение нормалей примыкающих граней:
        /// освещение коробки без этого выглядит плоским.
        /// </summary>
        private static List<Vector3D> CalculateNormals(IReadOnlyList<Point3D> positions, IReadOnlyList<int> indices)
        {
            var normals = new Vector3D[positions.Count];

            for (var i = 0; i < indices.Count; i += 3)
            {
                var i0 = indices[i];
                var i1 = indices[i + 1];
                var i2 = indices[i + 2];

                var normal = Vector3D.CrossProduct(positions[i1] - positions[i0], positions[i2] - positions[i0]);
                if (normal.Length <= DegenerateLength)
                    continue;

                normal.Normalize();
                normals[i0] += normal;
                normals[i1] += normal;
                normals[i2] += normal;
            }

            var result = new List<Vector3D>(normals.Length);
            foreach (var normal in normals)
            {
                var vertexNormal = normal;
                if (vertexNormal.Length > DegenerateLength)
                    vertexNormal.Normalize();
                else
                    vertexNormal = new Vector3D(0, 0, 1);

                result.Add(vertexNormal);
            }

            return result;
        }

        /// <summary>Грани коробки: два торца и четыре боковые стенки.</summary>
        private static readonly int[] BoxIndices =
        {
            0, 2, 1,  0, 3, 2,
            4, 5, 6,  4, 6, 7,
            0, 4, 7,  0, 7, 3,
            1, 2, 6,  1, 6, 5,
            0, 1, 5,  0, 5, 4,
            2, 3, 7,  2, 7, 6
        };
    }
}
