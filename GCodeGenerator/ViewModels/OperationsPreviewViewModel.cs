#nullable enable
using System;
using System.Collections.ObjectModel;
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
        private OperationScene? _scene;
        private OperationBase? _selectedOperation;
        private Toolpath.ToolPath? _toolPath;
        private bool _showToolPath;

        public OperationsPreviewViewModel(ObservableCollection<OperationBase> operations, IThemeService? themeService)
        {
            _operations = operations ?? throw new ArgumentNullException(nameof(operations));
            // Пункт 7.5 плана: тема через IoC (ранее code-behind подписывался
            // на статический ThemeHelper.ThemeChanged).
            _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
            _scene = OperationSceneBuilder.Build(_operations);
            _themeService.ThemeChanged += OnThemeServiceChanged;
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

        /// <summary>Есть ли что показывать в режиме траектории.</summary>
        public bool HasToolPath => _toolPath != null && !_toolPath.IsEmpty;

        /// <summary>Пересобирает сцену (вызывается из MainViewModel при любом изменении операций).</summary>
        public void RebuildScene()
        {
            Scene = ShowToolPath && _toolPath != null
                ? ToolPathSceneProjection.Build(_toolPath)
                : OperationSceneBuilder.Build(_operations);
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
