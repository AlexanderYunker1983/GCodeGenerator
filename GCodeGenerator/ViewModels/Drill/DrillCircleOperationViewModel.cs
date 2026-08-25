#nullable enable
using GCodeGenerator.Localization;
using GCodeGenerator.Models;

namespace GCodeGenerator.ViewModels.Drill
{
    /// <summary>Диалог сверления по окружности: центр, радиус и число отверстий.</summary>
    public class DrillCircleOperationViewModel : DrillPatternEditorViewModelBase
    {
        public DrillCircleOperationViewModel(ILocalizationManager localizationManager)
        {
            // Пункт 8.3: без захардкоженного фолбэка — отсутствующий ключ
            // вернёт «?Key?» (лог — в LocalizationManager).
            DisplayName = localizationManager?.GetString("AddDrillCircle") ?? "AddDrillCircle";
        }

        protected override DrillMode Mode => DrillMode.Circle;

        /// <summary>Шаблон без отверстий не имеет смысла.</summary>
        protected override bool IsValid(DrillPointsOperation operation) => PreviewHoles.Count > 0;
    }
}