#nullable enable
using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using GCodeGenerator.Diagnostics;
using GCodeGenerator.Localization;
using GCodeGenerator.Models;
using GCodeGenerator.Services;
using GCodeGenerator.Trajectory;

namespace GCodeGenerator.ViewModels
{
    /// <summary>
    /// ViewModel of the 3D G-code preview dialog (plan item 6.1).
    /// Получает структурированную программу и отдаёт чистую сцену
    /// (<see cref="TrajectoryScene"/>) — без разбора G-кода и без типов
    /// <c>System.Windows.Media.*</c>. Трёхмерную модель строит
    /// <see cref="Views.SceneRenderer"/> в code-behind окна.
    /// </summary>
    public class PreviewViewModel : CloseableViewModel, IHasDisplayName
    {
        private readonly ILocalizationManager? _localizationManager;
        private readonly IAppLogger _logger;
        private Toolpath.ToolPath? _toolPath;
        private GCodeProgram? _program;
        private TrajectoryScene? _scene;
        private TrajectoryScene _fullScene = TrajectoryScene.Empty;
        private bool _isBuilding;
        private string _sceneError = string.Empty;
        private bool _showXyGrid;
        private bool _showXzGrid;
        private bool _showYzGrid;
        private int _firstPreviewPrimitive = 1;
        private int _lastPreviewPrimitive = 1;
        private int _sceneBuildRevision;
        private CancellationTokenSource? _sceneBuildCancellation;

        /// <summary>Окно предпросмотра.</summary>
        /// <param name="localizationManager">Словарь интерфейса.</param>
        /// <param name="logger">
        /// Журнал: сбой построения сцены попадает в него целиком, со стеком.
        /// Пользователю в окне остаётся причина, а не пустота.
        /// </param>
        public PreviewViewModel(ILocalizationManager localizationManager, IAppLogger? logger = null)
        {
            _localizationManager = localizationManager;
            _logger = logger ?? NullAppLogger.Instance;
            // Пункт 8.3: без захардкоженного фолбэка — отсутствующий ключ
            // вернёт «?Key?» (лог — в LocalizationManager).
            DisplayName = _localizationManager?.GetString("PreviewGCode") ?? "PreviewGCode";
            Scene = TrajectoryScene.Empty;
        }

        /// <summary>
        /// Промежуточная траектория для внутренних тестов и совместимости.
        /// Рабочий поток задаёт <see cref="Program"/>.
        /// </summary>
        public Toolpath.ToolPath? ToolPath
        {
            get => _toolPath;
            set
            {
                if (ReferenceEquals(value, _toolPath)) return;
                _toolPath = value;
                OnPropertyChanged();
                _fullScene = TrajectoryScene.Empty;
                ResetPrimitiveRange();
                StartSceneRebuild();
            }
        }

        /// <summary>
        /// Готовая структурированная программа. В рабочем потоке имеет
        /// приоритет над ToolPath, поскольку содержит кадры постпроцессора.
        /// </summary>
        public GCodeProgram? Program
        {
            get => _program;
            set
            {
                if (ReferenceEquals(value, _program)) return;
                _program = value;
                OnPropertyChanged();
                _fullScene = TrajectoryScene.Empty;
                ResetPrimitiveRange();
                StartSceneRebuild();
            }
        }

        /// <summary>Количество элементарных прямых и дуг в полной сцене.</summary>
        public int PrimitiveCount => _fullScene.Segments.Count;

        /// <summary>
        /// Верхняя граница элемента управления. У скрытого пустого диапазона
        /// она остаётся равной единице, чтобы Minimum не оказался больше Maximum.
        /// </summary>
        public int PrimitiveSliderMaximum => Math.Max(1, PrimitiveCount);

        public bool HasPrimitives => PrimitiveCount > 0;

        public bool HasMultiplePrimitives => PrimitiveCount > 1;

        /// <summary>Первый показываемый примитив, нумерация для пользователя с единицы.</summary>
        public int FirstPreviewPrimitive
        {
            get => _firstPreviewPrimitive;
            set
            {
                var normalized = NormalizePrimitiveNumber(value);
                normalized = Math.Min(normalized, _lastPreviewPrimitive);
                if (normalized == _firstPreviewPrimitive) return;

                _firstPreviewPrimitive = normalized;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FirstPreviewPrimitiveText));
                OnPropertyChanged(nameof(PreviewPrimitiveRangeText));
                ApplyPrimitiveRange();
            }
        }

        /// <summary>Последний показываемый примитив, включая эту границу.</summary>
        public int LastPreviewPrimitive
        {
            get => _lastPreviewPrimitive;
            set
            {
                var normalized = NormalizePrimitiveNumber(value);
                normalized = Math.Max(normalized, _firstPreviewPrimitive);
                if (normalized == _lastPreviewPrimitive) return;

                _lastPreviewPrimitive = normalized;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LastPreviewPrimitiveText));
                OnPropertyChanged(nameof(PreviewPrimitiveRangeText));
                ApplyPrimitiveRange();
            }
        }

        /// <summary>Подпись выбранного диапазона над слайдером.</summary>
        public string PreviewPrimitiveRangeText
        {
            get
            {
                var format = _localizationManager?.GetString("PreviewPrimitiveRangeFormat")
                             ?? "Trajectory primitives: {0}–{1} of {2}";
                return string.Format(
                    CultureInfo.CurrentCulture,
                    format,
                    _firstPreviewPrimitive,
                    _lastPreviewPrimitive,
                    PrimitiveCount);
            }
        }

        public string FirstPreviewPrimitiveText => PrimitiveText(_firstPreviewPrimitive);

        public string LastPreviewPrimitiveText => PrimitiveText(_lastPreviewPrimitive);

        /// <summary>
        /// Идёт построение сцены. Окно показывает это ожиданием: на большой
        /// программе построение занимает заметное время.
        /// </summary>
        public bool IsBuilding
        {
            get => _isBuilding;
            private set
            {
                if (value == _isBuilding) return;
                _isBuilding = value;
                OnPropertyChanged();
            }
        }

        /// <summary>Показывать координатную сетку в плоскости XY (Z = 0).</summary>
        public bool ShowXyGrid
        {
            get => _showXyGrid;
            set
            {
                if (value == _showXyGrid) return;
                _showXyGrid = value;
                OnPropertyChanged();
            }
        }

        /// <summary>Показывать координатную сетку в плоскости XZ (Y = 0).</summary>
        public bool ShowXzGrid
        {
            get => _showXzGrid;
            set
            {
                if (value == _showXzGrid) return;
                _showXzGrid = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Почему сцена не построена; пусто — сбоя не было.
        ///
        /// Сбой построения не отменяет программу: она уже собрана,
        /// показывается текстом и сохраняется в файл. Поэтому окно не
        /// закрывается и не показывает модального сообщения — вместо сцены
        /// в нём стоит причина.
        /// </summary>
        public string SceneError
        {
            get => _sceneError;
            private set
            {
                if (value == _sceneError) return;
                _sceneError = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSceneError));
            }
        }

        /// <summary>Сцену построить не удалось.</summary>
        public bool HasSceneError => _sceneError.Length > 0;

        /// <summary>Показывать координатную сетку в плоскости YZ (X = 0).</summary>
        public bool ShowYzGrid
        {
            get => _showYzGrid;
            set
            {
                if (value == _showYzGrid) return;
                _showYzGrid = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Собирает сцену в фоне и показывает её, если траектория за это
        /// время не сменилась.
        ///
        /// Прежде сцена строилась прямо в присваивании траектории, то есть
        /// на потоке интерфейса: окно замирало на всё время обхода
        /// перемещений и разбиения дуг. Генерация в фон вынесена давно,
        /// а построение предпросмотра для той же программы оставалось
        /// синхронным.
        /// </summary>
        private void StartSceneRebuild()
        {
            var previousCancellation = _sceneBuildCancellation;
            _sceneBuildCancellation = null;
            previousCancellation?.Cancel();
            previousCancellation?.Dispose();

            var program = _program;
            var toolPath = _toolPath;
            var revision = ++_sceneBuildRevision;

            SceneError = string.Empty;

            if (program == null && (toolPath == null || toolPath.Operations.Count == 0))
            {
                _fullScene = TrajectoryScene.Empty;
                ResetPrimitiveRange();
                Scene = TrajectoryScene.Empty;
                IsBuilding = false;
                return;
            }

            var cancellation = new CancellationTokenSource();
            _sceneBuildCancellation = cancellation;
            // Задача запускается без ожидания намеренно: присваивание
            // траектории синхронно, а окно должно открыться сразу. Сбой при
            // этом не теряется — его ловит сама задача.
            _ = RebuildSceneAsync(program, toolPath, revision, cancellation);
        }

        private async Task RebuildSceneAsync(
            GCodeProgram? program,
            Toolpath.ToolPath? toolPath,
            int revision,
            CancellationTokenSource cancellation)
        {
            IsBuilding = true;
            try
            {
                var scene = await Task.Run(() => program != null
                    ? BuildScene(program, cancellation.Token)
                    : BuildScene(toolPath!, cancellation.Token), cancellation.Token);

                // Пока строили, могли показать другую траекторию: поздний
                // результат затирать новую сцену не должен.
                if (revision != _sceneBuildRevision
                    || !ReferenceEquals(program, _program)
                    || !ReferenceEquals(toolPath, _toolPath))
                    return;

                _fullScene = scene;
                ResetPrimitiveRange();
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                // A newer program owns the preview now.
            }
            catch (Exception ex)
            {
                // Сбой построения уходил в UnobservedTaskException — то есть
                // никуда: окно оставалось пустым, журнал молчал, и объяснить
                // происходящее было нечем. Программа при этом построена и
                // сохраняется: не показывается только её трёхмерный вид.
                _logger.Error("Building the 3D preview scene failed", ex);

                if (revision != _sceneBuildRevision
                    || !ReferenceEquals(program, _program)
                    || !ReferenceEquals(toolPath, _toolPath))
                    return;

                _fullScene = TrajectoryScene.Empty;
                ResetPrimitiveRange();
                SceneError = Localize("PreviewSceneFailed")
                    + Environment.NewLine
                    + CoreErrorMessages.Describe(ex, _localizationManager);
            }
            finally
            {
                if (revision == _sceneBuildRevision
                    && ReferenceEquals(program, _program)
                    && ReferenceEquals(toolPath, _toolPath))
                {
                    _sceneBuildCancellation = null;
                    IsBuilding = false;
                }
                cancellation.Dispose();
            }
        }

        /// <summary>
        /// Строит сцену по траектории.
        ///
        /// Отдельный виртуальный метод — точка подмены для проверок: сбой
        /// построения не воспроизвести никакой траекторией, потому что на
        /// любую построитель отвечает сценой, а пустую операцию траектория
        /// в себя не принимает.
        /// </summary>
        /// <param name="toolPath">Траектория, которую нужно показать.</param>
        /// <param name="cancellationToken">Отмена устаревшего построения.</param>
        protected virtual TrajectoryScene BuildScene(
            Toolpath.ToolPath toolPath,
            CancellationToken cancellationToken)
            => ToolPathSceneBuilder.Build(toolPath, cancellationToken);

        /// <summary>Строит сцену из уже постпроцессированной программы.</summary>
        /// <param name="program">Структурированная программа.</param>
        /// <param name="cancellationToken">Отмена устаревшего построения.</param>
        protected virtual TrajectoryScene BuildScene(
            GCodeProgram program,
            CancellationToken cancellationToken)
            => SceneBuilder.Build(program, cancellationToken);

        /// <summary>Строка словаря; без словаря — сам ключ, как и всюду в окнах.</summary>
        private string Localize(string key) => _localizationManager?.GetString(key) ?? key;

        private void ResetPrimitiveRange()
        {
            _firstPreviewPrimitive = 1;
            _lastPreviewPrimitive = Math.Max(1, PrimitiveCount);

            OnPropertyChanged(nameof(PrimitiveCount));
            OnPropertyChanged(nameof(PrimitiveSliderMaximum));
            OnPropertyChanged(nameof(HasPrimitives));
            OnPropertyChanged(nameof(HasMultiplePrimitives));
            OnPropertyChanged(nameof(FirstPreviewPrimitive));
            OnPropertyChanged(nameof(LastPreviewPrimitive));
            OnPropertyChanged(nameof(FirstPreviewPrimitiveText));
            OnPropertyChanged(nameof(LastPreviewPrimitiveText));
            OnPropertyChanged(nameof(PreviewPrimitiveRangeText));
            Scene = PrimitiveCount > 0 ? _fullScene : TrajectoryScene.Empty;
        }

        private void ApplyPrimitiveRange()
        {
            if (PrimitiveCount == 0)
            {
                Scene = TrajectoryScene.Empty;
                return;
            }

            if (_firstPreviewPrimitive == 1 && _lastPreviewPrimitive == PrimitiveCount)
            {
                Scene = _fullScene;
                return;
            }

            var count = _lastPreviewPrimitive - _firstPreviewPrimitive + 1;
            var selected = new TrajectorySegment[count];
            for (var index = 0; index < count; index++)
                selected[index] = _fullScene.Segments[_firstPreviewPrimitive - 1 + index];

            Scene = new TrajectoryScene(selected);
        }

        private int NormalizePrimitiveNumber(int value)
            => PrimitiveCount == 0 ? 1 : Math.Max(1, Math.Min(PrimitiveCount, value));

        private string PrimitiveText(int primitiveNumber)
        {
            if (primitiveNumber < 1 || primitiveNumber > _fullScene.Segments.Count)
                return string.Empty;

            var key = _fullScene.Segments[primitiveNumber - 1].MoveType switch
            {
                MoveType.Rapid => "PreviewPrimitiveRapid",
                MoveType.Linear => "PreviewPrimitiveLinear",
                MoveType.ArcCW => "PreviewPrimitiveArcClockwise",
                MoveType.ArcCCW => "PreviewPrimitiveArcCounterClockwise",
                _ => "PreviewPrimitive"
            };
            var fallback = _fullScene.Segments[primitiveNumber - 1].MoveType.ToString();
            var name = _localizationManager?.GetString(key) ?? fallback;

            return string.Format(CultureInfo.CurrentCulture, "{0}. {1}", primitiveNumber, name);
        }

        /// <summary>The pure trajectory scene (Core types only).</summary>
        public TrajectoryScene? Scene
        {
            get => _scene;
            private set
            {
                if (ReferenceEquals(value, _scene)) return;
                _scene = value;
                OnPropertyChanged();
            }
        }

        public string DisplayName { get; }
    }
}
