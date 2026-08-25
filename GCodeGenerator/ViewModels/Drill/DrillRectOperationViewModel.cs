#nullable enable
using GCodeGenerator.Localization;
using GCodeGenerator.Models;

namespace GCodeGenerator.ViewModels.Drill
{
    /// <summary>Диалог сверления по периметру прямоугольника.</summary>
    public class DrillRectOperationViewModel : DrillPatternEditorViewModelBase
    {
        public DrillRectOperationViewModel(ILocalizationManager localizationManager)
        {
            // Пункт 8.3: без захардкоженного фолбэка — отсутствующий ключ
            // вернёт «?Key?» (лог — в LocalizationManager).
            DisplayName = localizationManager?.GetString("AddDrillRect") ?? "AddDrillRect";
        }

        protected override DrillMode Mode => DrillMode.Rect;

        /// <summary>Шаблон без отверстий не имеет смысла.</summary>
        protected override bool IsValid(DrillPointsOperation operation) => PreviewHoles.Count > 0;
    }
}