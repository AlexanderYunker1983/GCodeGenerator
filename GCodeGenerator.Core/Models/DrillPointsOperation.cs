#nullable enable
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text.Json.Serialization;

using CommunityToolkit.Mvvm.ComponentModel;

namespace GCodeGenerator.Models
{
    /// <summary>
    /// Drilling holes operation with individual hole Z-parameters
    /// and common XY feeds and safety settings.
    ///
    /// The pattern is described by <see cref="DrillMode"/> and the typed
    /// parameters below (plan item 3.1); <see cref="Holes"/> always holds the
    /// concrete hole list that the generator drills.
    /// </summary>
    public partial class DrillPointsOperation : CuttingOperationBase, IValidatable
    {
        public DrillPointsOperation() : base(OperationCategory.Drill, "Drill points")
        {
            AttachHoles(_holes);
        }

        /// <summary>
        /// Drill pattern (plan item 3.1). Defaults to <see cref="DrillMode.Points"/>
        /// so that legacy files without this field (and manually created operations)
        /// keep the previous "individual holes" behavior.
        /// </summary>
        [ObservableProperty]
        private DrillMode _drillMode = DrillMode.Points;

        /// <summary>
        /// Отверстия, заданные пользователем поштучно (режим
        /// <see cref="DrillMode.Points"/>). В остальных режимах расстановку
        /// задаёт шаблон, и этот список не используется — сверлится
        /// <see cref="HolesToDrill"/>.
        ///
        /// Setter is needed for JSON deserialization of saved projects.
        ///
        /// Наблюдаемая коллекция: отверстия правят прямо в таблице диалога,
        /// и добавленное, удалённое или изменённое отверстие должно быть
        /// видно в предпросмотре сразу.
        ///
        /// Операция следит и за составом списка, и за самими отверстиями:
        /// иначе о правке координаты в таблице не узнал бы никто — обычное
        /// уведомление приходит только при замене всего списка.
        /// </summary>
        public ObservableCollection<DrillHole> Holes
        {
            get => _holes;
            set
            {
                var replacement = value ?? new ObservableCollection<DrillHole>();
                if (ReferenceEquals(_holes, replacement))
                    return;

                DetachHoles(_holes);
                _holes = replacement;
                AttachHoles(_holes);
                OnPropertyChanged();
            }
        }

        private ObservableCollection<DrillHole> _holes = new ObservableCollection<DrillHole>();

        private void AttachHoles(ObservableCollection<DrillHole> holes)
        {
            if (holes == null)
                return;

            holes.CollectionChanged += OnHolesCollectionChanged;
            foreach (var hole in holes)
                if (hole != null) hole.PropertyChanged += OnHoleChanged;
        }

        private void DetachHoles(ObservableCollection<DrillHole> holes)
        {
            if (holes == null)
                return;

            holes.CollectionChanged -= OnHolesCollectionChanged;
            foreach (var hole in holes)
                if (hole != null) hole.PropertyChanged -= OnHoleChanged;
        }

        private void OnHolesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // Отверстие может быть пустым: файл проекта, написанный вручную,
            // способен принести и такое — валидация сообщит об этом отдельно.
            if (e.OldItems != null)
                foreach (DrillHole hole in e.OldItems)
                    if (hole != null) hole.PropertyChanged -= OnHoleChanged;

            if (e.NewItems != null)
                foreach (DrillHole hole in e.NewItems)
                    if (hole != null) hole.PropertyChanged += OnHoleChanged;

            OnPropertyChanged(nameof(Holes));
        }

        private void OnHoleChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
            => OnPropertyChanged(nameof(Holes));

        /// <summary>
        /// Отверстия, которые будут просверлены: в поштучном режиме — список
        /// <see cref="Holes"/>, в остальных — расстановка, вычисленная
        /// шаблоном по параметрам операции.
        ///
        /// Прежде отверстия шаблона считал диалог и записывал их в операцию,
        /// откуда они попадали в файл проекта — тысячами записей рядом с
        /// параметрами, из которых они получены. Два описания одного и того
        /// же могли разойтись: достаточно было поправить параметр в файле
        /// вручную, и программа сверлила бы по старым координатам. Теперь
        /// источник один — параметры, а отверстия из них выводятся.
        /// </summary>
        [JsonIgnore]
        public IReadOnlyList<DrillHole> HolesToDrill => DrillPatterns.For(DrillMode).Holes(this);

        /// <summary>
        /// Safe Z height for moves between holes.
        /// </summary>
        [ObservableProperty]
        private double _safeZBetweenHoles = 1.0;

        // ------------------------------------------------------------------
        // Pattern parameters (plan item 3.1; previously stored in Metadata).
        // Defaults match the values the drill dialogs used to show for a new
        // operation of the corresponding mode.
        // ------------------------------------------------------------------

        // --- Line / Array / Rect pattern ---------------------------------

        /// <summary>Start point X of the line/grid pattern.</summary>
        [ObservableProperty]
        private double _startX;

        /// <summary>Start point Y of the line/grid pattern.</summary>
        [ObservableProperty]
        private double _startY;

        /// <summary>Start point Z of the line/grid pattern.</summary>
        [ObservableProperty]
        private double _startZ;

        /// <summary>Distance between neighboring holes in the pattern.</summary>
        [ObservableProperty]
        private double _distance = 10.0;

        /// <summary>Number of holes per line (line mode) or per row (array/rect mode).</summary>
        [ObservableProperty]
        private int _holeCount = 3;

        /// <summary>Pattern direction angle in degrees (0 = along X axis).</summary>
        [ObservableProperty]
        private double _angleDeg;

        /// <summary>Distance between rows (array/rect mode).</summary>
        [ObservableProperty]
        private double _rowPitch = 10.0;

        /// <summary>Number of rows (array/rect mode).</summary>
        [ObservableProperty]
        private int _rowCount = 2;

        // --- Circle / Arc / Polygon / Ellipse pattern ---------------------

        /// <summary>Center X of the circular pattern.</summary>
        [ObservableProperty]
        private double _centerX;

        /// <summary>Center Y of the circular pattern.</summary>
        [ObservableProperty]
        private double _centerY;

        /// <summary>Contour height (Z) of the circular pattern.</summary>
        [ObservableProperty]
        private double _z;

        /// <summary>Radius of the circle/arc/polygon pattern.</summary>
        [ObservableProperty]
        private double _radius = 10.0;

        /// <summary>Start angle of the circle/arc/ellipse pattern in degrees.</summary>
        [ObservableProperty]
        private double _startAngleDeg;

        /// <summary>End angle of the arc pattern in degrees.</summary>
        [ObservableProperty]
        private double _endAngleDeg = 90.0;

        /// <summary>Rotation angle of the polygon/ellipse pattern in degrees.</summary>
        [ObservableProperty]
        private double _rotationAngle;

        /// <summary>Number of sides of the polygon pattern.</summary>
        [ObservableProperty]
        private int _numberOfSides = 6;

        /// <summary>Number of holes per side of the polygon pattern.</summary>
        [ObservableProperty]
        private int _holesPerSide = 2;

        /// <summary>Horizontal radius of the ellipse pattern.</summary>
        [ObservableProperty]
        private double _radiusX = 10.0;

        /// <summary>Vertical radius of the ellipse pattern.</summary>
        [ObservableProperty]
        private double _radiusY = 10.0;

        // --- Package pattern ----------------------------------------------

        /// <summary>
        /// Name of the package template (DIP8, SOIC-8, ...). Empty for a fresh
        /// operation: the dialog falls back to its default template (DIP8).
        /// </summary>
        [ObservableProperty]
        private string _packageName = string.Empty;


        /// <summary>
        /// Creates a fresh operation for the given drill mode with the default
        /// parameters the dialog used to show for a new operation of that mode
        /// (plan item 3.1).
        /// </summary>
        public static DrillPointsOperation CreateNew(DrillMode mode)
        {
            var operation = new DrillPointsOperation { DrillMode = mode };
            // Circular patterns (circle/arc/ellipse) used to default to 2 holes
            // in the dialog, which differs from the model default (3).
            if (mode == DrillMode.Circle || mode == DrillMode.Arc || mode == DrillMode.Ellipse)
                operation.HoleCount = 2;
            return operation;
        }

        public override string GetDescription()
        {
            return $"Drill {HolesToDrill.Count} hole(s)";
        }

        /// <summary>
        /// Проверяет расстановку: сверлится то, что вернул шаблон, — в
        /// поштучном режиме это заданный пользователем список, в остальных
        /// вычисленные по параметрам координаты.
        /// </summary>
        /// <param name="issues">Список проблем, куда добавляются найденные.</param>
        private void AddHoleIssues(List<ValidationIssue> issues)
        {
            var holes = HolesToDrill;
            if (holes == null || holes.Count == 0)
            {
                issues.Add(new ValidationIssue(nameof(Holes), ValidationCode.Empty, "no holes to drill"));
                return;
            }

            for (int i = 0; i < holes.Count; i++)
            {
                var hole = holes[i];
                if (hole == null)
                {
                    issues.Add(new ValidationIssue($"Holes[{i}]", ValidationCode.Empty, "hole is null"));
                    continue;
                }

                OperationValidation.AddIfNotPositive(issues, $"Holes[{i}].TotalDepth", hole.TotalDepth);
                OperationValidation.AddIfNotPositive(issues, $"Holes[{i}].StepDepth", hole.StepDepth);
                OperationValidation.AddIfNotPositive(issues, $"Holes[{i}].FeedZWork", hole.FeedZWork);
                OperationValidation.AddIfNotPositive(issues, $"Holes[{i}].FeedZRapid", hole.FeedZRapid);
                OperationValidation.AddIfNotFinite(issues, $"Holes[{i}].X", hole.X);
                OperationValidation.AddIfNotFinite(issues, $"Holes[{i}].Y", hole.Y);
                OperationValidation.AddIfNotFinite(issues, $"Holes[{i}].Z", hole.Z);
            }
        }

        /// <summary>
        /// Domain validation (plan item 3.7): the drilled hole list, the
        /// per-hole Z parameters and the mode-specific pattern parameters.
        /// Point values that the generators can handle are never flagged.
        /// </summary>
        public IReadOnlyList<ValidationIssue> Validate()
        {
            var issues = new List<ValidationIssue>();

            // Подачи, отвод и точность вывода нужны в любом режиме: между
            // отверстиями инструмент идёт на быстрой подаче, вглубь — на рабочей.
            OperationValidation.AddCuttingIssues(issues, this);
            OperationValidation.AddIfNotFinite(issues, nameof(SafeZBetweenHoles), SafeZBetweenHoles);

            // Pattern modes share common Z parameters; Points mode keeps
            // per-hole Z parameters in Holes only.
            if (DrillMode != DrillMode.Points)
            {
                OperationValidation.AddIfNotPositive(issues, nameof(TotalDepth), TotalDepth);
                OperationValidation.AddIfNotPositive(issues, nameof(StepDepth), StepDepth);
            }

            switch (DrillMode)
            {
                case DrillMode.Line:
                    OperationValidation.AddIfBelow(issues, nameof(HoleCount), HoleCount, 1);
                    OperationValidation.AddIfNotPositive(issues, nameof(Distance), Distance);
                    break;
                case DrillMode.Array:
                case DrillMode.Rect:
                    OperationValidation.AddIfBelow(issues, nameof(HoleCount), HoleCount, 1);
                    OperationValidation.AddIfNotPositive(issues, nameof(Distance), Distance);
                    OperationValidation.AddIfBelow(issues, nameof(RowCount), RowCount, 1);
                    OperationValidation.AddIfNotPositive(issues, nameof(RowPitch), RowPitch);
                    break;
                case DrillMode.Circle:
                case DrillMode.Arc:
                    OperationValidation.AddIfBelow(issues, nameof(HoleCount), HoleCount, 1);
                    OperationValidation.AddIfNotPositive(issues, nameof(Radius), Radius);
                    break;
                case DrillMode.Polygon:
                    OperationValidation.AddIfNotPositive(issues, nameof(Radius), Radius);
                    OperationValidation.AddIfBelow(issues, nameof(NumberOfSides), NumberOfSides, 3);
                    OperationValidation.AddIfBelow(issues, nameof(HolesPerSide), HolesPerSide, 1);
                    break;
                case DrillMode.Ellipse:
                    OperationValidation.AddIfBelow(issues, nameof(HoleCount), HoleCount, 1);
                    OperationValidation.AddIfNotPositive(issues, nameof(RadiusX), RadiusX);
                    OperationValidation.AddIfNotPositive(issues, nameof(RadiusY), RadiusY);
                    break;
                case DrillMode.Package:
                    // PackageName may be empty: the dialog falls back to its default template.
                    break;
                case DrillMode.Points:
                default:
                    break;
            }

            // Расстановка проверяется последней и только если параметры
            // шаблона верны: пустая расстановка при неверном параметре — его
            // следствие, и называть её второй проблемой значит показать
            // пользователю два сообщения об одной ошибке.
            if (issues.Count == 0)
                AddHoleIssues(issues);

            return issues;
        }
    }
}
