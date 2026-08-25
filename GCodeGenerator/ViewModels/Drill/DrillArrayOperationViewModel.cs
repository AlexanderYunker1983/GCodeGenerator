#nullable enable
using GCodeGenerator.Localization;
using GCodeGenerator.Models;

namespace GCodeGenerator.ViewModels.Drill
{
    /// <summary>Диалог сверления по сетке: шаг и число отверстий по строкам и рядам.</summary>
    public class DrillArrayOperationViewModel : DrillPatternEditorViewModelBase
    {
        public DrillArrayOperationViewModel(ILocalizationManager localizationManager)
        {
            // Пункт 8.3: без захардкоженного фолбэка — отсутствующий ключ
            // вернёт «?Key?» (лог — в LocalizationManager).
            DisplayName = localizationManager?.GetString("AddDrillArray") ?? "AddDrillArray";
        }

        protected override DrillMode Mode => DrillMode.Array;

    }
}