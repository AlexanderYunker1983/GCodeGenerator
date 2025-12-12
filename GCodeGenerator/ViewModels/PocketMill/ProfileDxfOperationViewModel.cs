using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Input;
using GCodeGenerator.Infrastructure;
using GCodeGenerator.Models;
using MugenMvvmToolkit.Interfaces.Models;
using MugenMvvmToolkit.ViewModels;
using YLocalization;

namespace GCodeGenerator.ViewModels.PocketMill
{
    public class ProfileDxfOperationViewModel : CloseableViewModel, IHasDisplayName
    {
        private readonly ILocalizationManager _localizationManager;
        private ProfileDxfOperation _operation;
        public ProfileDxfOperation Operation 
        { 
            get => _operation;
            set 
            {
                if (Equals(value, _operation)) return;
                _operation = value;
                UpdateOperationData();
            }
        }
        public ProfileMillingOperationsViewModel ProfileMillingOperationsViewModel { get; set; }

        public ICommand ImportDxfCommand { get; }

        public ProfileDxfOperationViewModel()
            : this(null)
        {
        }

        public ProfileDxfOperationViewModel(ILocalizationManager localizationManager)
        {
            _localizationManager = localizationManager;
            ImportDxfCommand = new RelayCommand(ImportDxfFile);

            var title = _localizationManager?.GetString("ProfileDxfName");
            DisplayName = string.IsNullOrEmpty(title) ? "Импорт DXF" : title;

            if (Operation == null)
                Operation = new ProfileDxfOperation();
            else
            {
                UpdateOperationData();
            }
        }

        private void UpdateOperationData()
        {
            if (Operation == null)
                return;

            // Загружаем сохраненный путь к файлу
            FilePath = Operation.DxfFilePath;
            
            // Показываем информацию об импорте, если данные уже загружены
            if (Operation.Polylines != null && Operation.Polylines.Count > 0)
            {
                var lineCount = Operation.Polylines.Sum(p => p?.Points?.Count > 1 ? p.Points.Count - 1 : 0);
                var infoTemplate = _localizationManager?.GetString("DxfImportInfo") ?? "Импортировано линий: {0}";
                ImportInfo = string.Format(infoTemplate, lineCount);
            }
            else
            {
                ImportInfo = null;
            }
            
            // Уведомляем об изменении всех свойств, которые зависят от Operation
            OnPropertyChanged(nameof(TotalDepth));
            OnPropertyChanged(nameof(StepDepth));
            OnPropertyChanged(nameof(ToolDiameter));
            OnPropertyChanged(nameof(ContourHeight));
            OnPropertyChanged(nameof(FeedXYRapid));
            OnPropertyChanged(nameof(FeedXYWork));
            OnPropertyChanged(nameof(FeedZRapid));
            OnPropertyChanged(nameof(FeedZWork));
            OnPropertyChanged(nameof(SafeZHeight));
            OnPropertyChanged(nameof(RetractHeight));
            OnPropertyChanged(nameof(Decimals));
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

        public double TotalDepth
        {
            get => Operation.TotalDepth;
            set { if (value.Equals(Operation.TotalDepth)) return; Operation.TotalDepth = value; OnPropertyChanged(); }
        }

        public double StepDepth
        {
            get => Operation.StepDepth;
            set { if (value.Equals(Operation.StepDepth)) return; Operation.StepDepth = value; OnPropertyChanged(); }
        }

        public double ToolDiameter
        {
            get => Operation.ToolDiameter;
            set { if (value.Equals(Operation.ToolDiameter)) return; Operation.ToolDiameter = value; OnPropertyChanged(); }
        }

        public double ContourHeight
        {
            get => Operation.ContourHeight;
            set { if (value.Equals(Operation.ContourHeight)) return; Operation.ContourHeight = value; OnPropertyChanged(); }
        }

        public double FeedXYRapid
        {
            get => Operation.FeedXYRapid;
            set { if (value.Equals(Operation.FeedXYRapid)) return; Operation.FeedXYRapid = value; OnPropertyChanged(); }
        }

        public double FeedXYWork
        {
            get => Operation.FeedXYWork;
            set { if (value.Equals(Operation.FeedXYWork)) return; Operation.FeedXYWork = value; OnPropertyChanged(); }
        }

        public double FeedZRapid
        {
            get => Operation.FeedZRapid;
            set { if (value.Equals(Operation.FeedZRapid)) return; Operation.FeedZRapid = value; OnPropertyChanged(); }
        }

        public double FeedZWork
        {
            get => Operation.FeedZWork;
            set { if (value.Equals(Operation.FeedZWork)) return; Operation.FeedZWork = value; OnPropertyChanged(); }
        }

        public double SafeZHeight
        {
            get => Operation.SafeZHeight;
            set { if (value.Equals(Operation.SafeZHeight)) return; Operation.SafeZHeight = value; OnPropertyChanged(); }
        }

        public double RetractHeight
        {
            get => Operation.RetractHeight;
            set { if (value.Equals(Operation.RetractHeight)) return; Operation.RetractHeight = value; OnPropertyChanged(); }
        }

        public int Decimals
        {
            get => Operation.Decimals;
            set { if (value == Operation.Decimals) return; Operation.Decimals = value; OnPropertyChanged(); }
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
                var polylines = ParseDxfLines(dialog.FileName);
                if (polylines.Count == 0)
                {
                    var msg = _localizationManager?.GetString("DxfImportNoLines") ?? "В файле не найдено линий для импорта.";
                    System.Windows.MessageBox.Show(msg, dialog.Title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    return;
                }

                Operation.Polylines = polylines;
                Operation.DxfFilePath = dialog.FileName;
                FilePath = dialog.FileName;
                var lineCount = polylines.Sum(p => Math.Max(0, p.Points.Count - 1));
                var infoTemplate = _localizationManager?.GetString("DxfImportInfo") ?? "Импортировано линий: {0}";
                ImportInfo = string.Format(infoTemplate, lineCount);
                ProfileMillingOperationsViewModel?.MainViewModel?.NotifyOperationsChanged();
            }
            catch (Exception ex)
            {
                var msg = _localizationManager?.GetString("DxfImportFailed") ?? "Ошибка импорта DXF:";
                System.Windows.MessageBox.Show($"{msg} {ex.Message}", dialog.Title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private List<DxfPolyline> ParseDxfLines(string path)
        {
            var polylines = new List<DxfPolyline>();
            var lines = File.ReadAllLines(path);
            int i = 0;
            
            double Parse(string v)
            {
                if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                    return d;
                return 0;
            }

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
                            case "39": // Thickness - игнорируем, импортируем все линии независимо от толщины
                                break;
                            case "0": i -= 2; goto EndLine;
                        }
                    }
                EndLine:
                    if (x1.HasValue && y1.HasValue && x2.HasValue && y2.HasValue)
                    {
                        polylines.Add(new DxfPolyline
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
                        polylines.Add(new DxfPolyline { Points = circlePoints });
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
                    if (cx.HasValue && cy.HasValue && radius.HasValue && radius.Value > 0 && 
                        startAngle.HasValue && endAngle.HasValue)
                    {
                        var arcPoints = ApproximateArc(cx.Value, cy.Value, radius.Value, 
                            startAngle.Value, endAngle.Value);
                        polylines.Add(new DxfPolyline { Points = arcPoints });
                    }
                    continue;
                }
            }

            return polylines;
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
            
            // Нормализуем углы
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

        protected override void OnClosed(IDataContext context)
        {
            base.OnClosed(context);
            ProfileMillingOperationsViewModel?.MainViewModel?.NotifyOperationsChanged();
        }
    }
}


