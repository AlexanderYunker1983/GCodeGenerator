#nullable enable
using GCodeGenerator.Localization;
using GCodeGenerator.Models;

namespace GCodeGenerator.ViewModels.Drill
{
    /// <summary>Диалог сверления по линии: начальная точка, шаг, число отверстий и угол линии.</summary>
    public class DrillLineOperationViewModel : DrillPatternEditorViewModelBase
    {
        public DrillLineOperationViewModel(ILocalizationManager localizationManager)
        {
            // Пункт 8.3: без захардкоженного фолбэка — отсутствующий ключ
            // вернёт «?Key?» (лог — в LocalizationManager).
            DisplayName = localizationManager?.GetString("AddDrillLine") ?? "AddDrillLine";
        }

        protected override DrillMode Mode => DrillMode.Line;

    }
}