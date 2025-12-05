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
    /// <summary>
    /// Types of G-code moves for visualization
    /// </summary>
    public enum MoveType
    {
        Rapid,      // G0 - rapid positioning (no cutting)
        Linear,     // G1 - linear interpolation (cutting)
        ArcCW,      // G2 - circular interpolation clockwise
        ArcCCW      // G3 - circular interpolation counter-clockwise
    }

    /// <summary>
    /// Represents a single movement segment for visualization
    /// </summary>
    public class TrajectorySegment
    {
        public Point3D Start { get; set; }
        public Point3D End { get; set; }
        public MoveType MoveType { get; set; }

        // For arcs (G2/G3)
        public Point3D? ArcCenter { get; set; }
        public double ArcRadius { get; set; }
        public List<Point3D> InterpolatedPoints { get; set; }
    }

    public class PreviewViewModel : CloseableViewModel, IHasDisplayName
    {
        private readonly ILocalizationManager _localizationManager;
        private string _gCodeText;
        private Model3DGroup _trajectoryModel;

        // Visual settings
        private const double RapidDashLength = 2.0;      // Length of dash for rapid moves
        private const double RapidGapLength = 1.5;       // Length of gap between dashes
        private const int ArcInterpolationSegments = 32; // Segments per arc for smooth curves

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
            var segments = new List<TrajectorySegment>();
            var currentPos = new Point3D(0, 0, 0);

            // Modal state - G-codes persist until changed
            var currentMoveType = MoveType.Rapid; // Default to rapid
            var currentPlane = "G17"; // XY plane default for arcs

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("(") || trimmed.StartsWith(";"))
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

                // Skip program end commands
                if (codeLine.StartsWith("M30", StringComparison.OrdinalIgnoreCase) ||
                    codeLine.StartsWith("M2", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Parse plane selection (for arcs)
                if (ContainsGCode(codeLine, "G17")) currentPlane = "G17"; // XY
                if (ContainsGCode(codeLine, "G18")) currentPlane = "G18"; // XZ
                if (ContainsGCode(codeLine, "G19")) currentPlane = "G19"; // YZ

                // Parse G-codes - check for move type changes
                // Order matters: check G00/G01 before G0/G1 to avoid partial matches
                var newMoveType = ParseMoveType(codeLine);
                if (newMoveType.HasValue)
                {
                    currentMoveType = newMoveType.Value;
                }

                // Parse coordinates
                var x = ParseCoordinate(codeLine, 'X', currentPos.X);
                var y = ParseCoordinate(codeLine, 'Y', currentPos.Y);
                var z = ParseCoordinate(codeLine, 'Z', currentPos.Z);

                // Parse arc parameters (I, J, K for center offset, R for radius)
                var hasI = TryParseCoordinate(codeLine, 'I', out var i);
                var hasJ = TryParseCoordinate(codeLine, 'J', out var j);
                var hasK = TryParseCoordinate(codeLine, 'K', out var k);
                var hasR = TryParseCoordinate(codeLine, 'R', out var r);

                var newPos = new Point3D(x, y, z);

                // Check if position changed
                if (Math.Abs(newPos.X - currentPos.X) > 0.0001 ||
                    Math.Abs(newPos.Y - currentPos.Y) > 0.0001 ||
                    Math.Abs(newPos.Z - currentPos.Z) > 0.0001)
                {
                    var segment = new TrajectorySegment
                    {
                        Start = currentPos,
                        End = newPos,
                        MoveType = currentMoveType
                    };

                    // Handle arcs
                    if ((currentMoveType == MoveType.ArcCW || currentMoveType == MoveType.ArcCCW) &&
                        (hasI || hasJ || hasK || hasR))
                    {
                        if (hasR)
                        {
                            // Radius format - calculate center
                            segment.InterpolatedPoints = InterpolateArcByRadius(
                                currentPos, newPos, r, currentMoveType == MoveType.ArcCW, currentPlane);
                        }
                        else
                        {
                            // Center offset format (I, J, K)
                            var center = new Point3D(
                                currentPos.X + (hasI ? i : 0),
                                currentPos.Y + (hasJ ? j : 0),
                                currentPos.Z + (hasK ? k : 0));
                            segment.ArcCenter = center;
                            segment.InterpolatedPoints = InterpolateArcByCenter(
                                currentPos, newPos, center, currentMoveType == MoveType.ArcCW, currentPlane);
                        }
                    }

                    segments.Add(segment);
                    currentPos = newPos;
                }
            }

            BuildModelFromSegments(segments);
        }

        private MoveType? ParseMoveType(string codeLine)
        {
            // Find all G codes in the line and return the last motion command
            MoveType? result = null;
            var upperLine = codeLine.ToUpperInvariant();

            int idx = 0;
            while (idx < upperLine.Length)
            {
                var gIndex = upperLine.IndexOf('G', idx);
                if (gIndex < 0) break;

                // Extract the number after G
                var numStart = gIndex + 1;
                var numEnd = numStart;
                while (numEnd < upperLine.Length && (char.IsDigit(upperLine[numEnd]) || upperLine[numEnd] == '.'))
                {
                    numEnd++;
                }

                if (numEnd > numStart)
                {
                    var numStr = upperLine.Substring(numStart, numEnd - numStart);
                    if (double.TryParse(numStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var gNum))
                    {
                        // Check for motion commands
                        if (Math.Abs(gNum - 0) < 0.001) result = MoveType.Rapid;
                        else if (Math.Abs(gNum - 1) < 0.001) result = MoveType.Linear;
                        else if (Math.Abs(gNum - 2) < 0.001) result = MoveType.ArcCW;
                        else if (Math.Abs(gNum - 3) < 0.001) result = MoveType.ArcCCW;
                    }
                }

                idx = numEnd;
            }

            return result;
        }

        private bool ContainsGCode(string line, string gCode)
        {
            var upper = line.ToUpperInvariant();
            var code = gCode.ToUpperInvariant();
            return upper.Contains(code);
        }

        private double ParseCoordinate(string line, char axis, double defaultValue)
        {
            if (TryParseCoordinate(line, axis, out var value))
                return value;
            return defaultValue;
        }

        private bool TryParseCoordinate(string line, char axis, out double value)
        {
            value = 0;
            var axisStr = axis.ToString();
            var upperLine = line.ToUpperInvariant();
            var index = upperLine.IndexOf(axisStr, StringComparison.OrdinalIgnoreCase);

            if (index < 0) return false;

            var start = index + 1;
            var end = start;

            // Handle optional sign
            if (end < line.Length && (line[end] == '-' || line[end] == '+'))
                end++;

            // Parse digits and decimal point
            while (end < line.Length && (char.IsDigit(line[end]) || line[end] == '.'))
            {
                end++;
            }

            if (end > start)
            {
                var valueStr = line.Substring(start, end - start);
                return double.TryParse(valueStr, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
            }

            return false;
        }

        private List<Point3D> InterpolateArcByCenter(Point3D start, Point3D end, Point3D center,
            bool clockwise, string plane)
        {
            var points = new List<Point3D>();

            // Determine which axes to use based on plane
            double startA, startB, endA, endB, centerA, centerB;
            double startC, endC; // The third axis (linear interpolation)

            switch (plane)
            {
                case "G18": // XZ plane
                    startA = start.X; startB = start.Z; startC = start.Y;
                    endA = end.X; endB = end.Z; endC = end.Y;
                    centerA = center.X; centerB = center.Z;
                    break;
                case "G19": // YZ plane
                    startA = start.Y; startB = start.Z; startC = start.X;
                    endA = end.Y; endB = end.Z; endC = end.X;
                    centerA = center.Y; centerB = center.Z;
                    break;
                default: // G17 - XY plane
                    startA = start.X; startB = start.Y; startC = start.Z;
                    endA = end.X; endB = end.Y; endC = end.Z;
                    centerA = center.X; centerB = center.Y;
                    break;
            }

            // Calculate start and end angles
            var startAngle = Math.Atan2(startB - centerB, startA - centerA);
            var endAngle = Math.Atan2(endB - centerB, endA - centerA);
            var radius = Math.Sqrt(Math.Pow(startA - centerA, 2) + Math.Pow(startB - centerB, 2));

            // Adjust angles for direction
            if (clockwise)
            {
                if (endAngle >= startAngle) endAngle -= 2 * Math.PI;
            }
            else
            {
                if (endAngle <= startAngle) endAngle += 2 * Math.PI;
            }

            var totalAngle = Math.Abs(endAngle - startAngle);
            var segments = Math.Max((int)(totalAngle / (Math.PI / 16)), 4); // At least 4 segments

            for (int i = 0; i <= segments; i++)
            {
                var t = (double)i / segments;
                var angle = startAngle + t * (endAngle - startAngle);
                var a = centerA + radius * Math.Cos(angle);
                var b = centerB + radius * Math.Sin(angle);
                var c = startC + t * (endC - startC); // Linear interpolation for third axis

                Point3D point;
                switch (plane)
                {
                    case "G18": point = new Point3D(a, c, b); break;
                    case "G19": point = new Point3D(c, a, b); break;
                    default: point = new Point3D(a, b, c); break;
                }
                points.Add(point);
            }

            return points;
        }

        private List<Point3D> InterpolateArcByRadius(Point3D start, Point3D end, double radius,
            bool clockwise, string plane)
        {
            // Calculate center from radius
            // This is a simplified version - full implementation would handle
            // the sign of R to determine which of the two possible centers to use

            double startA, startB, endA, endB;
            double startC, endC;

            switch (plane)
            {
                case "G18":
                    startA = start.X; startB = start.Z; startC = start.Y;
                    endA = end.X; endB = end.Z; endC = end.Y;
                    break;
                case "G19":
                    startA = start.Y; startB = start.Z; startC = start.X;
                    endA = end.Y; endB = end.Z; endC = end.X;
                    break;
                default:
                    startA = start.X; startB = start.Y; startC = start.Z;
                    endA = end.X; endB = end.Y; endC = end.Z;
                    break;
            }

            // Midpoint between start and end
            var midA = (startA + endA) / 2;
            var midB = (startB + endB) / 2;

            // Distance from start to end
            var chordLength = Math.Sqrt(Math.Pow(endA - startA, 2) + Math.Pow(endB - startB, 2));

            // Check if radius is valid
            if (Math.Abs(radius) < chordLength / 2)
            {
                // Radius too small, just return linear segment
                return new List<Point3D> { start, end };
            }

            // Distance from midpoint to center
            var h = Math.Sqrt(radius * radius - chordLength * chordLength / 4);

            // Direction perpendicular to chord
            var dx = endA - startA;
            var dy = endB - startB;
            var perpX = -dy / chordLength;
            var perpY = dx / chordLength;

            // Choose center based on direction and sign of radius
            var sign = (clockwise ^ (radius < 0)) ? -1 : 1;
            var centerA = midA + sign * h * perpX;
            var centerB = midB + sign * h * perpY;

            Point3D center;
            switch (plane)
            {
                case "G18": center = new Point3D(centerA, start.Y, centerB); break;
                case "G19": center = new Point3D(start.X, centerA, centerB); break;
                default: center = new Point3D(centerA, centerB, start.Z); break;
            }

            return InterpolateArcByCenter(start, end, center, clockwise, plane);
        }

        private void BuildModelFromSegments(List<TrajectorySegment> segments)
        {
            var modelGroup = new Model3DGroup();

            if (segments.Count == 0)
            {
                TrajectoryModel = modelGroup;
                return;
            }

            // Calculate bounding box to determine model scale
            var allPoints = new List<Point3D>();
            foreach (var seg in segments)
            {
                if (seg.InterpolatedPoints != null && seg.InterpolatedPoints.Count > 0)
                    allPoints.AddRange(seg.InterpolatedPoints);
                else
                {
                    allPoints.Add(seg.Start);
                    allPoints.Add(seg.End);
                }
            }

            var minX = allPoints.Min(p => p.X);
            var maxX = allPoints.Max(p => p.X);
            var minY = allPoints.Min(p => p.Y);
            var maxY = allPoints.Max(p => p.Y);
            var minZ = allPoints.Min(p => p.Z);
            var maxZ = allPoints.Max(p => p.Z);

            var sizeX = maxX - minX;
            var sizeY = maxY - minY;
            var sizeZ = maxZ - minZ;
            var maxSize = Math.Max(Math.Max(sizeX, sizeY), Math.Max(sizeZ, 1.0));

            // Calculate adaptive thickness based on model size
            var baseThickness = maxSize * 0.008;
            var rapidThickness = Math.Max(baseThickness * 0.4, 0.05);   // Thin for rapids
            var workThickness = Math.Max(baseThickness * 1.5, 0.15);   // Thicker for work moves
            var arcThickness = Math.Max(baseThickness * 1.2, 0.12);    // Slightly thinner for arcs

            // Dash parameters scaled to model
            var dashLength = Math.Max(maxSize * 0.03, RapidDashLength);
            var gapLength = Math.Max(maxSize * 0.02, RapidGapLength);

            // Materials
            // Rapid moves: blue, semi-transparent, dashed
            var rapidBrush = new SolidColorBrush(Color.FromArgb(180, 100, 100, 255));
            var rapidMaterial = new DiffuseMaterial(rapidBrush);

            // Linear work moves (G1): red, fully opaque
            var linearBrush = new SolidColorBrush(Color.FromArgb(255, 220, 50, 50));
            var linearMaterial = new MaterialGroup();
            linearMaterial.Children.Add(new DiffuseMaterial(linearBrush));
            linearMaterial.Children.Add(new EmissiveMaterial(new SolidColorBrush(Color.FromArgb(80, 255, 0, 0))));

            // Arc CW (G2): orange
            var arcCWBrush = new SolidColorBrush(Color.FromArgb(255, 255, 140, 0));
            var arcCWMaterial = new MaterialGroup();
            arcCWMaterial.Children.Add(new DiffuseMaterial(arcCWBrush));
            arcCWMaterial.Children.Add(new EmissiveMaterial(new SolidColorBrush(Color.FromArgb(60, 255, 140, 0))));

            // Arc CCW (G3): yellow-green
            var arcCCWBrush = new SolidColorBrush(Color.FromArgb(255, 180, 200, 0));
            var arcCCWMaterial = new MaterialGroup();
            arcCCWMaterial.Children.Add(new DiffuseMaterial(arcCCWBrush));
            arcCCWMaterial.Children.Add(new EmissiveMaterial(new SolidColorBrush(Color.FromArgb(60, 180, 200, 0))));

            // Build geometry for each segment
            foreach (var segment in segments)
            {
                Material material;
                double thickness;

                switch (segment.MoveType)
                {
                    case MoveType.Rapid:
                        material = rapidMaterial;
                        thickness = rapidThickness;
                        // Create dashed line for rapids
                        CreateDashedLineSegments(modelGroup, segment.Start, segment.End,
                            thickness, material, dashLength, gapLength);
                        continue; // Skip normal line creation

                    case MoveType.ArcCW:
                        material = arcCWMaterial;
                        thickness = arcThickness;
                        break;

                    case MoveType.ArcCCW:
                        material = arcCCWMaterial;
                        thickness = arcThickness;
                        break;

                    default: // Linear
                        material = linearMaterial;
                        thickness = workThickness;
                        break;
                }

                // For arcs with interpolated points
                if (segment.InterpolatedPoints != null && segment.InterpolatedPoints.Count > 1)
                {
                    for (int i = 0; i < segment.InterpolatedPoints.Count - 1; i++)
                    {
                        var lineGeometry = new MeshGeometry3D();
                        CreateLineGeometry(lineGeometry, segment.InterpolatedPoints[i],
                            segment.InterpolatedPoints[i + 1], thickness);

                        if (lineGeometry.Positions.Count > 0)
                        {
                            var lineModel = new GeometryModel3D(lineGeometry, material);
                            lineModel.BackMaterial = material; // Visible from both sides
                            modelGroup.Children.Add(lineModel);
                        }
                    }
                }
                else
                {
                    // Simple line segment
                    var lineGeometry = new MeshGeometry3D();
                    CreateLineGeometry(lineGeometry, segment.Start, segment.End, thickness);

                    if (lineGeometry.Positions.Count > 0)
                    {
                        var lineModel = new GeometryModel3D(lineGeometry, material);
                        lineModel.BackMaterial = material;
                        modelGroup.Children.Add(lineModel);
                    }
                }
            }

            // Add point markers at key positions
            AddPointMarkers(modelGroup, segments, Math.Max(baseThickness * 2, 0.2));

            // Add ambient light for better visibility
            modelGroup.Children.Add(new AmbientLight(Color.FromRgb(80, 80, 80)));

            TrajectoryModel = modelGroup;
        }

        private void CreateDashedLineSegments(Model3DGroup modelGroup, Point3D start, Point3D end,
            double thickness, Material material, double dashLength, double gapLength)
        {
            var direction = new Vector3D(end.X - start.X, end.Y - start.Y, end.Z - start.Z);
            var totalLength = direction.Length;

            if (totalLength < 0.0001) return;

            direction.Normalize();
            var cycleLength = dashLength + gapLength;
            var currentDistance = 0.0;
            var isDash = true;

            while (currentDistance < totalLength)
            {
                var segmentLength = isDash ? dashLength : gapLength;
                var remainingLength = totalLength - currentDistance;

                if (segmentLength > remainingLength)
                    segmentLength = remainingLength;

                if (isDash && segmentLength > 0.001)
                {
                    var segStart = new Point3D(
                        start.X + direction.X * currentDistance,
                        start.Y + direction.Y * currentDistance,
                        start.Z + direction.Z * currentDistance);

                    var segEnd = new Point3D(
                        start.X + direction.X * (currentDistance + segmentLength),
                        start.Y + direction.Y * (currentDistance + segmentLength),
                        start.Z + direction.Z * (currentDistance + segmentLength));

                    var lineGeometry = new MeshGeometry3D();
                    CreateLineGeometry(lineGeometry, segStart, segEnd, thickness);

                    if (lineGeometry.Positions.Count > 0)
                    {
                        var lineModel = new GeometryModel3D(lineGeometry, material);
                        lineModel.BackMaterial = material;
                        modelGroup.Children.Add(lineModel);
                    }
                }

                currentDistance += segmentLength;
                isDash = !isDash;
            }
        }

        private void CreateLineGeometry(MeshGeometry3D geometry, Point3D start, Point3D end, double thickness)
        {
            var direction = new Vector3D(end.X - start.X, end.Y - start.Y, end.Z - start.Z);
            var length = direction.Length;

            if (length < 0.0001) return;

            // Normalize direction
            direction /= length;

            // Find a perpendicular vector using a robust method
            // Choose the axis that is least aligned with direction for cross product
            Vector3D perp1;
            var absX = Math.Abs(direction.X);
            var absY = Math.Abs(direction.Y);
            var absZ = Math.Abs(direction.Z);

            if (absX <= absY && absX <= absZ)
            {
                // X component is smallest, cross with X axis
                perp1 = Vector3D.CrossProduct(direction, new Vector3D(1, 0, 0));
            }
            else if (absY <= absX && absY <= absZ)
            {
                // Y component is smallest, cross with Y axis
                perp1 = Vector3D.CrossProduct(direction, new Vector3D(0, 1, 0));
            }
            else
            {
                // Z component is smallest, cross with Z axis
                perp1 = Vector3D.CrossProduct(direction, new Vector3D(0, 0, 1));
            }

            // Safety check - should not happen with above logic
            if (perp1.Length < 0.0001)
            {
                perp1 = Vector3D.CrossProduct(direction, new Vector3D(1, 0, 0));
                if (perp1.Length < 0.0001)
                    perp1 = Vector3D.CrossProduct(direction, new Vector3D(0, 1, 0));
                if (perp1.Length < 0.0001)
                    return; // Give up - degenerate case
            }

            perp1.Normalize();

            // Second perpendicular using cross product
            var perp2 = Vector3D.CrossProduct(direction, perp1);
            perp2.Normalize();

            // Scale by half thickness
            var halfThickness = thickness * 0.5;
            perp1 *= halfThickness;
            perp2 *= halfThickness;

            // Create 8 vertices of the box (rectangular cross-section)
            var v0 = start + perp1 + perp2;
            var v1 = start + perp1 - perp2;
            var v2 = start - perp1 - perp2;
            var v3 = start - perp1 + perp2;
            var v4 = end + perp1 + perp2;
            var v5 = end + perp1 - perp2;
            var v6 = end - perp1 - perp2;
            var v7 = end - perp1 + perp2;

            var positions = new List<Point3D> { v0, v1, v2, v3, v4, v5, v6, v7 };

            // Triangle indices with correct winding order (counter-clockwise when viewed from outside)
            var indices = new List<int>
            {
                // Start cap (facing -direction)
                0, 2, 1,  0, 3, 2,
                // End cap (facing +direction)
                4, 5, 6,  4, 6, 7,
                // Side faces
                0, 4, 7,  0, 7, 3,  // Top
                1, 2, 6,  1, 6, 5,  // Bottom
                0, 1, 5,  0, 5, 4,  // Front
                2, 3, 7,  2, 7, 6   // Back
            };

            geometry.Positions = new Point3DCollection(positions);
            geometry.TriangleIndices = new Int32Collection(indices);
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

                if (normal.Length > 0.0001)
                {
                    normal.Normalize();
                    normals[i0] += normal;
                    normals[i1] += normal;
                    normals[i2] += normal;
                }
            }

            // Normalize all accumulated normals
            for (int i = 0; i < normals.Length; i++)
            {
                if (normals[i].Length > 0.0001)
                    normals[i].Normalize();
                else
                    normals[i] = new Vector3D(0, 0, 1); // Default normal
            }

            return new Vector3DCollection(normals);
        }

        private void AddPointMarkers(Model3DGroup modelGroup, List<TrajectorySegment> segments, double markerSize)
        {
            // Collect unique points - start point and points where move type changes
            var keyPoints = new List<(Point3D point, MoveType moveType)>();

            if (segments.Count > 0)
            {
                // Add start point
                keyPoints.Add((segments[0].Start, MoveType.Rapid));

                // Add points where move type changes or at significant locations
                MoveType lastMoveType = segments[0].MoveType;
                foreach (var seg in segments)
                {
                    if (seg.MoveType != lastMoveType)
                    {
                        keyPoints.Add((seg.Start, seg.MoveType));
                        lastMoveType = seg.MoveType;
                    }
                }

                // Add final point
                var lastSeg = segments[segments.Count - 1];
                keyPoints.Add((lastSeg.End, lastSeg.MoveType));
            }

            // Materials for markers
            var startMaterial = new DiffuseMaterial(new SolidColorBrush(Colors.LimeGreen));
            var endMaterial = new DiffuseMaterial(new SolidColorBrush(Colors.Red));
            var transitionMaterial = new DiffuseMaterial(new SolidColorBrush(Colors.Yellow));

            for (int i = 0; i < keyPoints.Count; i++)
            {
                Material material;
                double size = markerSize;

                if (i == 0)
                {
                    material = startMaterial;
                    size *= 1.5; // Larger start marker
                }
                else if (i == keyPoints.Count - 1)
                {
                    material = endMaterial;
                    size *= 1.3; // Larger end marker
                }
                else
                {
                    material = transitionMaterial;
                    size *= 0.8; // Smaller transition markers
                }

                var sphereGeometry = new MeshGeometry3D();
                CreateSphereGeometry(sphereGeometry, keyPoints[i].point, size);
                var sphereModel = new GeometryModel3D(sphereGeometry, material);
                modelGroup.Children.Add(sphereModel);
            }
        }

        private void CreateSphereGeometry(MeshGeometry3D geometry, Point3D center, double radius)
        {
            var positions = new List<Point3D>();
            var indices = new List<int>();
            var normals = new List<Vector3D>();

            int segments = 12;
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

                    var nx = sinTheta * cosPhi;
                    var ny = sinTheta * sinPhi;
                    var nz = cosTheta;

                    positions.Add(new Point3D(
                        center.X + radius * nx,
                        center.Y + radius * ny,
                        center.Z + radius * nz));

                    normals.Add(new Vector3D(nx, ny, nz));
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
            geometry.Normals = new Vector3DCollection(normals);
        }

        public string DisplayName { get; }
    }
}