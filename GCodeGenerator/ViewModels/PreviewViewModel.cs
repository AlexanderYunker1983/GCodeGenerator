using GCodeGenerator.Localization;
using GCodeGenerator.Models;
using GCodeGenerator.Trajectory;

namespace GCodeGenerator.ViewModels
{
    /// <summary>
    /// ViewModel of the 3D G-code preview dialog (plan item 6.1).
    /// Takes a structured <see cref="GCodeProgram"/> (not text) and exposes
    /// a pure <see cref="TrajectoryScene"/> — no G-code parsing and no
    /// <c>System.Windows.Media.*</c> types here. The WPF <c>Model3DGroup</c>
    /// is built by <see cref="Views.SceneRenderer"/> in the view's code-behind.
    /// </summary>
    public class PreviewViewModel : CloseableViewModel, IHasDisplayName
    {
        private readonly ILocalizationManager _localizationManager;
        private GCodeProgram _program;
        private TrajectoryScene _scene;

        public PreviewViewModel(ILocalizationManager localizationManager)
        {
            _localizationManager = localizationManager;
            var title = _localizationManager?.GetString("PreviewGCode");
            DisplayName = string.IsNullOrEmpty(title) ? "Предварительный просмотр G-кода" : title;
            Scene = TrajectoryScene.Empty;
        }

        /// <summary>
        /// The generated program to preview (structured blocks, not text).
        /// Setting it rebuilds <see cref="Scene"/>.
        /// </summary>
        public GCodeProgram Program
        {
            get => _program;
            set
            {
                if (ReferenceEquals(value, _program)) return;
                _program = value;
                OnPropertyChanged();
                Scene = value != null ? SceneBuilder.Build(value) : TrajectoryScene.Empty;
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
