using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Input;
using System.Windows.Threading;
using GCodeGenerator.Infrastructure;
using GCodeGenerator.Models;
using MugenMvvmToolkit.Interfaces.Models;
using MugenMvvmToolkit.ViewModels;
using YLocalization;

namespace GCodeGenerator.ViewModels.Pocket
{
    public class PocketDxfOperationViewModel : CloseableViewModel, IHasDisplayName
    {
        private readonly ILocalizationManager _localizationManager;
        private const double ClosedContourTolerance = 0.001; // Точность для определения замкнутости контура

        public PocketDxfOperationViewModel(ILocalizationManager localizationManager)
        {
            _localizationManager = localizationManager;
            ImportDxfCommand = new RelayCommand(ImportDxfFile);
            var title = _localizationManager?.GetString("PocketDxfName");
            DisplayName = string.IsNullOrEmpty(title) ? "Импорт DXF - карманы" : title;

            if (Operation == null)
                Operation = new PocketDxfOperation();
            else
            {
                UpdateOperationData();
            }
        }

        public PocketOperationsViewModel PocketOperationsViewModel { get; set; }

        private PocketDxfOperation _operation;
        public PocketDxfOperation Operation
        {
            get => _operation;
            set
            {
                if (Equals(value, _operation)) return;
                _operation = value;
                UpdateOperationData();
            }
        }

        private string _displayName;
        public string DisplayName
        {
            get => _displayName;
            set
            {
                if (Equals(value, _displayName)) return;
                _displayName = value;
                OnPropertyChanged();
            }
        }

        private string _filePath;
        public string FilePath
        {
            get => _filePath;
            set
            {
                if (Equals(value, _filePath)) return;
                _filePath = value;
                OnPropertyChanged();
            }
        }

        private string _importInfo;
        public string ImportInfo
        {
            get => _importInfo;
            set
            {
                if (Equals(value, _importInfo)) return;
                _importInfo = value;
                OnPropertyChanged();
            }
        }

        public ICommand ImportDxfCommand { get; }

        private MillingDirection _direction = MillingDirection.Clockwise;
        public MillingDirection Direction
        {
            get => _direction;
            set
            {
                if (value == _direction) return;
                _direction = value;
                OnPropertyChanged();
            }
        }

        private PocketStrategy _pocketStrategy = PocketStrategy.Spiral;
        public PocketStrategy PocketStrategy
        {
            get => _pocketStrategy;
            set
            {
                if (value == _pocketStrategy) return;
                _pocketStrategy = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsLinesStrategy));
                OnPropertyChanged(nameof(IsLinesOrZigZagStrategy));
            }
        }

        public bool IsLinesStrategy => PocketStrategy == PocketStrategy.Lines;
        public bool IsLinesOrZigZagStrategy => PocketStrategy == PocketStrategy.Lines || PocketStrategy == PocketStrategy.ZigZag;

        private double _totalDepth = 2.0;
        public double TotalDepth
        {
            get => _totalDepth;
            set
            {
                if (value.Equals(_totalDepth)) return;
                _totalDepth = value;
                OnPropertyChanged();
            }
        }

        private double _stepDepth = 1.0;
        public double StepDepth
        {
            get => _stepDepth;
            set
            {
                if (value.Equals(_stepDepth)) return;
                _stepDepth = value;
                OnPropertyChanged();
            }
        }

        private double _toolDiameter = 3.0;
        public double ToolDiameter
        {
            get => _toolDiameter;
            set
            {
                if (value.Equals(_toolDiameter)) return;
                _toolDiameter = value;
                OnPropertyChanged();
            }
        }

        private double _contourHeight = 0.0;
        public double ContourHeight
        {
            get => _contourHeight;
            set
            {
                if (value.Equals(_contourHeight)) return;
                _contourHeight = value;
                OnPropertyChanged();
            }
        }

        private double _feedXYRapid = 1000.0;
        public double FeedXYRapid
        {
            get => _feedXYRapid;
            set
            {
                if (value.Equals(_feedXYRapid)) return;
                _feedXYRapid = value;
                OnPropertyChanged();
            }
        }

        private double _feedXYWork = 300.0;
        public double FeedXYWork
        {
            get => _feedXYWork;
            set
            {
                if (value.Equals(_feedXYWork)) return;
                _feedXYWork = value;
                OnPropertyChanged();
            }
        }

        private double _feedZRapid = 500.0;
        public double FeedZRapid
        {
            get => _feedZRapid;
            set
            {
                if (value.Equals(_feedZRapid)) return;
                _feedZRapid = value;
                OnPropertyChanged();
            }
        }

        private double _feedZWork = 200.0;
        public double FeedZWork
        {
            get => _feedZWork;
            set
            {
                if (value.Equals(_feedZWork)) return;
                _feedZWork = value;
                OnPropertyChanged();
            }
        }

        private double _safeZHeight = 1.0;
        public double SafeZHeight
        {
            get => _safeZHeight;
            set
            {
                if (value.Equals(_safeZHeight)) return;
                _safeZHeight = value;
                OnPropertyChanged();
            }
        }

        private double _retractHeight = 0.3;
        public double RetractHeight
        {
            get => _retractHeight;
            set
            {
                if (value.Equals(_retractHeight)) return;
                _retractHeight = value;
                OnPropertyChanged();
            }
        }

        private double _stepPercent = 40.0;
        public double StepPercentOfTool
        {
            get => _stepPercent;
            set
            {
                if (value.Equals(_stepPercent)) return;
                _stepPercent = value;
                OnPropertyChanged();
            }
        }

        private int _decimals = 3;
        public int Decimals
        {
            get => _decimals;
            set
            {
                if (value == _decimals) return;
                _decimals = value;
                OnPropertyChanged();
            }
        }

        private double _lineAngleDeg = 0.0;
        public double LineAngleDeg
        {
            get => _lineAngleDeg;
            set
            {
                if (value.Equals(_lineAngleDeg)) return;
                _lineAngleDeg = value;
                OnPropertyChanged();
            }
        }

        private double _wallTaperAngleDeg = 0.0;
        public double WallTaperAngleDeg
        {
            get => _wallTaperAngleDeg;
            set
            {
                // Ограничиваем угол диапазоном [0; 90)
                var v = Math.Max(0, Math.Min(89.999999, value));
                if (v.Equals(_wallTaperAngleDeg)) return;
                _wallTaperAngleDeg = v;
                OnPropertyChanged();
            }
        }

        private bool _isRoughingEnabled;
        public bool IsRoughingEnabled
        {
            get => _isRoughingEnabled;
            set
            {
                if (value == _isRoughingEnabled) return;
                _isRoughingEnabled = value;
                if (_isRoughingEnabled)
                {
                    _isFinishingEnabled = false;
                    OnPropertyChanged(nameof(IsFinishingEnabled));
                }
                OnPropertyChanged();
            }
        }

        private bool _isFinishingEnabled;
        public bool IsFinishingEnabled
        {
            get => _isFinishingEnabled;
            set
            {
                if (value == _isFinishingEnabled) return;
                _isFinishingEnabled = value;
                if (_isFinishingEnabled)
                {
                    _isRoughingEnabled = false;
                    OnPropertyChanged(nameof(IsRoughingEnabled));
                }
                OnPropertyChanged();
            }
        }

        private double _finishAllowance;
        public double FinishAllowance
        {
            get => _finishAllowance;
            set
            {
                if (value.Equals(_finishAllowance)) return;
                _finishAllowance = value;
                OnPropertyChanged();
            }
        }

        private PocketFinishingMode _finishingMode = PocketFinishingMode.All;
        public PocketFinishingMode FinishingMode
        {
            get => _finishingMode;
            set
            {
                if (value == _finishingMode) return;
                _finishingMode = value;
                OnPropertyChanged();
            }
        }


        private void UpdateOperationData()
        {
            if (Operation == null)
                return;

            FilePath = Operation.DxfFilePath;

            if (Operation.ClosedContours != null && Operation.ClosedContours.Count > 0)
            {
                var contourCount = Operation.ClosedContours.Count;
                var infoTemplate = _localizationManager?.GetString("DxfImportContoursInfo") ?? "Импортировано замкнутых контуров: {0}";
                ImportInfo = string.Format(infoTemplate, contourCount);
            }
            else
            {
                ImportInfo = null;
            }

            Direction = Operation.Direction;
            PocketStrategy = Operation.PocketStrategy;
            TotalDepth = Operation.TotalDepth;
            StepDepth = Operation.StepDepth;
            ToolDiameter = Operation.ToolDiameter;
            ContourHeight = Operation.ContourHeight;
            FeedXYRapid = Operation.FeedXYRapid;
            FeedXYWork = Operation.FeedXYWork;
            FeedZRapid = Operation.FeedZRapid;
            FeedZWork = Operation.FeedZWork;
            SafeZHeight = Operation.SafeZHeight;
            RetractHeight = Operation.RetractHeight;
            StepPercentOfTool = Operation.StepPercentOfTool;
            Decimals = Operation.Decimals;
            LineAngleDeg = Operation.LineAngleDeg;
            WallTaperAngleDeg = Math.Max(0, Operation.WallTaperAngleDeg);
            IsRoughingEnabled = Operation.IsRoughingEnabled;
            IsFinishingEnabled = Operation.IsFinishingEnabled;
            FinishAllowance = Operation.FinishAllowance;
            FinishingMode = Operation.FinishingMode;
        }

        private void ImportDxfFile()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "DXF files (*.dxf)|*.dxf|All files (*.*)|*.*",
                DefaultExt = "dxf",
                Title = _localizationManager?.GetString("DxfImportDialogTitle") ?? "Импорт DXF"
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                var closedContours = ParseDxfClosedContours(dialog.FileName);
                if (closedContours.Count == 0)
                {
                    var msg = _localizationManager?.GetString("DxfImportNoClosedContours") ?? "В файле не найдено замкнутых контуров для импорта.";
                    System.Windows.MessageBox.Show(msg, dialog.Title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    return;
                }

                Operation.ClosedContours = closedContours;
                Operation.DxfFilePath = dialog.FileName;
                FilePath = dialog.FileName;
                var contourCount = closedContours.Count;
                var infoTemplate = _localizationManager?.GetString("DxfImportContoursInfo") ?? "Импортировано замкнутых контуров: {0}";
                ImportInfo = string.Format(infoTemplate, contourCount);
                PocketOperationsViewModel?.MainViewModel?.NotifyOperationsChanged();
            }
            catch (Exception ex)
            {
                var msg = _localizationManager?.GetString("DxfImportFailed") ?? "Ошибка импорта DXF:";
                System.Windows.MessageBox.Show($"{msg} {ex.Message}", dialog.Title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private List<DxfPolyline> ParseDxfClosedContours(string path)
        {
            var allPolylines = new List<DxfPolyline>();
            var lines = File.ReadAllLines(path);
            int i = 0;

            double Parse(string v)
            {
                if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                    return d;
                return 0;
            }

            // Парсим все полилинии из DXF
            while (i < lines.Length)
            {
                var code = lines[i].Trim();
                i++;

                if (string.Equals(code, "LINE", StringComparison.OrdinalIgnoreCase))
                {
                    double? x1 = null, y1 = null, x2 = null, y2 = null;
                    while (i + 1 < lines.Length)
                    {
                        var groupCode = lines[i].Trim();
                        var value = lines[i + 1].Trim();
                        i += 2;

                        switch (groupCode)
                        {
                            case "10": x1 = Parse(value); break;
                            case "20": y1 = Parse(value); break;
                            case "11": x2 = Parse(value); break;
                            case "21": y2 = Parse(value); break;
                            case "39": break; // Thickness - игнорируем
                            case "0": i -= 2; goto EndLine;
                        }
                    }
                EndLine:
                    if (x1.HasValue && y1.HasValue && x2.HasValue && y2.HasValue)
                    {
                        allPolylines.Add(new DxfPolyline
                        {
                            Points = new List<DxfPoint>
                            {
                                new DxfPoint { X = x1.Value, Y = y1.Value },
                                new DxfPoint { X = x2.Value, Y = y2.Value }
                            }
                        });
                    }
                    continue;
                }
                else if (string.Equals(code, "CIRCLE", StringComparison.OrdinalIgnoreCase))
                {
                    double? cx = null, cy = null, radius = null;
                    while (i + 1 < lines.Length)
                    {
                        var groupCode = lines[i].Trim();
                        var value = lines[i + 1].Trim();
                        i += 2;

                        switch (groupCode)
                        {
                            case "10": cx = Parse(value); break;
                            case "20": cy = Parse(value); break;
                            case "40": radius = Parse(value); break;
                            case "0": i -= 2; goto EndCircle;
                        }
                    }
                EndCircle:
                    if (cx.HasValue && cy.HasValue && radius.HasValue && radius.Value > 0)
                    {
                        var circlePoints = ApproximateCircle(cx.Value, cy.Value, radius.Value);
                        allPolylines.Add(new DxfPolyline { Points = circlePoints });
                    }
                    continue;
                }
                else if (string.Equals(code, "ARC", StringComparison.OrdinalIgnoreCase))
                {
                    double? cx = null, cy = null, radius = null, startAngle = null, endAngle = null;
                    while (i + 1 < lines.Length)
                    {
                        var groupCode = lines[i].Trim();
                        var value = lines[i + 1].Trim();
                        i += 2;

                        switch (groupCode)
                        {
                            case "10": cx = Parse(value); break;
                            case "20": cy = Parse(value); break;
                            case "40": radius = Parse(value); break;
                            case "50": startAngle = Parse(value); break;
                            case "51": endAngle = Parse(value); break;
                            case "0": i -= 2; goto EndArc;
                        }
                    }
                EndArc:
                    // Дуги могут быть частью замкнутого контура из нескольких сегментов
                    // Добавляем их как сегменты для последующего соединения
                    if (cx.HasValue && cy.HasValue && radius.HasValue && radius.Value > 0 && 
                        startAngle.HasValue && endAngle.HasValue)
                    {
                        var arcPoints = ApproximateArc(cx.Value, cy.Value, radius.Value, 
                            startAngle.Value, endAngle.Value);
                        allPolylines.Add(new DxfPolyline { Points = arcPoints });
                    }
                    continue;
                }
                else if (string.Equals(code, "ELLIPSE", StringComparison.OrdinalIgnoreCase))
                {
                    double? centerX = null, centerY = null;
                    double? majorEndX = null, majorEndY = null;
                    double? ratio = null;
                    double? startParam = null, endParam = null;
                    while (i + 1 < lines.Length)
                    {
                        var groupCode = lines[i].Trim();
                        var value = lines[i + 1].Trim();
                        i += 2;

                        switch (groupCode)
                        {
                            case "10": centerX = Parse(value); break;
                            case "20": centerY = Parse(value); break;
                            case "11": majorEndX = Parse(value); break;
                            case "21": majorEndY = Parse(value); break;
                            case "40": ratio = Parse(value); break;
                            case "41": startParam = Parse(value); break;
                            case "42": endParam = Parse(value); break;
                            case "0": i -= 2; goto EndEllipse;
                        }
                    }
                EndEllipse:
                    if (centerX.HasValue && centerY.HasValue && majorEndX.HasValue && majorEndY.HasValue && 
                        ratio.HasValue && ratio.Value > 0 && startParam.HasValue && endParam.HasValue)
                    {
                        var ellipsePoints = ApproximateEllipse(centerX.Value, centerY.Value,
                            majorEndX.Value, majorEndY.Value, ratio.Value,
                            startParam.Value, endParam.Value);
                        allPolylines.Add(new DxfPolyline { Points = ellipsePoints });
                    }
                    continue;
                }
                else if (string.Equals(code, "LWPOLYLINE", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(code, "POLYLINE", StringComparison.OrdinalIgnoreCase))
                {
                    var polylinePoints = new List<DxfPoint>();
                    bool isClosed = false;
                    while (i + 1 < lines.Length)
                    {
                        var groupCode = lines[i].Trim();
                        var value = lines[i + 1].Trim();
                        i += 2;

                        switch (groupCode)
                        {
                            case "70": // Flags
                                isClosed = (int.Parse(value) & 1) != 0; // Bit 0 = closed
                                break;
                            case "10": // X coordinate
                                var x = Parse(value);
                                var y = 0.0;
                                if (i < lines.Length && lines[i].Trim() == "20")
                                {
                                    i++;
                                    if (i < lines.Length)
                                        y = Parse(lines[i].Trim());
                                }
                                polylinePoints.Add(new DxfPoint { X = x, Y = y });
                                break;
                            case "0":
                                if (string.Equals(value, "VERTEX", StringComparison.OrdinalIgnoreCase))
                                {
                                    // Читаем вершину
                                    double? vx = null, vy = null;
                                    while (i + 1 < lines.Length)
                                    {
                                        var vGroupCode = lines[i].Trim();
                                        var vValue = lines[i + 1].Trim();
                                        i += 2;

                                        switch (vGroupCode)
                                        {
                                            case "10": vx = Parse(vValue); break;
                                            case "20": vy = Parse(vValue); break;
                                            case "0":
                                                i -= 2;
                                                goto EndVertex;
                                        }
                                    }
                                EndVertex:
                                    if (vx.HasValue && vy.HasValue)
                                        polylinePoints.Add(new DxfPoint { X = vx.Value, Y = vy.Value });
                                }
                                else
                                {
                                    i -= 2;
                                    goto EndPolyline;
                                }
                                break;
                        }
                    }
                EndPolyline:
                    if (polylinePoints.Count >= 3)
                    {
                        // Если полилиния помечена как замкнутая, добавляем первую точку в конец
                        if (isClosed && polylinePoints.Count > 0)
                        {
                            var firstPoint = polylinePoints[0];
                            var lastPoint = polylinePoints[polylinePoints.Count - 1];
                            if (Math.Abs(firstPoint.X - lastPoint.X) > ClosedContourTolerance ||
                                Math.Abs(firstPoint.Y - lastPoint.Y) > ClosedContourTolerance)
                            {
                                polylinePoints.Add(new DxfPoint { X = firstPoint.X, Y = firstPoint.Y });
                            }
                        }
                        allPolylines.Add(new DxfPolyline { Points = polylinePoints });
                    }
                    continue;
                }
            }

            // Теперь пытаемся соединить отдельные линии и дуги в замкнутые контуры
            var connectedContours = ConnectSegmentsIntoContours(allPolylines);
            
            // Ищем замкнутые области, образованные пересекающимися линиями
            var intersectionContours = FindClosedAreasFromIntersections(allPolylines);
            
            // Фильтруем только замкнутые контуры
            var closedContours = new List<DxfPolyline>();
            foreach (var polyline in allPolylines)
            {
                if (IsClosedContour(polyline))
                {
                    closedContours.Add(polyline);
                }
            }
            
            // Добавляем соединенные контуры
            foreach (var contour in connectedContours)
            {
                if (IsClosedContour(contour))
                {
                    closedContours.Add(contour);
                }
            }
            
            // Добавляем контуры из пересечений
            foreach (var contour in intersectionContours)
            {
                if (IsClosedContour(contour))
                {
                    // Проверяем, нет ли уже такого контура
                    bool isDuplicate = false;
                    foreach (var existing in closedContours)
                    {
                        if (AreContoursSimilar(contour, existing))
                        {
                            isDuplicate = true;
                            break;
                        }
                    }
                    if (!isDuplicate)
                    {
                        closedContours.Add(contour);
                    }
                }
            }

            return closedContours;
        }

        private List<DxfPolyline> ConnectSegmentsIntoContours(List<DxfPolyline> segments)
        {
            var contours = new List<DxfPolyline>();
            var used = new bool[segments.Count];
            
            for (int i = 0; i < segments.Count; i++)
            {
                if (used[i] || segments[i].Points == null || segments[i].Points.Count < 2)
                    continue;
                
                // Пытаемся построить контур, начиная с этого сегмента
                var contour = BuildContourFromSegment(segments, i, used);
                if (contour != null && contour.Points != null && contour.Points.Count >= 3)
                {
                    contours.Add(contour);
                }
            }
            
            return contours;
        }

        private DxfPolyline BuildContourFromSegment(List<DxfPolyline> segments, int startIdx, bool[] used)
        {
            var contourPoints = new List<DxfPoint>();
            var currentSegmentIdx = startIdx;
            var startPoint = segments[startIdx].Points[0];
            var currentPoint = segments[startIdx].Points[segments[startIdx].Points.Count - 1];
            
            // Добавляем точки первого сегмента
            foreach (var p in segments[startIdx].Points)
            {
                contourPoints.Add(new DxfPoint { X = p.X, Y = p.Y });
            }
            used[startIdx] = true;
            
            // Ищем следующий сегмент, который начинается там, где заканчивается текущий
            while (true)
            {
                int nextSegmentIdx = -1;
                bool reverseNext = false;
                
                for (int i = 0; i < segments.Count; i++)
                {
                    if (used[i] || segments[i].Points == null || segments[i].Points.Count < 2)
                        continue;
                    
                    var seg = segments[i];
                    var segStart = seg.Points[0];
                    var segEnd = seg.Points[seg.Points.Count - 1];
                    
                    // Проверяем, совпадает ли начало или конец сегмента с текущей точкой
                    if (PointsMatch(currentPoint, segStart))
                    {
                        nextSegmentIdx = i;
                        reverseNext = false;
                        break;
                    }
                    else if (PointsMatch(currentPoint, segEnd))
                    {
                        nextSegmentIdx = i;
                        reverseNext = true;
                        break;
                    }
                }
                
                if (nextSegmentIdx < 0)
                    break; // Не нашли следующий сегмент
                
                // Добавляем точки следующего сегмента
                var nextSeg = segments[nextSegmentIdx];
                if (reverseNext)
                {
                    // Добавляем точки в обратном порядке
                    for (int j = nextSeg.Points.Count - 2; j >= 0; j--) // Пропускаем последнюю точку (она уже есть)
                    {
                        contourPoints.Add(new DxfPoint { X = nextSeg.Points[j].X, Y = nextSeg.Points[j].Y });
                    }
                    currentPoint = nextSeg.Points[0];
                }
                else
                {
                    // Добавляем точки в прямом порядке
                    for (int j = 1; j < nextSeg.Points.Count; j++) // Пропускаем первую точку (она уже есть)
                    {
                        contourPoints.Add(new DxfPoint { X = nextSeg.Points[j].X, Y = nextSeg.Points[j].Y });
                    }
                    currentPoint = nextSeg.Points[nextSeg.Points.Count - 1];
                }
                
                used[nextSegmentIdx] = true;
                currentSegmentIdx = nextSegmentIdx;
                
                // Проверяем, замкнулся ли контур
                if (PointsMatch(currentPoint, startPoint))
                {
                    break; // Контур замкнут
                }
            }
            
            return new DxfPolyline { Points = contourPoints };
        }

        private bool PointsMatch(DxfPoint p1, DxfPoint p2)
        {
            if (p1 == null || p2 == null)
                return false;
            double dx = p1.X - p2.X;
            double dy = p1.Y - p2.Y;
            double distance = Math.Sqrt(dx * dx + dy * dy);
            return distance <= ClosedContourTolerance;
        }

        private List<DxfPolyline> FindClosedAreasFromIntersections(List<DxfPolyline> segments)
        {
            var contours = new List<DxfPolyline>();
            
            if (segments == null || segments.Count == 0)
                return contours;
            
            // Находим все точки пересечения и разбиваем сегменты
            var splitSegments = SplitSegmentsAtIntersections(segments);
            
            if (splitSegments == null || splitSegments.Count == 0)
                return contours;
            
            // Строим граф соединений на основе точек (вершин), а не сегментов
            var pointGraph = BuildPointGraph(splitSegments);
            
            if (pointGraph == null || pointGraph.Count == 0)
                return contours;
            
            // Ищем все циклы в графе точек
            var cycles = FindCyclesInPointGraph(pointGraph);
            
            // Фильтруем циклы - оставляем только те, которые образуют замкнутые области
            foreach (var cycle in cycles)
            {
                if (cycle != null && cycle.Count >= 3)
                {
                    var contour = BuildContourFromPointCycle(cycle);
                    if (contour != null && IsClosedContour(contour))
                    {
                        // Проверяем, что контур имеет достаточную площадь (не является вырожденным)
                        var area = GetContourArea(contour);
                        if (area > ClosedContourTolerance * ClosedContourTolerance)
                        {
                            contours.Add(contour);
                        }
                    }
                }
            }
            
            return contours;
        }
        
        private double GetContourArea(DxfPolyline contour)
        {
            if (contour?.Points == null || contour.Points.Count < 3)
                return 0;
            
            double area = 0;
            for (int i = 0; i < contour.Points.Count; i++)
            {
                var p1 = contour.Points[i];
                var p2 = contour.Points[(i + 1) % contour.Points.Count];
                area += p1.X * p2.Y - p2.X * p1.Y;
            }
            return Math.Abs(area / 2.0);
        }

        private List<DxfPolyline> SplitSegmentsAtIntersections(List<DxfPolyline> segments)
        {
            var splitSegments = new List<DxfPolyline>();
            var intersectionPoints = new Dictionary<int, List<(DxfPoint point, double distance)>>(); // Индекс сегмента -> список точек пересечения с расстоянием от начала
            
            // Находим все пересечения и добавляем точки пересечения к обоим сегментам
            for (int i = 0; i < segments.Count; i++)
            {
                var seg1 = segments[i];
                if (seg1.Points == null || seg1.Points.Count < 2)
                    continue;
                
                if (!intersectionPoints.ContainsKey(i))
                    intersectionPoints[i] = new List<(DxfPoint point, double distance)>();
                
                for (int j = i + 1; j < segments.Count; j++)
                {
                    var seg2 = segments[j];
                    if (seg2.Points == null || seg2.Points.Count < 2)
                        continue;
                    
                    // Находим пересечения между сегментами
                    var pts = FindSegmentIntersections(seg1, seg2);
                    foreach (var pt in pts)
                    {
                        // Вычисляем расстояние от начала сегмента 1 до точки пересечения
                        double dist1 = 0;
                        for (int k = 0; k < seg1.Points.Count - 1; k++)
                        {
                            var p1 = seg1.Points[k];
                            var p2 = seg1.Points[k + 1];
                            var segDist = Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));
                            var distToInter = DistanceToSegment(pt.X, pt.Y, p1.X, p1.Y, p2.X, p2.Y);
                            if (distToInter < ClosedContourTolerance)
                            {
                                dist1 += Math.Sqrt(Math.Pow(pt.X - p1.X, 2) + Math.Pow(pt.Y - p1.Y, 2));
                                break;
                            }
                            dist1 += segDist;
                        }
                        
                        // Вычисляем расстояние от начала сегмента 2 до точки пересечения
                        double dist2 = 0;
                        for (int k = 0; k < seg2.Points.Count - 1; k++)
                        {
                            var p1 = seg2.Points[k];
                            var p2 = seg2.Points[k + 1];
                            var segDist = Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));
                            var distToInter = DistanceToSegment(pt.X, pt.Y, p1.X, p1.Y, p2.X, p2.Y);
                            if (distToInter < ClosedContourTolerance)
                            {
                                dist2 += Math.Sqrt(Math.Pow(pt.X - p1.X, 2) + Math.Pow(pt.Y - p1.Y, 2));
                                break;
                            }
                            dist2 += segDist;
                        }
                        
                        // Добавляем точку пересечения к обоим сегментам
                        if (!intersectionPoints[i].Any(p => PointsMatch(p.point, pt)))
                            intersectionPoints[i].Add((pt, dist1));
                        
                        if (!intersectionPoints.ContainsKey(j))
                            intersectionPoints[j] = new List<(DxfPoint point, double distance)>();
                        if (!intersectionPoints[j].Any(p => PointsMatch(p.point, pt)))
                            intersectionPoints[j].Add((pt, dist2));
                    }
                }
            }
            
            // Сортируем точки пересечения по расстоянию для каждого сегмента
            foreach (var kvp in intersectionPoints)
            {
                kvp.Value.Sort((a, b) => a.distance.CompareTo(b.distance));
            }
            
            // Разбиваем сегменты в точках пересечения
            for (int i = 0; i < segments.Count; i++)
            {
                var seg = segments[i];
                if (seg.Points == null || seg.Points.Count < 2)
                    continue;
                
                if (intersectionPoints.ContainsKey(i))
                {
                    var points = new List<DxfPoint>(seg.Points);
                    var intersections = intersectionPoints[i];
                    
                    // Добавляем точки пересечения в правильном порядке (уже отсортированы по расстоянию)
                    foreach (var inter in intersections)
                    {
                        // Находим позицию для вставки точки пересечения
                        int insertPos = -1;
                        double minDist = double.MaxValue;
                        
                        for (int j = 0; j < points.Count - 1; j++)
                        {
                            var p1 = points[j];
                            var p2 = points[j + 1];
                            var dist = DistanceToSegment(inter.point.X, inter.point.Y, p1.X, p1.Y, p2.X, p2.Y);
                            if (dist < minDist && dist < ClosedContourTolerance * 10) // Увеличиваем допуск для поиска
                            {
                                // Проверяем, что точка действительно на отрезке между p1 и p2
                                var dx = p2.X - p1.X;
                                var dy = p2.Y - p1.Y;
                                var segLen = Math.Sqrt(dx * dx + dy * dy);
                                if (segLen > 1e-9)
                                {
                                    var t = ((inter.point.X - p1.X) * dx + (inter.point.Y - p1.Y) * dy) / (segLen * segLen);
                                    if (t >= -0.01 && t <= 1.01) // Небольшой допуск для границ
                                    {
                                        minDist = dist;
                                        insertPos = j + 1;
                                    }
                                }
                            }
                        }
                        
                        if (insertPos >= 0)
                        {
                            // Проверяем, нет ли уже такой точки рядом
                            bool pointExists = false;
                            for (int j = Math.Max(0, insertPos - 1); j < Math.Min(points.Count, insertPos + 2); j++)
                            {
                                if (PointsMatch(points[j], inter.point))
                                {
                                    pointExists = true;
                                    break;
                                }
                            }
                            
                            if (!pointExists)
                            {
                                points.Insert(insertPos, inter.point);
                            }
                        }
                    }
                    
                    // Разбиваем на подсегменты
                    for (int j = 0; j < points.Count - 1; j++)
                    {
                        splitSegments.Add(new DxfPolyline
                        {
                            Points = new List<DxfPoint> { points[j], points[j + 1] }
                        });
                    }
                }
                else
                {
                    // Сегмент без пересечений - добавляем как есть
                    splitSegments.Add(seg);
                }
            }
            
            return splitSegments;
        }

        private List<DxfPoint> FindSegmentIntersections(DxfPolyline seg1, DxfPolyline seg2)
        {
            var intersections = new List<DxfPoint>();
            
            if (seg1.Points == null || seg1.Points.Count < 2 || seg2.Points == null || seg2.Points.Count < 2)
                return intersections;
            
            // Проверяем пересечения между всеми парами отрезков
            for (int i = 0; i < seg1.Points.Count - 1; i++)
            {
                var p1 = seg1.Points[i];
                var p2 = seg1.Points[i + 1];
                
                for (int j = 0; j < seg2.Points.Count - 1; j++)
                {
                    var p3 = seg2.Points[j];
                    var p4 = seg2.Points[j + 1];
                    
                    var intersection = FindLineSegmentIntersection(p1.X, p1.Y, p2.X, p2.Y, p3.X, p3.Y, p4.X, p4.Y);
                    if (intersection != null)
                    {
                        if (!intersections.Any(p => PointsMatch(p, intersection)))
                            intersections.Add(intersection);
                    }
                }
            }
            
            return intersections;
        }

        private DxfPoint FindLineSegmentIntersection(double x1, double y1, double x2, double y2,
            double x3, double y3, double x4, double y4)
        {
            double dx1 = x2 - x1;
            double dy1 = y2 - y1;
            double dx2 = x4 - x3;
            double dy2 = y4 - y3;
            
            double denom = dx1 * dy2 - dy1 * dx2;
            if (Math.Abs(denom) < 1e-9)
                return null; // Параллельные линии
            
            double t1 = ((x3 - x1) * dy2 - (y3 - y1) * dx2) / denom;
            double t2 = ((x3 - x1) * dy1 - (y3 - y1) * dx1) / denom;
            
            // Используем небольшой допуск для границ отрезков
            const double tolerance = 1e-6;
            if (t1 >= -tolerance && t1 <= 1.0 + tolerance && t2 >= -tolerance && t2 <= 1.0 + tolerance)
            {
                // Ограничиваем параметры диапазоном [0, 1]
                t1 = Math.Max(0, Math.Min(1, t1));
                return new DxfPoint
                {
                    X = x1 + t1 * dx1,
                    Y = y1 + t1 * dy1
                };
            }
            
            return null;
        }

        private double DistanceToSegment(double px, double py, double x1, double y1, double x2, double y2)
        {
            double dx = x2 - x1;
            double dy = y2 - y1;
            if (Math.Abs(dx) < 1e-9 && Math.Abs(dy) < 1e-9)
                return Math.Sqrt(Math.Pow(px - x1, 2) + Math.Pow(py - y1, 2));
            
            double t = ((px - x1) * dx + (py - y1) * dy) / (dx * dx + dy * dy);
            t = Math.Max(0, Math.Min(1, t));
            double projX = x1 + t * dx;
            double projY = y1 + t * dy;
            return Math.Sqrt(Math.Pow(px - projX, 2) + Math.Pow(py - projY, 2));
        }

        private Dictionary<int, List<int>> BuildConnectionGraph(List<DxfPolyline> segments)
        {
            var graph = new Dictionary<int, List<int>>();
            
            for (int i = 0; i < segments.Count; i++)
            {
                if (!graph.ContainsKey(i))
                    graph[i] = new List<int>();
                var seg1 = segments[i];
                if (seg1.Points == null || seg1.Points.Count < 2)
                    continue;
                
                var seg1Start = seg1.Points[0];
                var seg1End = seg1.Points[seg1.Points.Count - 1];
                
                for (int j = 0; j < segments.Count; j++)
                {
                    if (i == j)
                        continue;
                    
                    var seg2 = segments[j];
                    if (seg2.Points == null || seg2.Points.Count < 2)
                        continue;
                    
                    var seg2Start = seg2.Points[0];
                    var seg2End = seg2.Points[seg2.Points.Count - 1];
                    
                    // Проверяем соединения по концам сегментов
                    bool connected = false;
                    if (PointsMatch(seg1End, seg2Start) || PointsMatch(seg1End, seg2End) ||
                        PointsMatch(seg1Start, seg2Start) || PointsMatch(seg1Start, seg2End))
                    {
                        connected = true;
                    }
                    // Также проверяем, что сегменты имеют общую точку (включая точки внутри сегментов)
                    else if (PointsMatch(seg1Start, seg2Start) || PointsMatch(seg1Start, seg2End) ||
                             PointsMatch(seg1End, seg2Start) || PointsMatch(seg1End, seg2End))
                    {
                        connected = true;
                    }
                    
                    if (connected)
                    {
                        if (!graph[i].Contains(j))
                            graph[i].Add(j);
                        if (!graph.ContainsKey(j))
                            graph[j] = new List<int>();
                        if (!graph[j].Contains(i))
                            graph[j].Add(i);
                    }
                }
            }
            
            return graph;
        }

        private List<List<int>> FindCyclesInGraph(Dictionary<int, List<int>> graph, List<DxfPolyline> segments)
        {
            var cycles = new List<List<int>>();
            var foundCycles = new HashSet<string>(); // Для фильтрации дубликатов
            
            foreach (var node in graph.Keys)
            {
                FindCyclesFromNode(node, node, graph, segments, new List<int>(), cycles, foundCycles);
            }
            
            return cycles;
        }

        private void FindCyclesFromNode(int startNode, int currentNode, Dictionary<int, List<int>> graph,
            List<DxfPolyline> segments, List<int> path, List<List<int>> cycles, HashSet<string> foundCycles)
        {
            if (path.Count > 0 && currentNode == startNode && path.Count >= 3)
            {
                // Найден цикл - проверяем, не дубликат ли это
                var cycleKey = string.Join(",", path.OrderBy(x => x));
                if (!foundCycles.Contains(cycleKey))
                {
                    foundCycles.Add(cycleKey);
                    cycles.Add(new List<int>(path));
                }
                return;
            }
            
            if (path.Contains(currentNode) && currentNode != startNode)
                return; // Уже были в этой вершине (кроме начальной)
            
            path.Add(currentNode);
            
            if (graph.ContainsKey(currentNode))
            {
                foreach (var neighbor in graph[currentNode])
                {
                    if (neighbor == startNode && path.Count >= 3)
                    {
                        // Замыкаем цикл
                        var cycleKey = string.Join(",", path.OrderBy(x => x));
                        if (!foundCycles.Contains(cycleKey))
                        {
                            foundCycles.Add(cycleKey);
                            cycles.Add(new List<int>(path));
                        }
                    }
                    else if (!path.Contains(neighbor))
                    {
                        FindCyclesFromNode(startNode, neighbor, graph, segments, path, cycles, foundCycles);
                    }
                }
            }
            
            path.RemoveAt(path.Count - 1);
        }

        private DxfPolyline BuildContourFromCycle(List<int> cycle, List<DxfPolyline> segments)
        {
            var contourPoints = new List<DxfPoint>();
            
            for (int i = 0; i < cycle.Count; i++)
            {
                var segIdx = cycle[i];
                var nextSegIdx = cycle[(i + 1) % cycle.Count];
                
                var seg = segments[segIdx];
                if (seg.Points == null || seg.Points.Count < 2)
                    continue;
                
                var nextSeg = segments[nextSegIdx];
                if (nextSeg.Points == null || nextSeg.Points.Count < 2)
                    continue;
                
                // Определяем направление соединения
                var segStart = seg.Points[0];
                var segEnd = seg.Points[seg.Points.Count - 1];
                var nextSegStart = nextSeg.Points[0];
                var nextSegEnd = nextSeg.Points[nextSeg.Points.Count - 1];
                
                if (i == 0)
                {
                    // Первый сегмент - добавляем все точки
                    foreach (var p in seg.Points)
                    {
                        if (contourPoints.Count == 0 || !PointsMatch(contourPoints[contourPoints.Count - 1], p))
                            contourPoints.Add(new DxfPoint { X = p.X, Y = p.Y });
                    }
                }
                else
                {
                    // Последующие сегменты - добавляем только новые точки
                    if (PointsMatch(segEnd, nextSegStart))
                    {
                        for (int j = 1; j < seg.Points.Count; j++)
                        {
                            var p = seg.Points[j];
                            if (!PointsMatch(contourPoints[contourPoints.Count - 1], p))
                                contourPoints.Add(new DxfPoint { X = p.X, Y = p.Y });
                        }
                    }
                    else if (PointsMatch(segEnd, nextSegEnd))
                    {
                        for (int j = seg.Points.Count - 2; j >= 0; j--)
                        {
                            var p = seg.Points[j];
                            if (!PointsMatch(contourPoints[contourPoints.Count - 1], p))
                                contourPoints.Add(new DxfPoint { X = p.X, Y = p.Y });
                        }
                    }
                }
            }
            
            return new DxfPolyline { Points = contourPoints };
        }

        private Dictionary<DxfPoint, List<DxfPoint>> BuildPointGraph(List<DxfPolyline> segments)
        {
            var graph = new Dictionary<DxfPoint, List<DxfPoint>>();
            
            // Для каждого сегмента добавляем соединения между его концами
            foreach (var seg in segments)
            {
                if (seg.Points == null || seg.Points.Count < 2)
                    continue;
                
                var start = seg.Points[0];
                var end = seg.Points[seg.Points.Count - 1];
                
                // Находим или создаем ключи для точек
                DxfPoint startKey = FindOrAddPoint(graph, start);
                DxfPoint endKey = FindOrAddPoint(graph, end);
                
                // Добавляем соединение (двунаправленное)
                if (!graph[startKey].Any(p => PointsMatch(p, endKey)))
                    graph[startKey].Add(endKey);
                if (!graph[endKey].Any(p => PointsMatch(p, startKey)))
                    graph[endKey].Add(startKey);
            }
            
            return graph;
        }

        private DxfPoint FindOrAddPoint(Dictionary<DxfPoint, List<DxfPoint>> graph, DxfPoint point)
        {
            // Ищем существующую точку в графе
            foreach (var key in graph.Keys)
            {
                if (PointsMatch(key, point))
                    return key;
            }
            
            // Если не нашли, добавляем новую точку
            graph[point] = new List<DxfPoint>();
            return point;
        }

        private List<List<DxfPoint>> FindCyclesInPointGraph(Dictionary<DxfPoint, List<DxfPoint>> graph)
        {
            var cycles = new List<List<DxfPoint>>();
            var foundCycles = new HashSet<string>();
            
            // Ограничиваем глубину поиска, чтобы избежать бесконечных циклов
            const int maxCycleLength = 100;
            
            // Пробуем начать поиск с каждой точки в графе
            foreach (var startPoint in graph.Keys)
            {
                if (graph[startPoint] == null || graph[startPoint].Count < 2)
                    continue; // Пропускаем точки с менее чем 2 соседями (не могут быть частью цикла)
                
                // Начинаем поиск с каждого соседа начальной точки
                foreach (var firstNeighbor in graph[startPoint])
                {
                    var path = new List<DxfPoint> { startPoint };
                    FindCyclesFromPoint(startPoint, firstNeighbor, graph, path, cycles, foundCycles, maxCycleLength);
                }
            }
            
            return cycles;
        }

        private void FindCyclesFromPoint(DxfPoint startPoint, DxfPoint currentPoint, 
            Dictionary<DxfPoint, List<DxfPoint>> graph, List<DxfPoint> path, 
            List<List<DxfPoint>> cycles, HashSet<string> foundCycles, int maxLength)
        {
            // Ограничиваем длину пути
            if (path.Count >= maxLength)
                return;
            
            // Если мы вернулись в начальную точку и прошли минимум 3 точки - это цикл
            if (path.Count > 0 && PointsMatch(currentPoint, startPoint) && path.Count >= 3)
            {
                // Найден цикл - проверяем, не дубликат ли это
                var sortedPath = path.OrderBy(p => p.X).ThenBy(p => p.Y).ToList();
                var cycleKey = string.Join("|", sortedPath.Select(p => $"{p.X:F6},{p.Y:F6}"));
                if (!foundCycles.Contains(cycleKey))
                {
                    foundCycles.Add(cycleKey);
                    cycles.Add(new List<DxfPoint>(path));
                }
                return;
            }
            
            // Проверяем, не были ли мы уже в этой точке (кроме начальной)
            for (int i = 0; i < path.Count - 1; i++)
            {
                if (PointsMatch(path[i], currentPoint))
                {
                    return; // Уже были в этой точке
                }
            }
            
            path.Add(currentPoint);
            
            if (graph.ContainsKey(currentPoint))
            {
                foreach (var neighbor in graph[currentPoint])
                {
                    // Если сосед - это начальная точка и мы прошли минимум 2 точки - замыкаем цикл
                    if (PointsMatch(neighbor, startPoint))
                    {
                        if (path.Count >= 3)
                        {
                            var sortedPath = path.OrderBy(p => p.X).ThenBy(p => p.Y).ToList();
                            var cycleKey = string.Join("|", sortedPath.Select(p => $"{p.X:F6},{p.Y:F6}"));
                            if (!foundCycles.Contains(cycleKey))
                            {
                                foundCycles.Add(cycleKey);
                                cycles.Add(new List<DxfPoint>(path));
                            }
                        }
                    }
                    else
                    {
                        // Проверяем, не были ли мы уже в этой соседней точке
                        bool alreadyVisited = false;
                        for (int i = 0; i < path.Count; i++)
                        {
                            if (PointsMatch(path[i], neighbor))
                            {
                                alreadyVisited = true;
                                break;
                            }
                        }
                        
                        if (!alreadyVisited)
                        {
                            FindCyclesFromPoint(startPoint, neighbor, graph, path, cycles, foundCycles, maxLength);
                        }
                    }
                }
            }
            
            path.RemoveAt(path.Count - 1);
        }

        private DxfPolyline BuildContourFromPointCycle(List<DxfPoint> cycle)
        {
            // Строим контур из цикла точек
            var contourPoints = new List<DxfPoint>();
            foreach (var point in cycle)
            {
                if (contourPoints.Count == 0 || !PointsMatch(contourPoints[contourPoints.Count - 1], point))
                {
                    contourPoints.Add(new DxfPoint { X = point.X, Y = point.Y });
                }
            }
            
            // Замыкаем контур
            if (contourPoints.Count > 0 && !PointsMatch(contourPoints[0], contourPoints[contourPoints.Count - 1]))
            {
                contourPoints.Add(new DxfPoint { X = contourPoints[0].X, Y = contourPoints[0].Y });
            }
            
            return new DxfPolyline { Points = contourPoints };
        }

        private bool AreContoursSimilar(DxfPolyline c1, DxfPolyline c2)
        {
            if (c1?.Points == null || c2?.Points == null)
                return false;
            
            if (Math.Abs(c1.Points.Count - c2.Points.Count) > 2)
                return false;
            
            // Проверяем, совпадают ли точки контуров (с учетом возможного сдвига начала)
            int matches = 0;
            for (int offset = 0; offset < c1.Points.Count; offset++)
            {
                int matchCount = 0;
                for (int i = 0; i < c1.Points.Count && i < c2.Points.Count; i++)
                {
                    int idx1 = (i + offset) % c1.Points.Count;
                    int idx2 = i % c2.Points.Count;
                    if (PointsMatch(c1.Points[idx1], c2.Points[idx2]))
                        matchCount++;
                }
                if (matchCount >= Math.Min(c1.Points.Count, c2.Points.Count) - 1)
                    return true;
            }
            
            return false;
        }

        private bool IsClosedContour(DxfPolyline polyline)
        {
            if (polyline?.Points == null || polyline.Points.Count < 3)
                return false;

            var first = polyline.Points[0];
            var last = polyline.Points[polyline.Points.Count - 1];
            var dx = first.X - last.X;
            var dy = first.Y - last.Y;
            var distance = Math.Sqrt(dx * dx + dy * dy);
            return distance <= ClosedContourTolerance;
        }

        private List<DxfPoint> ApproximateCircle(double centerX, double centerY, double radius)
        {
            const int segments = 32;
            var points = new List<DxfPoint>();
            for (int i = 0; i <= segments; i++)
            {
                var angle = 2.0 * Math.PI * i / segments;
                points.Add(new DxfPoint
                {
                    X = centerX + radius * Math.Cos(angle),
                    Y = centerY + radius * Math.Sin(angle)
                });
            }
            return points;
        }

        private List<DxfPoint> ApproximateArc(double centerX, double centerY, double radius,
            double startAngleDeg, double endAngleDeg)
        {
            const int minSegments = 8;
            var startAngle = startAngleDeg * Math.PI / 180.0;
            var endAngle = endAngleDeg * Math.PI / 180.0;

            while (endAngle < startAngle)
                endAngle += 2.0 * Math.PI;

            var angleSpan = endAngle - startAngle;
            var segments = Math.Max(minSegments, (int)(angleSpan / (Math.PI / 16.0)));

            var points = new List<DxfPoint>();
            for (int i = 0; i <= segments; i++)
            {
                var angle = startAngle + angleSpan * i / segments;
                points.Add(new DxfPoint
                {
                    X = centerX + radius * Math.Cos(angle),
                    Y = centerY + radius * Math.Sin(angle)
                });
            }
            return points;
        }

        private List<DxfPoint> ApproximateEllipse(double centerX, double centerY,
            double majorEndX, double majorEndY, double ratio,
            double startParam, double endParam)
        {
            // В DXF: (11, 21) - это конечная точка большой оси ОТНОСИТЕЛЬНО ЦЕНТРА (вектор от центра)
            // Это стандарт для DXF ELLIPSE - координаты задаются относительно центра
            // Используем (11, 21) напрямую как вектор от центра
            double majorRadius = Math.Sqrt(majorEndX * majorEndX + majorEndY * majorEndY);
            
            // Проверяем, что радиус не нулевой
            if (majorRadius < 1e-9)
                return new List<DxfPoint>();
            
            // Малая полуось = большая полуось * соотношение
            double minorRadius = majorRadius * ratio;

            // Вычисляем угол поворота большой оси (направление вектора)
            double rotationAngle = Math.Atan2(majorEndY, majorEndX);

            // Нормализуем параметры (в DXF параметры заданы в радианах)
            double normalizedStartParam = startParam;
            double normalizedEndParam = endParam;
            while (normalizedEndParam < normalizedStartParam)
                normalizedEndParam += 2.0 * Math.PI;

            const int minSegments = 32;
            var paramSpan = normalizedEndParam - normalizedStartParam;
            var segments = Math.Max(minSegments, (int)(paramSpan / (Math.PI / 16.0)));

            var points = new List<DxfPoint>();
            double cosRot = Math.Cos(rotationAngle);
            double sinRot = Math.Sin(rotationAngle);
            
            for (int i = 0; i <= segments; i++)
            {
                var param = normalizedStartParam + paramSpan * i / segments;
                // Параметрическое уравнение эллипса в локальной системе координат
                // где большая ось направлена по оси X, малая по оси Y
                // x = a * cos(t), y = b * sin(t), где a = majorRadius, b = minorRadius
                double xLocal = majorRadius * Math.Cos(param);
                double yLocal = minorRadius * Math.Sin(param);
                
                // Поворачиваем на угол rotationAngle (чтобы совместить локальную ось X с направлением большой оси)
                // и переносим в центр
                double rotatedX = xLocal * cosRot - yLocal * sinRot;
                double rotatedY = xLocal * sinRot + yLocal * cosRot;
                
                points.Add(new DxfPoint
                {
                    X = centerX + rotatedX,
                    Y = centerY + rotatedY
                });
            }
            return points;
        }

        protected override void OnClosed(IDataContext context)
        {
            base.OnClosed(context);
            if (_operation == null) return;

            if (ToolDiameter <= 0 || StepPercentOfTool <= 0 || _operation.ClosedContours == null || _operation.ClosedContours.Count == 0)
            {
                RemoveOperationFromMain();
                return;
            }

            _operation.Direction = Direction;
            _operation.PocketStrategy = PocketStrategy;
            _operation.TotalDepth = TotalDepth;
            _operation.StepDepth = StepDepth;
            _operation.ToolDiameter = ToolDiameter;
            _operation.ContourHeight = ContourHeight;
            _operation.FeedXYRapid = FeedXYRapid;
            _operation.FeedXYWork = FeedXYWork;
            _operation.FeedZRapid = FeedZRapid;
            _operation.FeedZWork = FeedZWork;
            _operation.SafeZHeight = SafeZHeight;
            _operation.RetractHeight = RetractHeight;
            _operation.StepPercentOfTool = StepPercentOfTool;
            _operation.Decimals = Decimals;
            _operation.LineAngleDeg = LineAngleDeg;
            _operation.WallTaperAngleDeg = WallTaperAngleDeg;
            _operation.IsRoughingEnabled = IsRoughingEnabled;
            _operation.IsFinishingEnabled = IsFinishingEnabled;
            _operation.FinishAllowance = FinishAllowance;
            _operation.FinishingMode = FinishingMode;

            if (_operation.Metadata == null)
                _operation.Metadata = new Dictionary<string, object>();

            _operation.Metadata["Direction"] = Direction;
            _operation.Metadata["PocketStrategy"] = PocketStrategy;
            _operation.Metadata["TotalDepth"] = TotalDepth;
            _operation.Metadata["StepDepth"] = StepDepth;
            _operation.Metadata["ToolDiameter"] = ToolDiameter;
            _operation.Metadata["ContourHeight"] = ContourHeight;
            _operation.Metadata["FeedXYRapid"] = FeedXYRapid;
            _operation.Metadata["FeedXYWork"] = FeedXYWork;
            _operation.Metadata["FeedZRapid"] = FeedZRapid;
            _operation.Metadata["FeedZWork"] = FeedZWork;
            _operation.Metadata["SafeZHeight"] = SafeZHeight;
            _operation.Metadata["RetractHeight"] = RetractHeight;
            _operation.Metadata["StepPercentOfTool"] = StepPercentOfTool;
            _operation.Metadata["Decimals"] = Decimals;
            _operation.Metadata["LineAngleDeg"] = LineAngleDeg;
            _operation.Metadata["WallTaperAngleDeg"] = WallTaperAngleDeg;
            _operation.Metadata["IsRoughingEnabled"] = IsRoughingEnabled;
            _operation.Metadata["IsFinishingEnabled"] = IsFinishingEnabled;
            _operation.Metadata["FinishAllowance"] = FinishAllowance;
            _operation.Metadata["FinishingMode"] = FinishingMode;

            PocketOperationsViewModel?.MainViewModel?.NotifyOperationsChanged();
        }

        private void RemoveOperationFromMain()
        {
            if (PocketOperationsViewModel != null)
            {
                var dispatcher = System.Windows.Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
                if (dispatcher.CheckAccess())
                {
                    PocketOperationsViewModel.RemoveOperation(_operation);
                }
                else
                {
                    dispatcher.Invoke(() => PocketOperationsViewModel.RemoveOperation(_operation));
                }
            }
        }
    }
}

