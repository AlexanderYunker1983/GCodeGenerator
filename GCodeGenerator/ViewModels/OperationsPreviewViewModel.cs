#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using GCodeGenerator.Diagnostics;
using GCodeGenerator.Models;
using GCodeGenerator.Preview;
using GCodeGenerator.Services;

namespace GCodeGenerator.ViewModels
{
    /// <summary>
    /// ViewModel of the 2D operations preview (plan item 6.3). Exposes a
    /// pure <see cref="OperationScene"/> (Core data, no WPF types).
    /// The view's code-behind only renders the scene and handles the mouse.
    ///
    /// DoD фазы 7: циклическая ссылка с MainViewModel убрана — VM не хранит
    /// ссылку на MainViewModel. MainViewModel пушит состояние (пересборка
    /// сцены, выбор) и подписывается на события
    /// <see cref="SelectionChanged"/>/<see cref="EditRequested"/>/<see cref="ShowAllRequested"/>.
    /// </summary>
    public class OperationsPreviewViewModel : ViewModelBase
    {
        private readonly ObservableCollection<OperationBase> _operations;
        private readonly IThemeService? _themeService;
        private readonly IAppLogger _logger;
        private OperationScene? _scene;
        private OperationBase? _selectedOperation;
        private Toolpath.ToolPath? _toolPath;
        private GCodeProgram? _program;
        private bool _showToolPath;
        private bool _isBuilding;
        private int _sceneBuildRevision;
        private CancellationTokenSource? _sceneBuildCancellation;

        public OperationsPreviewViewModel(
            ObservableCollection<OperationBase> operations,
            IThemeService? themeService,
            IAppLogger? logger = null)
        {
            _operations = operations ?? throw new ArgumentNullException(nameof(operations));
            // Пункт 7.5 плана: тема через IoC (ранее code-behind подписывался
            // на статический ThemeHelper.ThemeChanged).
            _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
            _logger = logger ?? NullAppLogger.Instance;
            _scene = OperationSceneBuilder.Build(_operations);
            _themeService.ThemeChanged += OnThemeServiceChanged;
            ShowAllCommand = new RelayCommand(RaiseShowAll);
        }

        /// <summary>The pure 2D scene of all operations (Core types only).</summary>
        public OperationScene? Scene
        {
            get => _scene;
            private set
            {
                if (ReferenceEquals(value, _scene)) return;
                _scene = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Selected operation. MainViewModel пушит выбор из списка и получает
        /// изменения из 2D-превью через <see cref="SelectionChanged"/>.
        /// </summary>
        public OperationBase? SelectedOperation
        {
            get => _selectedOperation;
            set
            {
                if (Equals(value, _selectedOperation)) return;
                _selectedOperation = value;
                OnPropertyChanged();
                SelectionChanged?.Invoke(this, value);
            }
        }

        /// <summary>Raised when the selection changed in the 2D preview (операция может быть null).</summary>
        public event EventHandler<OperationBase?>? SelectionChanged;

        /// <summary>Raised when the user requests editing of the selected operation (двойной клик в 2D-превью).</summary>
        public event EventHandler? EditRequested;

        /// <summary>Raised when "show all" is requested (fit the view to the scene).</summary>
        public event EventHandler? ShowAllRequested;

        /// <summary>Raised when the application theme changed (view redraws the scene) — пункт 7.5 плана.</summary>
        public event EventHandler? ThemeChanged;

        /// <summary>
        /// Показывать траекторию инструмента вместо контуров операций.
        ///
        /// Контуры показывают замысел — где лежит окружность, каких размеров
        /// прямоугольник, — но не знают ни о компенсации радиуса фрезы,
        /// ни о стратегии выборки, ни о числе проходов. Траектория показывает
        /// то, что действительно проделает станок.
        /// </summary>
        public bool ShowToolPath
        {
            get => _showToolPath;
            set
            {
                if (value == _showToolPath) return;
                _showToolPath = value;
                OnPropertyChanged();
                RebuildScene();
            }
        }

        /// <summary>
        /// Траектория последней генерации. Пока её нет, показывать нечего,
        /// и предпросмотр остаётся на контурах.
        /// </summary>
        public Toolpath.ToolPath? ToolPath
        {
            get => _toolPath;
            set
            {
                if (ReferenceEquals(value, _toolPath)) return;
                _toolPath = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasToolPath));
                if (ShowToolPath)
                    RebuildScene();
            }
        }

        /// <summary>Готовая программа с координатным прологом и парковкой.</summary>
        public GCodeProgram? Program
        {
            get => _program;
            set
            {
                if (ReferenceEquals(value, _program)) return;
                _program = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasToolPath));
                if (ShowToolPath)
                    RebuildScene();
            }
        }

        /// <summary>Есть ли что показывать в режиме траектории.</summary>
        public bool HasToolPath => (_program != null && _program.Blocks.Count > 0)
                                   || (_toolPath != null && !_toolPath.IsEmpty);

        /// <summary>Тяжёлая проекция готовой траектории строится в фоне.</summary>
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

        /// <summary>Вписать все контуры или траекторию в область предпросмотра.</summary>
        public ICommand ShowAllCommand { get; }

        /// <summary>Пересобирает сцену (вызывается из MainViewModel при любом изменении операций).</summary>
        public void RebuildScene()
        {
            var previousCancellation = _sceneBuildCancellation;
            _sceneBuildCancellation = null;
            previousCancellation?.Cancel();
            var revision = ++_sceneBuildRevision;

            var program = _program;
            var toolPath = _toolPath;
            if (!ShowToolPath || (program == null && toolPath == null))
            {
                Scene = OperationSceneBuilder.Build(_operations);
                IsBuilding = false;
                return;
            }

            var cancellation = new CancellationTokenSource();
            _sceneBuildCancellation = cancellation;
            IsBuilding = true;
            _ = RebuildToolPathSceneAsync(program, toolPath, revision, cancellation);
        }

        private async Task RebuildToolPathSceneAsync(
            GCodeProgram? program,
            Toolpath.ToolPath? toolPath,
            int revision,
            CancellationTokenSource cancellation)
        {
            try
            {
                var scene = await Task.Run(
                    () => program != null
                        ? BuildScene(program, cancellation.Token)
                        : BuildScene(toolPath!, cancellation.Token),
                    cancellation.Token);

                if (revision != _sceneBuildRevision || cancellation.IsCancellationRequested)
                    return;

                Scene = ResolveToDocumentOperations(scene);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                // A newer mode/program owns the preview now.
            }
            catch (Exception ex)
            {
                _logger.Error("Building the 2D tool-path preview failed", ex);
                if (revision == _sceneBuildRevision)
                    Scene = OperationScene.Empty;
            }
            finally
            {
                if (revision == _sceneBuildRevision)
                {
                    _sceneBuildCancellation = null;
                    IsBuilding = false;
                }
                cancellation.Dispose();
            }
        }

        /// <summary>Точка подмены для проверки фонового построения и отмены.</summary>
        protected virtual OperationScene BuildScene(
            GCodeProgram program,
            CancellationToken cancellationToken)
            => ProgramSceneProjection.Build(program, cancellationToken);

        /// <summary>Промежуточная траектория для редактора и тестов.</summary>
        protected virtual OperationScene BuildScene(
            Toolpath.ToolPath toolPath,
            CancellationToken cancellationToken)
            => ToolPathSceneProjection.Build(toolPath, cancellationToken);

        /// <summary>
        /// Заменяет в фигурах сцены клоны слепка на операции документа —
        /// по идентификатору операции, который копия несёт с оригинала.
        /// Генерация работает со слепком, и фигуры траектории помечены
        /// клонами; сцена же обязана вести к операциям документа — иначе
        /// клик по траектории выбирал бы объект, которого нет в списке:
        /// выделение снималось, перестановка гасла, удаление молча
        /// не удаляло, а правки уходили в отсоединённый клон. Замена
        /// делается один раз при сборке сцены.
        /// </summary>
        private OperationScene ResolveToDocumentOperations(OperationScene scene)
        {
            if (scene.IsEmpty)
                return scene;

            var byId = new Dictionary<Guid, OperationBase>();
            foreach (var operation in _operations)
                byId[operation.Id] = operation;

            var shapes = new List<OperationShape>(scene.Shapes.Count);
            foreach (var shape in scene.Shapes)
            {
                shapes.Add(shape.Operation != null
                           && byId.TryGetValue(shape.Operation.Id, out var document)
                           && !ReferenceEquals(document, shape.Operation)
                    ? new OperationShape(document, shape.Kind, shape.Points, shape.IsClosed, shape.IsFilled)
                    : shape);
            }

            return new OperationScene(shapes);
        }

        /// <summary>Запрос редактирования выбранной операции (вызывается из view по двойному клику).</summary>
        public void RequestEdit()
        {
            EditRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Поднятие "show all" (вызывается из MainViewModel).</summary>
        public void RaiseShowAll()
        {
            ShowAllRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnThemeServiceChanged(object? sender, EventArgs e)
        {
            ThemeChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
