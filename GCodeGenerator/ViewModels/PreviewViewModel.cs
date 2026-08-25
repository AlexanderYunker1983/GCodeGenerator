using GCodeGenerator.Localization;
using GCodeGenerator.Trajectory;

namespace GCodeGenerator.ViewModels
{
    /// <summary>
    /// ViewModel of the 3D G-code preview dialog (plan item 6.1).
    /// Получает траекторию инструмента и отдаёт чистую сцену
    /// (<see cref="TrajectoryScene"/>) — без разбора G-кода и без типов
    /// <c>System.Windows.Media.*</c>. Трёхмерную модель строит
    /// <see cref="Views.SceneRenderer"/> в code-behind окна.
    /// </summary>
    public class PreviewViewModel : CloseableViewModel, IHasDisplayName
    {
        private readonly ILocalizationManager _localizationManager;
        private Toolpath.ToolPath _toolPath;
        private TrajectoryScene _scene;

        public PreviewViewModel(ILocalizationManager localizationManager)
        {
            _localizationManager = localizationManager;
            // Пункт 8.3: без захардкоженного фолбэка — отсутствующий ключ
            // вернёт «?Key?» (лог — в LocalizationManager).
            DisplayName = _localizationManager?.GetString("PreviewGCode") ?? "PreviewGCode";
            Scene = TrajectoryScene.Empty;
        }

        /// <summary>
        /// Траектория, которую показывает окно. Задание её пересобирает
        /// <see cref="Scene"/>.
        ///
        /// Раньше окно получало готовую программу и разбирало её обратно,
        /// восстанавливая по G-словам, чем было каждое движение. Теперь
        /// показывается ровно то, из чего программа сделана.
        /// </summary>
        public Toolpath.ToolPath ToolPath
        {
            get => _toolPath;
            set
            {
                if (ReferenceEquals(value, _toolPath)) return;
                _toolPath = value;
                OnPropertyChanged();
                Scene = value != null ? ToolPathSceneBuilder.Build(value) : TrajectoryScene.Empty;
            }
        }

        /// <summary>The pure trajectory scene (Core types only).</summary>
        public TrajectoryScene Scene
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
