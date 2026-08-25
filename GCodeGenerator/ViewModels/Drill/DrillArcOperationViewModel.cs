#nullable enable
using GCodeGenerator.Localization;
using GCodeGenerator.Models;

namespace GCodeGenerator.ViewModels.Drill
{
    /// <summary>Диалог сверления по дуге: центр, радиус и угловой сектор.</summary>
    public class DrillArcOperationViewModel : DrillPatternEditorViewModelBase
    {
        public DrillArcOperationViewModel(ILocalizationManager localizationManager)
        {
            // Пункт 8.3: без захардкоженного фолбэка — отсутствующий ключ
            // вернёт «?Key?» (лог — в LocalizationManager).
            DisplayName = localizationManager?.GetString("AddDrillArc") ?? "AddDrillArc";
        }

        protected override DrillMode Mode => DrillMode.Arc;

        /// <summary>Шаблон без отверстий не имеет смысла.</summary>
        protected override bool IsValid(DrillPointsOperation operation) => PreviewHoles.Count > 0;
    }
}