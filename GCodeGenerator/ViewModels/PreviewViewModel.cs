using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using MugenMvvmToolkit.Interfaces.Models;
using MugenMvvmToolkit.ViewModels;
using YLocalization;

namespace GCodeGenerator.ViewModels
{
    public class PreviewViewModel : CloseableViewModel, IHasDisplayName
    {
        private readonly ILocalizationManager _localizationManager;
        private string _gCodeText;
        private Model3DGroup _trajectoryModel;

        public PreviewViewModel(ILocalizationManager localizationManager)
        {
            _localizationManager = localizationManager;
            var title = _localizationManager?.GetString("PreviewGCode");
            DisplayName = string.IsNullOrEmpty(title) ? "Предварительный просмотр G-кода" : title;
            TrajectoryModel = new Model3DGroup();
        }

        public string GCodeText
        {
            get => _gCodeText;
            set
            {
                if (Equals(value, _gCodeText)) return;
                _gCodeText = value;
                OnPropertyChanged();
                ParseAndBuildModel();
            }
        }

        public Model3DGroup TrajectoryModel
        {
            get => _trajectoryModel;
            set
            {
                if (Equals(value, _trajectoryModel)) return;
                _trajectoryModel = value;
                OnPropertyChanged();
            }
        }

        private void ParseAndBuildModel()
        {
            if (string.IsNullOrEmpty(_gCodeText))
            {
                TrajectoryModel = new Model3DGroup();
                return;
            }

            var lines = _gCodeText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var segments = new List<(Point3D start, Point3D end, bool isRapid)>();
            var currentPos = new Point3D(0, 0, 0);
            var isRapidMove = false;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("("))
                    continue;

                // Remove line numbers (N10, N20, etc.)
                var codeLine = trimmed;
                if (codeLine.StartsWith("N", StringComparison.OrdinalIgnoreCase))
                {
                    var spaceIndex = codeLine.IndexOf(' ');
                    if (spaceIndex > 0)
                        codeLine = codeLine.Substring(spaceIndex + 1).Trim();
                    else
                        continue;
                }

                // Parse G0/G00 (rapid move) or G1/G01 (work move)
                // Look for G commands anywhere in the line
                var gIndex = codeLine.IndexOf("G", StringComparison.OrdinalIgnoreCase);
                if (gIndex >= 0 && gIndex + 1 < codeLine.Length)
                {
                    var gCode = codeLine.Substring(gIndex + 1);
                    // Check for G00 or G0 (but not G01)
                    if (gCode.StartsWith("00", StringComparison.OrdinalIgnoreCase) ||
                        (gCode.StartsWith("0", StringComparison.OrdinalIgnoreCase) && 
                         !gCode.StartsWith("01", StringComparison.OrdinalIgnoreCase)))
                    {
                        isRapidMove = true;
                    }
                    else if (gCode.StartsWith("01", StringComparison.OrdinalIgnoreCase) ||
                             gCode.StartsWith("1", StringComparison.OrdinalIgnoreCase))
                    {
                        isRapidMove = false;
                    }
                }

                // Parse coordinates
                var x = ParseCoordinate(codeLine, 'X', currentPos.X);
                var y = ParseCoordinate(codeLine, 'Y', currentPos.Y);
                var z = ParseCoordinate(codeLine, 'Z', currentPos.Z);

                // If any coordinate changed, create a segment
                if (Math.Abs(x - currentPos.X) > 0.001 ||
                    Math.Abs(y - currentPos.Y) > 0.001 ||
                    Math.Abs(z - currentPos.Z) > 0.001)
                {
                    var startPos = currentPos;
                    currentPos = new Point3D(x, y, z);
                    segments.Add((startPos, currentPos, isRapidMove));
                }
            }

            BuildModelFromSegments(segments);
        }

        private double ParseCoordinate(string line, char axis, double defaultValue)
        {
            var axisStr = axis.ToString();
            var index = line.IndexOf(axisStr, StringComparison.OrdinalIgnoreCase);
            if (index < 0) return defaultValue;

            var start = index + 1;
            var end = start;
            while (end < line.Length && (char.IsDigit(line[end]) || line[end] == '.' || line[end] == '-' || line[end] == '+'))
            {
                end++;
            }

            if (end > start)
            {
                var valueStr = line.Substring(start, end - start);
                if (double.TryParse(valueStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                    return value;
            }

            return defaultValue;
        }

        private void BuildModelFromSegments(List<(Point3D start, Point3D end, bool isRapid)> segments)
        {
            var modelGroup = new Model3DGroup();

            if (segments.Count == 0)
            {
                TrajectoryModel = modelGroup;
                return;
            }

            // Calculate bounding box to determine model scale
            var allPoints = segments.SelectMany(s => new[] { s.start, s.end }).ToList();
            var minX = allPoints.Min(p => p.X);
            var maxX = allPoints.Max(p => p.X);
            var minY = allPoints.Min(p => p.Y);
            var maxY = allPoints.Max(p => p.Y);
            var minZ = allPoints.Min(p => p.Z);
            var maxZ = allPoints.Max(p => p.Z);

            var sizeX = maxX - minX;
            var sizeY = maxY - minY;
            var sizeZ = maxZ - minZ;
            var maxSize = Math.Max(Math.Max(sizeX, sizeY), sizeZ);

            // Calculate adaptive thickness based on model size
            // Use larger percentage for better visibility
            var baseThickness = maxSize > 0 ? maxSize * 0.02 : 2.0;
            // Make thickness difference very noticeable
            var rapidThickness = Math.Max(baseThickness * 0.05, 0.05); // Very thin line for rapid moves
            var workThickness = Math.Max(baseThickness * 10.0, 2.0);   // Very thick line for work moves (20% of model, min 2.0)

            // Create material for trajectory
            // Rapid moves: blue, 50% transparency, thin line
            var rapidBrush = new SolidColorBrush(Color.FromArgb(128, 0, 0, 255)); // Blue with 50% opacity
            var rapidMaterial = new DiffuseMaterial(rapidBrush);
            
            // Work moves: red, fully opaque, thick line - use ONLY EmissiveMaterial for maximum visibility
            var workBrush = new SolidColorBrush(Color.FromArgb(255, 255, 0, 0)); // Red fully opaque
            var workMaterial = new EmissiveMaterial(workBrush); // Emissive makes it always visible, no lighting needed

            // Build lines from segments
            foreach (var segment in segments)
            {
                var start = segment.start;
                var end = segment.end;
                var isRapid = segment.isRapid;

                var material = isRapid ? (Material)rapidMaterial : workMaterial;

                var lineGeometry = new MeshGeometry3D();
                // Rapid moves: thin line, work moves: thick line
                var thickness = isRapid ? rapidThickness : workThickness;
                CreateLineGeometry(lineGeometry, start, end, thickness);
                
                var lineModel = new GeometryModel3D(lineGeometry, material);
                modelGroup.Children.Add(lineModel);
            }

            // Add point markers (smaller spheres) - only unique points
            var uniquePoints = segments.SelectMany(s => new[] { s.start, s.end }).Distinct().ToList();
            var pointMaterial = new DiffuseMaterial(new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green));
            foreach (var point in uniquePoints)
            {
                var sphereGeometry = new MeshGeometry3D();
                CreateSphereGeometry(sphereGeometry, point, 0.15);
                var sphereModel = new GeometryModel3D(sphereGeometry, pointMaterial);
                modelGroup.Children.Add(sphereModel);
            }

            TrajectoryModel = modelGroup;
        }

        private void CreateLineGeometry(MeshGeometry3D geometry, Point3D start, Point3D end, double thickness)
        {
            // Simple line as a thin box
            var dx = end.X - start.X;
            var dy = end.Y - start.Y;
            var dz = end.Z - start.Z;
            var length = Math.Sqrt(dx * dx + dy * dy + dz * dz);

            if (length < 0.001) return;

            // Create a simple line segment
            var positions = new List<Point3D>();
            var indices = new List<int>();

            // Create a thin box along the line
            var perp1 = new Vector3D(-dy, dx, 0);
            if (perp1.Length < 0.001)
                perp1 = new Vector3D(0, -dz, dy);
            perp1.Normalize();
            perp1 *= thickness;

            var perp2 = Vector3D.CrossProduct(new Vector3D(dx, dy, dz), perp1);
            perp2.Normalize();
            perp2 *= thickness;

            var v0 = start + perp1 + perp2;
            var v1 = start + perp1 - perp2;
            var v2 = start - perp1 - perp2;
            var v3 = start - perp1 + perp2;
            var v4 = end + perp1 + perp2;
            var v5 = end + perp1 - perp2;
            var v6 = end - perp1 - perp2;
            var v7 = end - perp1 + perp2;

            var baseIndex = positions.Count;
            positions.AddRange(new[] { v0, v1, v2, v3, v4, v5, v6, v7 });

            // Front face
            indices.AddRange(new[] { baseIndex + 0, baseIndex + 1, baseIndex + 2, baseIndex + 0, baseIndex + 2, baseIndex + 3 });
            // Back face
            indices.AddRange(new[] { baseIndex + 4, baseIndex + 6, baseIndex + 5, baseIndex + 4, baseIndex + 7, baseIndex + 6 });
            // Top face
            indices.AddRange(new[] { baseIndex + 0, baseIndex + 4, baseIndex + 5, baseIndex + 0, baseIndex + 5, baseIndex + 1 });
            // Bottom face
            indices.AddRange(new[] { baseIndex + 2, baseIndex + 6, baseIndex + 7, baseIndex + 2, baseIndex + 7, baseIndex + 3 });
            // Left face
            indices.AddRange(new[] { baseIndex + 0, baseIndex + 3, baseIndex + 7, baseIndex + 0, baseIndex + 7, baseIndex + 4 });
            // Right face
            indices.AddRange(new[] { baseIndex + 1, baseIndex + 5, baseIndex + 6, baseIndex + 1, baseIndex + 6, baseIndex + 2 });

            geometry.Positions = new Point3DCollection(positions);
            geometry.TriangleIndices = new Int32Collection(indices);
            
            // Calculate normals for proper lighting
            geometry.Normals = CalculateNormals(positions, indices);
        }

        private Vector3DCollection CalculateNormals(List<Point3D> positions, List<int> indices)
        {
            var normals = new Vector3D[positions.Count];
            
            // Initialize all normals to zero
            for (int i = 0; i < normals.Length; i++)
            {
                normals[i] = new Vector3D(0, 0, 0);
            }
            
            // Calculate face normals and accumulate
            for (int i = 0; i < indices.Count; i += 3)
            {
                var i0 = indices[i];
                var i1 = indices[i + 1];
                var i2 = indices[i + 2];
                
                var v0 = positions[i0];
                var v1 = positions[i1];
                var v2 = positions[i2];
                
                var edge1 = v1 - v0;
                var edge2 = v2 - v0;
                var normal = Vector3D.CrossProduct(edge1, edge2);
                normal.Normalize();
                
                normals[i0] += normal;
                normals[i1] += normal;
                normals[i2] += normal;
            }
            
            // Normalize all normals
            for (int i = 0; i < normals.Length; i++)
            {
                normals[i].Normalize();
            }
            
            return new Vector3DCollection(normals);
        }

        private void CreateSphereGeometry(MeshGeometry3D geometry, Point3D center, double radius)
        {
            var positions = new List<Point3D>();
            var indices = new List<int>();

            int segments = 8;
            int rings = 8;

            for (int ring = 0; ring <= rings; ring++)
            {
                var theta = ring * Math.PI / rings;
                var sinTheta = Math.Sin(theta);
                var cosTheta = Math.Cos(theta);

                for (int segment = 0; segment <= segments; segment++)
                {
                    var phi = segment * 2 * Math.PI / segments;
                    var sinPhi = Math.Sin(phi);
                    var cosPhi = Math.Cos(phi);

                    var x = center.X + radius * sinTheta * cosPhi;
                    var y = center.Y + radius * sinTheta * sinPhi;
                    var z = center.Z + radius * cosTheta;

                    positions.Add(new Point3D(x, y, z));
                }
            }

            for (int ring = 0; ring < rings; ring++)
            {
                for (int segment = 0; segment < segments; segment++)
                {
                    var baseIndex = ring * (segments + 1) + segment;
                    var nextBaseIndex = baseIndex + segments + 1;

                    indices.Add(baseIndex);
                    indices.Add(nextBaseIndex);
                    indices.Add(baseIndex + 1);

                    indices.Add(baseIndex + 1);
                    indices.Add(nextBaseIndex);
                    indices.Add(nextBaseIndex + 1);
                }
            }

            geometry.Positions = new Point3DCollection(positions);
            geometry.TriangleIndices = new Int32Collection(indices);
        }

        public string DisplayName { get; }
    }
}

