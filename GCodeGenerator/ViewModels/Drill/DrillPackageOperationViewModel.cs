#nullable enable
using System.Collections.ObjectModel;
using GCodeGenerator.Localization;
using GCodeGenerator.Models;

namespace GCodeGenerator.ViewModels.Drill
{
    /// <summary>
    /// Диалог сверления по корпусу компонента: центр, поворот и выбор корпуса
    /// из перечня. Координаты выводов задаёт сам корпус.
    ///
    /// Операция хранит имя корпуса, а окно показывает список: выбранный
    /// элемент связывает одно с другим.
    /// </summary>
    public partial class DrillPackageOperationViewModel : DrillPatternEditorViewModelBase
    {
        private PackageDefinition? _selectedPackage;

        public DrillPackageOperationViewModel(ILocalizationManager localizationManager)
        {
            // Пункт 8.3: без захардкоженного фолбэка — отсутствующий ключ
            // вернёт «?Key?» (лог — в LocalizationManager).
            DisplayName = localizationManager?.GetString("AddDrillPackage") ?? "AddDrillPackage";

            // Перечень корпусов принадлежит ядру: по имени корпуса, сохранённому
            // в проекте, отверстия должны пересчитываться и без открытого диалога.
            Packages = new ObservableCollection<PackageDefinition>(PackageCatalog.All);
        }

        /// <summary>Корпуса, доступные для выбора.</summary>
        public ObservableCollection<PackageDefinition> Packages { get; }

        /// <summary>
        /// Выбранный корпус. В операцию уходит его имя — по нему отверстия
        /// пересчитываются и при следующем открытии проекта.
        /// </summary>
        public PackageDefinition? SelectedPackage
        {
            get => _selectedPackage;
            set
            {
                if (!SetProperty(ref _selectedPackage, value) || Operation == null)
                    return;

                Operation.PackageName = value?.Name ?? string.Empty;
            }
        }

        protected override DrillMode Mode => DrillMode.Package;

        protected override void OnOperationChanged(DrillPointsOperation operation)
        {
            base.OnOperationChanged(operation);

            SelectedPackage = PackageCatalog.FindOrDefault(operation.PackageName);
        }

    }
}
