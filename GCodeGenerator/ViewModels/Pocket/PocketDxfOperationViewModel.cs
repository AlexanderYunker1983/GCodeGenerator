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
            DisplayName = string.IsNullOrEmpty(title) ? "Импорт DXF карман" : title;

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
                    // Дуги не являются замкнутыми контурами, пропускаем их для карманов
                    // (дуги могут быть частью полилинии, которая будет обработана как LWPOLYLINE/POLYLINE)
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

            // Фильтруем только замкнутые контуры
            var closedContours = new List<DxfPolyline>();
            foreach (var polyline in allPolylines)
            {
                if (IsClosedContour(polyline))
                {
                    closedContours.Add(polyline);
                }
            }

            return closedContours;
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
            // Вычисляем большую и малую полуоси
            double majorRadius = Math.Sqrt(Math.Pow(majorEndX - centerX, 2) + Math.Pow(majorEndY - centerY, 2));
            double minorRadius = majorRadius * ratio;

            // Вычисляем угол поворота большой оси
            double rotationAngle = Math.Atan2(majorEndY - centerY, majorEndX - centerX);

            // Нормализуем параметры
            while (endParam < startParam)
                endParam += 2.0 * Math.PI;

            const int minSegments = 32;
            var paramSpan = endParam - startParam;
            var segments = Math.Max(minSegments, (int)(paramSpan / (Math.PI / 16.0)));

            var points = new List<DxfPoint>();
            for (int i = 0; i <= segments; i++)
            {
                var param = startParam + paramSpan * i / segments;
                // Параметрическое уравнение эллипса
                double x = majorRadius * Math.Cos(param);
                double y = minorRadius * Math.Sin(param);
                
                // Поворачиваем на угол rotationAngle
                double cosRot = Math.Cos(rotationAngle);
                double sinRot = Math.Sin(rotationAngle);
                double rotatedX = x * cosRot - y * sinRot;
                double rotatedY = x * sinRot + y * cosRot;
                
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

