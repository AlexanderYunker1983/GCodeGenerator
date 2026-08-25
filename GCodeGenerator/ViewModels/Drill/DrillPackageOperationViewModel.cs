using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using GCodeGenerator.Localization;
using GCodeGenerator.Models;

namespace GCodeGenerator.ViewModels.Drill
{
    /// <summary>
    /// Диалог сверления по корпусу компонента: центр, поворот и выбор корпуса
    /// из перечня. Координаты выводов задаёт сам корпус.
    /// </summary>
    public partial class DrillPackageOperationViewModel : DrillPatternEditorViewModelBase
    {
        [ObservableProperty]
        private double _centerX;

        [ObservableProperty]
        private double _centerY;

        [ObservableProperty]
        private double _z;

        [ObservableProperty]
        private double _rotationAngle;

        [ObservableProperty]
        private PackageDefinition _selectedPackage;

        public DrillPackageOperationViewModel(ILocalizationManager localizationManager)
        {
            // Пункт 8.3: без захардкоженного фолбэка — отсутствующий ключ
            // вернёт «?Key?» (лог — в LocalizationManager).
            DisplayName = localizationManager?.GetString("AddDrillPackage") ?? "AddDrillPackage";

            // Перечень корпусов принадлежит ядру: по имени корпуса, сохранённому
            // в проекте, отверстия должны пересчитываться и без открытого диалога.
            Packages = new ObservableCollection<PackageDefinition>(PackageCatalog.All);
            SelectedPackage = PackageCatalog.FindOrDefault(PackageCatalog.DefaultPackageName);
        }

        /// <summary>Корпуса, доступные для выбора.</summary>
        public ObservableCollection<PackageDefinition> Packages { get; }

        protected override DrillMode Mode => DrillMode.Package;

        protected override void LoadPatternSpecificParameters(DrillPointsOperation operation)
        {
            CenterX = operation.CenterX;
            CenterY = operation.CenterY;
            Z = operation.Z;
            RotationAngle = operation.RotationAngle;
            SelectedPackage = PackageCatalog.FindOrDefault(operation.PackageName);
        }

        protected override void ApplyPatternSpecificParameters(DrillPointsOperation target)
        {
            target.CenterX = CenterX;
            target.CenterY = CenterY;
            target.Z = Z;
            target.RotationAngle = RotationAngle;
            target.PackageName = SelectedPackage?.Name ?? string.Empty;
        }

        // Удаление операции при невалидных параметрах (legacy «remove if invalid», пункт 7.3):
        // шаблон без отверстий не имеет смысла.
        protected override bool IsValid() => PreviewHoles.Count > 0;
    }
}