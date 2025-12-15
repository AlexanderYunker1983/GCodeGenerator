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
            var upperLine = line.ToUpperInvariant();
            var axisChar = char.ToUpperInvariant(axis);

            // Find the axis letter, but make sure it's not part of another word
            // (e.g., 'X' in "NEXT" should not match)
            int index = -1;
            for (int i = 0; i < upperLine.Length; i++)
            {
                if (upperLine[i] == axisChar)
                {
                    // Check that previous char is not a letter (to avoid matching in words)
                    if (i == 0 || !char.IsLetter(upperLine[i - 1]))
                    {
                        // Check that next char is a digit, sign, or decimal point
                        if (i + 1 < upperLine.Length)
                        {
                            var nextChar = upperLine[i + 1];
                            if (char.IsDigit(nextChar) || nextChar == '-' || nextChar == '+' || nextChar == '.')
                            {
                                index = i;
                                break;
                            }
                        }
                    }
                }
            }

            if (index < 0) return false;

            var start = index + 1;
            var end = start;

            // Handle optional sign
            if (end < line.Length && (line[end] == '-' || line[end] == '+'))
                end++;

            // Parse digits and decimal point
            bool hasDigit = false;
            while (end < line.Length && (char.IsDigit(line[end]) || line[end] == '.'))
            {
                if (char.IsDigit(line[end]))
                    hasDigit = true;
                end++;
            }

            if (end > start && hasDigit)
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

            // Calculate bounding box to determine model scale (needed for axes thickness)
            var allPointsForBounds = new List<Point3D>();
            foreach (var seg in segments)
            {
                if (seg.InterpolatedPoints != null && seg.InterpolatedPoints.Count > 0)
                    allPointsForBounds.AddRange(seg.InterpolatedPoints);
                else
                {
                    allPointsForBounds.Add(seg.Start);
                    allPointsForBounds.Add(seg.End);
                }
            }

            // Calculate bounding box
            double minX = 0, maxX = 0, minY = 0, maxY = 0, minZ = 0, maxZ = 0;
            double sizeX = 0, sizeY = 0, sizeZ = 0, maxSize = 1.0;
            double rapidThicknessForAxes = 0.05; // Default

            if (allPointsForBounds.Count > 0)
            {
                minX = allPointsForBounds.Min(p => p.X);
                maxX = allPointsForBounds.Max(p => p.X);
                minY = allPointsForBounds.Min(p => p.Y);
                maxY = allPointsForBounds.Max(p => p.Y);
                minZ = allPointsForBounds.Min(p => p.Z);
                maxZ = allPointsForBounds.Max(p => p.Z);

                sizeX = maxX - minX;
                sizeY = maxY - minY;
                sizeZ = maxZ - minZ;
                maxSize = Math.Max(Math.Max(sizeX, sizeY), Math.Max(sizeZ, 1.0));
                var baseThickness = maxSize * 0.008;
                rapidThicknessForAxes = Math.Max(baseThickness * 0.4, 0.05);
            }

            // Always add coordinate axes first (even if no segments)
            AddCoordinateAxes(modelGroup, segments, rapidThicknessForAxes);

            if (segments.Count == 0)
            {
                TrajectoryModel = modelGroup;
                return;
            }

            // Calculate adaptive thickness based on model size
            var rapidThickness = rapidThicknessForAxes;   // Use same thickness as axes
            var workThickness = rapidThickness;   // Same thickness as rapids for work moves
            var arcThickness = rapidThickness;    // Same thickness as rapids for arcs

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

            // OPTIMIZATION: Group segments by material type and batch geometry
            // This dramatically reduces the number of GeometryModel3D objects from thousands to just a few
            var rapidGeometry = new MeshGeometry3D();
            var linearGeometry = new MeshGeometry3D();
            var arcCWGeometry = new MeshGeometry3D();
            var arcCCWGeometry = new MeshGeometry3D();

            // Process all segments and add geometry to appropriate batch
            foreach (var segment in segments)
            {
                Material material;
                double thickness;
                MeshGeometry3D targetGeometry;

                switch (segment.MoveType)
                {
                    case MoveType.Rapid:
                        material = rapidMaterial;
                        thickness = rapidThickness;
                        targetGeometry = rapidGeometry;
                        // Create dashed line segments for rapids
                        AddDashedLineGeometry(targetGeometry, segment.Start, segment.End,
                            thickness, dashLength, gapLength);
                        continue; // Skip normal line creation

                    case MoveType.ArcCW:
                        material = arcCWMaterial;
                        thickness = arcThickness;
                        targetGeometry = arcCWGeometry;
                        break;

                    case MoveType.ArcCCW:
                        material = arcCCWMaterial;
                        thickness = arcThickness;
                        targetGeometry = arcCCWGeometry;
                        break;

                    default: // Linear
                        material = linearMaterial;
                        thickness = workThickness;
                        targetGeometry = linearGeometry;
                        break;
                }

                // For arcs with interpolated points
                if (segment.InterpolatedPoints != null && segment.InterpolatedPoints.Count > 1)
                {
                    for (int i = 0; i < segment.InterpolatedPoints.Count - 1; i++)
                    {
                        AddLineGeometry(targetGeometry, segment.InterpolatedPoints[i],
                            segment.InterpolatedPoints[i + 1], thickness);
                    }
                }
                else
                {
                    // Simple line segment
                    AddLineGeometry(targetGeometry, segment.Start, segment.End, thickness);
                }
            }

            // Create one GeometryModel3D per material type (instead of one per segment)
            if (rapidGeometry.Positions != null && rapidGeometry.Positions.Count > 0)
            {
                var rapidModel = new GeometryModel3D(rapidGeometry, rapidMaterial);
                rapidModel.BackMaterial = rapidMaterial;
                modelGroup.Children.Add(rapidModel);
            }

            if (linearGeometry.Positions != null && linearGeometry.Positions.Count > 0)
            {
                var linearModel = new GeometryModel3D(linearGeometry, linearMaterial);
                linearModel.BackMaterial = linearMaterial;
                modelGroup.Children.Add(linearModel);
            }

            if (arcCWGeometry.Positions != null && arcCWGeometry.Positions.Count > 0)
            {
                var arcCWModel = new GeometryModel3D(arcCWGeometry, arcCWMaterial);
                arcCWModel.BackMaterial = arcCWMaterial;
                modelGroup.Children.Add(arcCWModel);
            }

            if (arcCCWGeometry.Positions != null && arcCCWGeometry.Positions.Count > 0)
            {
                var arcCCWModel = new GeometryModel3D(arcCCWGeometry, arcCCWMaterial);
                arcCCWModel.BackMaterial = arcCCWMaterial;
                modelGroup.Children.Add(arcCCWModel);
            }

            // Add point markers at key positions (2x the rapid line thickness)
            AddPointMarkers(modelGroup, segments, rapidThickness * 2);

            // Add ambient light for better visibility
            modelGroup.Children.Add(new AmbientLight(Color.FromRgb(80, 80, 80)));

            TrajectoryModel = modelGroup;
        }

        private void AddCoordinateAxes(Model3DGroup modelGroup, List<TrajectorySegment> segments, double lineThickness)
        {
            // Calculate axis length based on model size or use default
            double axisLength = 10.0;
            double axisThickness = lineThickness; // Use the same thin thickness as trajectory lines
            double arrowLength = 1.5;
            double arrowRadius = lineThickness * 2; // Thin arrow heads

            if (segments.Count > 0)
            {
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

                if (allPoints.Count > 0)
                {
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

                    axisLength = Math.Max(maxSize * 0.6, 10.0);
                    axisThickness = lineThickness; // Keep thin
                    arrowLength = axisLength * 0.12;
                    arrowRadius = lineThickness * 2; // Thin arrow heads
                }
            }

            var origin = new Point3D(0, 0, 0);

            // X axis - Red
            var xEnd = new Point3D(axisLength, 0, 0);
            var xArrowStart = new Point3D(axisLength - arrowLength, 0, 0);
            var xMaterial = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(200, 0, 0)));
            AddAxisLine(modelGroup, origin, xArrowStart, axisThickness, xMaterial);
            AddArrowHead(modelGroup, xArrowStart, xEnd, arrowRadius, xMaterial);
            AddAxisLabel(modelGroup, new Point3D(axisLength + arrowLength * 0.5, 0, 0), "X", xMaterial, arrowRadius);

            // Y axis - Green
            var yEnd = new Point3D(0, axisLength, 0);
            var yArrowStart = new Point3D(0, axisLength - arrowLength, 0);
            var yMaterial = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(0, 180, 0)));
            AddAxisLine(modelGroup, origin, yArrowStart, axisThickness, yMaterial);
            AddArrowHead(modelGroup, yArrowStart, yEnd, arrowRadius, yMaterial);
            AddAxisLabel(modelGroup, new Point3D(0, axisLength + arrowLength * 0.5, 0), "Y", yMaterial, arrowRadius);

            // Z axis - Blue
            var zEnd = new Point3D(0, 0, axisLength);
            var zArrowStart = new Point3D(0, 0, axisLength - arrowLength);
            var zMaterial = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(0, 80, 220)));
            AddAxisLine(modelGroup, origin, zArrowStart, axisThickness, zMaterial);
            AddArrowHead(modelGroup, zArrowStart, zEnd, arrowRadius, zMaterial);
            AddAxisLabel(modelGroup, new Point3D(0, 0, axisLength + arrowLength * 0.5), "Z", zMaterial, arrowRadius);

            // Origin marker - small white sphere (same size as point markers)
            var originMaterial = new DiffuseMaterial(new SolidColorBrush(Colors.White));
            var originSphere = new MeshGeometry3D();
            CreateSphereGeometry(originSphere, origin, lineThickness * 2);
            var originModel = new GeometryModel3D(originSphere, originMaterial);
            modelGroup.Children.Add(originModel);
        }

        private void AddAxisLine(Model3DGroup modelGroup, Point3D start, Point3D end, double thickness, Material material)
        {
            var lineGeometry = new MeshGeometry3D();
            CreateLineGeometry(lineGeometry, start, end, thickness);
            if (lineGeometry.Positions.Count > 0)
            {
                var lineModel = new GeometryModel3D(lineGeometry, material);
                lineModel.BackMaterial = material;
                modelGroup.Children.Add(lineModel);
            }
        }

        private void AddArrowHead(Model3DGroup modelGroup, Point3D baseCenter, Point3D tip, double radius, Material material)
        {
            // Create a cone for the arrow head
            var coneGeometry = new MeshGeometry3D();
            CreateConeGeometry(coneGeometry, baseCenter, tip, radius);
            if (coneGeometry.Positions.Count > 0)
            {
                var coneModel = new GeometryModel3D(coneGeometry, material);
                coneModel.BackMaterial = material;
                modelGroup.Children.Add(coneModel);
            }
        }

        private void AddAxisLabel(Model3DGroup modelGroup, Point3D position, string label, Material material, double size)
        {
            // Create a simple 3D representation for the axis label
            // Using distinctive shapes: X = cross, Y = fork, Z = zigzag marker
            var labelGeometry = new MeshGeometry3D();

            switch (label)
            {
                case "X":
                    // Create an X shape using two crossed bars
                    CreateXShape(labelGeometry, position, size * 1.5);
                    break;
                case "Y":
                    // Create a Y shape
                    CreateYShape(labelGeometry, position, size * 1.5);
                    break;
                case "Z":
                    // Create a Z shape
                    CreateZShape(labelGeometry, position, size * 1.5);
                    break;
            }

            if (labelGeometry.Positions.Count > 0)
            {
                var labelModel = new GeometryModel3D(labelGeometry, material);
                labelModel.BackMaterial = material;
                modelGroup.Children.Add(labelModel);
            }
        }

        private void CreateXShape(MeshGeometry3D geometry, Point3D center, double size)
        {
            var thickness = size * 0.15; // Thinner lines for labels
            var halfSize = size * 0.5;

            // Two diagonal bars forming X
            var bar1Start = new Point3D(center.X - halfSize, center.Y - halfSize, center.Z);
            var bar1End = new Point3D(center.X + halfSize, center.Y + halfSize, center.Z);
            var bar2Start = new Point3D(center.X - halfSize, center.Y + halfSize, center.Z);
            var bar2End = new Point3D(center.X + halfSize, center.Y - halfSize, center.Z);

            var tempGeom1 = new MeshGeometry3D();
            var tempGeom2 = new MeshGeometry3D();
            CreateLineGeometry(tempGeom1, bar1Start, bar1End, thickness);
            CreateLineGeometry(tempGeom2, bar2Start, bar2End, thickness);

            MergeGeometry(geometry, tempGeom1);
            MergeGeometry(geometry, tempGeom2);
        }

        private void CreateYShape(MeshGeometry3D geometry, Point3D center, double size)
        {
            var thickness = size * 0.15; // Thinner lines for labels
            var halfSize = size * 0.5;

            // Y shape: two upper arms meeting at center, one stem going down
            var armLeft = new Point3D(center.X - halfSize * 0.7, center.Y + halfSize, center.Z);
            var armRight = new Point3D(center.X + halfSize * 0.7, center.Y + halfSize, center.Z);
            var middle = new Point3D(center.X, center.Y, center.Z);
            var bottom = new Point3D(center.X, center.Y - halfSize, center.Z);

            var tempGeom1 = new MeshGeometry3D();
            var tempGeom2 = new MeshGeometry3D();
            var tempGeom3 = new MeshGeometry3D();
            CreateLineGeometry(tempGeom1, armLeft, middle, thickness);
            CreateLineGeometry(tempGeom2, armRight, middle, thickness);
            CreateLineGeometry(tempGeom3, middle, bottom, thickness);

            MergeGeometry(geometry, tempGeom1);
            MergeGeometry(geometry, tempGeom2);
            MergeGeometry(geometry, tempGeom3);
        }

        private void CreateZShape(MeshGeometry3D geometry, Point3D center, double size)
        {
            var thickness = size * 0.15; // Thinner lines for labels
            var halfSize = size * 0.5;

            // Z shape: top horizontal, diagonal, bottom horizontal
            var topLeft = new Point3D(center.X - halfSize * 0.6, center.Y + halfSize * 0.5, center.Z);
            var topRight = new Point3D(center.X + halfSize * 0.6, center.Y + halfSize * 0.5, center.Z);
            var bottomLeft = new Point3D(center.X - halfSize * 0.6, center.Y - halfSize * 0.5, center.Z);
            var bottomRight = new Point3D(center.X + halfSize * 0.6, center.Y - halfSize * 0.5, center.Z);

            var tempGeom1 = new MeshGeometry3D();
            var tempGeom2 = new MeshGeometry3D();
            var tempGeom3 = new MeshGeometry3D();
            CreateLineGeometry(tempGeom1, topLeft, topRight, thickness);
            CreateLineGeometry(tempGeom2, topRight, bottomLeft, thickness);
            CreateLineGeometry(tempGeom3, bottomLeft, bottomRight, thickness);

            MergeGeometry(geometry, tempGeom1);
            MergeGeometry(geometry, tempGeom2);
            MergeGeometry(geometry, tempGeom3);
        }

        private void MergeGeometry(MeshGeometry3D target, MeshGeometry3D source)
        {
            if (source.Positions == null || source.Positions.Count == 0)
                return;

            var baseIndex = target.Positions?.Count ?? 0;

            if (target.Positions == null)
                target.Positions = new Point3DCollection();
            if (target.TriangleIndices == null)
                target.TriangleIndices = new Int32Collection();
            if (target.Normals == null)
                target.Normals = new Vector3DCollection();

            foreach (var pos in source.Positions)
                target.Positions.Add(pos);

            if (source.Normals != null)
            {
                foreach (var norm in source.Normals)
                    target.Normals.Add(norm);
            }

            if (source.TriangleIndices != null)
            {
                foreach (var idx in source.TriangleIndices)
                    target.TriangleIndices.Add(idx + baseIndex);
            }
        }

        private void CreateConeGeometry(MeshGeometry3D geometry, Point3D baseCenter, Point3D tip, double radius)
        {
            var positions = new List<Point3D>();
            var indices = new List<int>();
            var normals = new List<Vector3D>();

            // Direction from base to tip
            var direction = new Vector3D(tip.X - baseCenter.X, tip.Y - baseCenter.Y, tip.Z - baseCenter.Z);
            var length = direction.Length;
            if (length < 0.0001) return;
            direction.Normalize();

            // Find perpendicular vectors
            Vector3D perp1;
            var absX = Math.Abs(direction.X);
            var absY = Math.Abs(direction.Y);
            var absZ = Math.Abs(direction.Z);

            if (absX <= absY && absX <= absZ)
                perp1 = Vector3D.CrossProduct(direction, new Vector3D(1, 0, 0));
            else if (absY <= absX && absY <= absZ)
                perp1 = Vector3D.CrossProduct(direction, new Vector3D(0, 1, 0));
            else
                perp1 = Vector3D.CrossProduct(direction, new Vector3D(0, 0, 1));

            perp1.Normalize();
            var perp2 = Vector3D.CrossProduct(direction, perp1);
            perp2.Normalize();

            int segments = 16;

            // Add tip vertex
            positions.Add(tip);
            normals.Add(direction);

            // Add base circle vertices
            for (int i = 0; i <= segments; i++)
            {
                var angle = i * 2 * Math.PI / segments;
                var cos = Math.Cos(angle);
                var sin = Math.Sin(angle);

                var offset = perp1 * (radius * cos) + perp2 * (radius * sin);
                var point = baseCenter + offset;
                positions.Add(point);

                // Calculate normal for cone surface
                var toPoint = point - baseCenter;
                var sideNormal = Vector3D.CrossProduct(Vector3D.CrossProduct(direction, toPoint), direction - toPoint);
                sideNormal.Normalize();
                normals.Add(sideNormal);
            }

            // Add base center
            var baseCenterIndex = positions.Count;
            positions.Add(baseCenter);
            normals.Add(-direction);

            // Create cone side triangles
            for (int i = 1; i <= segments; i++)
            {
                indices.Add(0);        // tip
                indices.Add(i);        // current base vertex
                indices.Add(i + 1);    // next base vertex
            }

            // Create base cap triangles
            for (int i = 1; i <= segments; i++)
            {
                indices.Add(baseCenterIndex);
                indices.Add(i + 1);
                indices.Add(i);
            }

            geometry.Positions = new Point3DCollection(positions);
            geometry.TriangleIndices = new Int32Collection(indices);
            geometry.Normals = new Vector3DCollection(normals);
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

        /// <summary>
        /// Adds line geometry to an existing MeshGeometry3D (for batching optimization)
        /// </summary>
        private void AddLineGeometry(MeshGeometry3D targetGeometry, Point3D start, Point3D end, double thickness)
        {
            // Initialize collections if needed
            if (targetGeometry.Positions == null)
                targetGeometry.Positions = new Point3DCollection();
            if (targetGeometry.TriangleIndices == null)
                targetGeometry.TriangleIndices = new Int32Collection();
            if (targetGeometry.Normals == null)
                targetGeometry.Normals = new Vector3DCollection();

            var baseIndex = targetGeometry.Positions.Count;

            var direction = new Vector3D(end.X - start.X, end.Y - start.Y, end.Z - start.Z);
            var length = direction.Length;

            if (length < 0.0001) return;

            // Normalize direction
            direction /= length;

            // Find a perpendicular vector using a robust method
            Vector3D perp1;
            var absX = Math.Abs(direction.X);
            var absY = Math.Abs(direction.Y);
            var absZ = Math.Abs(direction.Z);

            if (absX <= absY && absX <= absZ)
            {
                perp1 = Vector3D.CrossProduct(direction, new Vector3D(1, 0, 0));
            }
            else if (absY <= absX && absY <= absZ)
            {
                perp1 = Vector3D.CrossProduct(direction, new Vector3D(0, 1, 0));
            }
            else
            {
                perp1 = Vector3D.CrossProduct(direction, new Vector3D(0, 0, 1));
            }

            // Safety check
            if (perp1.Length < 0.0001)
            {
                perp1 = Vector3D.CrossProduct(direction, new Vector3D(1, 0, 0));
                if (perp1.Length < 0.0001)
                {
                    perp1 = Vector3D.CrossProduct(direction, new Vector3D(0, 1, 0));
                    if (perp1.Length < 0.0001)
                        return; // Give up - degenerate case
                }
            }

            perp1.Normalize();
            var perp2 = Vector3D.CrossProduct(direction, perp1);
            perp2.Normalize();

            // Scale by half thickness
            var halfThickness = thickness * 0.5;
            perp1 *= halfThickness;
            perp2 *= halfThickness;

            // Create 8 vertices of the box
            var v0 = start + perp1 + perp2;
            var v1 = start + perp1 - perp2;
            var v2 = start - perp1 - perp2;
            var v3 = start - perp1 + perp2;
            var v4 = end + perp1 + perp2;
            var v5 = end + perp1 - perp2;
            var v6 = end - perp1 - perp2;
            var v7 = end - perp1 + perp2;

            // Add positions
            targetGeometry.Positions.Add(v0);
            targetGeometry.Positions.Add(v1);
            targetGeometry.Positions.Add(v2);
            targetGeometry.Positions.Add(v3);
            targetGeometry.Positions.Add(v4);
            targetGeometry.Positions.Add(v5);
            targetGeometry.Positions.Add(v6);
            targetGeometry.Positions.Add(v7);

            // Add triangle indices (offset by baseIndex)
            var indices = new List<int>
            {
                // Start cap (facing -direction)
                baseIndex + 0, baseIndex + 2, baseIndex + 1,
                baseIndex + 0, baseIndex + 3, baseIndex + 2,
                // End cap (facing +direction)
                baseIndex + 4, baseIndex + 5, baseIndex + 6,
                baseIndex + 4, baseIndex + 6, baseIndex + 7,
                // Side faces
                baseIndex + 0, baseIndex + 4, baseIndex + 7,  // Top
                baseIndex + 0, baseIndex + 7, baseIndex + 3,
                baseIndex + 1, baseIndex + 2, baseIndex + 6,  // Bottom
                baseIndex + 1, baseIndex + 6, baseIndex + 5,
                baseIndex + 0, baseIndex + 1, baseIndex + 5,  // Front
                baseIndex + 0, baseIndex + 5, baseIndex + 4,
                baseIndex + 2, baseIndex + 3, baseIndex + 7,  // Back
                baseIndex + 2, baseIndex + 7, baseIndex + 6
            };

            foreach (var idx in indices)
                targetGeometry.TriangleIndices.Add(idx);

            // Calculate and add normals for the new vertices
            var positions = new List<Point3D> { v0, v1, v2, v3, v4, v5, v6, v7 };
            var localTriangleIndices = new List<int>
            {
                // Start cap
                0, 2, 1,  0, 3, 2,
                // End cap
                4, 5, 6,  4, 6, 7,
                // Side faces
                0, 4, 7,  0, 7, 3,
                1, 2, 6,  1, 6, 5,
                0, 1, 5,  0, 5, 4,
                2, 3, 7,  2, 7, 6
            };
            var normals = CalculateNormals(positions, localTriangleIndices);
            foreach (var normal in normals)
                targetGeometry.Normals.Add(normal);
        }

        /// <summary>
        /// Adds dashed line segments to an existing MeshGeometry3D (for batching optimization)
        /// </summary>
        private void AddDashedLineGeometry(MeshGeometry3D targetGeometry, Point3D start, Point3D end,
            double thickness, double dashLength, double gapLength)
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

                    AddLineGeometry(targetGeometry, segStart, segEnd, thickness);
                }

                currentDistance += segmentLength;
                isDash = !isDash;
            }
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