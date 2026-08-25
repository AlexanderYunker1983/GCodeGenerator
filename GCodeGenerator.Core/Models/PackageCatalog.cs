#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace GCodeGenerator.Models
{
    /// <summary>
    /// Корпуса электронных компонентов для сверления по шаблону.
    ///
    /// Перечень был объявлен в view-модели диалога, поэтому ядро не могло
    /// пересчитать отверстия по имени корпуса, сохранённому в проекте:
    /// координаты выводов существовали только пока открыт диалог.
    /// </summary>
    public static class PackageCatalog
    {
        /// <summary>Корпус, выбираемый для новой операции.</summary>
        public const string DefaultPackageName = "DIP8";

        private static readonly PackageDefinition[] Packages =
        {
            // Двухрядные корпуса DIP: шаг 2.54, ряды на расстоянии 7.62
            new PackageDefinition("DIP8", 4, 2.54, 7.62),
            new PackageDefinition("DIP14", 7, 2.54, 7.62),
            new PackageDefinition("DIP16", 8, 2.54, 7.62),
            new PackageDefinition("DIP18", 9, 2.54, 7.62),
            new PackageDefinition("DIP20", 10, 2.54, 7.62),
            new PackageDefinition("DIP24", 12, 2.54, 7.62),
            new PackageDefinition("DIP28", 14, 2.54, 7.62),
            new PackageDefinition("DIP32", 16, 2.54, 7.62),
            new PackageDefinition("DIP40", 20, 2.54, 7.62),

            // Однорядные корпуса: расстояние между рядами не задано
            new PackageDefinition("TO-220", 3, 2.54, 0),
            new PackageDefinition("TO-92", 3, 2.54, 0),

            // Планарные корпуса SOIC: шаг 1.27, ряды на расстоянии 5.3
            new PackageDefinition("SOIC-8", 4, 1.27, 5.3),
            new PackageDefinition("SOIC-14", 7, 1.27, 5.3),
            new PackageDefinition("SOIC-16", 8, 1.27, 5.3),
        };

        /// <summary>Все известные корпуса.</summary>
        public static IReadOnlyList<PackageDefinition> All => Packages;

        /// <summary>
        /// Корпус по имени. Пустое или неизвестное имя даёт корпус
        /// по умолчанию: операция с ещё не выбранным корпусом должна
        /// показывать осмысленный шаблон.
        /// </summary>
        public static PackageDefinition FindOrDefault(string name)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                var found = Packages.FirstOrDefault(
                    package => string.Equals(package.Name, name, StringComparison.OrdinalIgnoreCase));
                if (found != null)
                    return found;
            }

            return Packages.First(package => package.Name == DefaultPackageName);
        }
    }
}
