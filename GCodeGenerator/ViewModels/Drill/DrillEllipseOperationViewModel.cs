#nullable enable
using GCodeGenerator.Localization;
using GCodeGenerator.Models;

namespace GCodeGenerator.ViewModels.Drill
{
    /// <summary>Диалог сверления по эллипсу: центр, радиусы и поворот.</summary>
    public class DrillEllipseOperationViewModel : DrillPatternEditorViewModelBase
    {
        public DrillEllipseOperationViewModel(ILocalizationManager localizationManager)
        {
            // Пункт 8.3: без захардкоженного фолбэка — отсутствующий ключ
            // вернёт «?Key?» (лог — в LocalizationManager).
            DisplayName = localizationManager?.GetString("AddDrillEllipse") ?? "AddDrillEllipse";
        }

        protected override DrillMode Mode => DrillMode.Ellipse;

        /// <summary>Шаблон без отверстий не имеет смысла.</summary>
        protected override bool IsValid(DrillPointsOperation operation) => PreviewHoles.Count > 0;
    }
}