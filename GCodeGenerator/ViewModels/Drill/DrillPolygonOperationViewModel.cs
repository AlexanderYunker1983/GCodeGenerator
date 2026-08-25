using GCodeGenerator.Localization;
using GCodeGenerator.Models;

namespace GCodeGenerator.ViewModels.Drill
{
    /// <summary>Диалог сверления по правильному многоугольнику.</summary>
    public class DrillPolygonOperationViewModel : DrillPatternEditorViewModelBase
    {
        public DrillPolygonOperationViewModel(ILocalizationManager localizationManager)
        {
            // Пункт 8.3: без захардкоженного фолбэка — отсутствующий ключ
            // вернёт «?Key?» (лог — в LocalizationManager).
            DisplayName = localizationManager?.GetString("AddDrillPolygon") ?? "AddDrillPolygon";
        }

        protected override DrillMode Mode => DrillMode.Polygon;

        /// <summary>Шаблон без отверстий не имеет смысла.</summary>
        protected override bool IsValid() => PreviewHoles.Count > 0;
    }
}