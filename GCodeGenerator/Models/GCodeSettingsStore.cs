using System;
using GCodeGenerator.Services;

namespace GCodeGenerator.Models
{
    /// <summary>
    /// Легаси-статический фасад (пункт 7.5 плана): делегирует экземпляру
    /// <see cref="ISettingsStore"/> из IoC (тот же экземпляр, ленивое создание).
    /// Удаляется в следующем релизе.
    /// </summary>
    public static class GCodeSettingsStore
    {
        private static readonly Lazy<AppSettingsStore> _instance = new(() => new AppSettingsStore());

        /// <summary>Общий экземпляр настроек (тот, что регистрируется в IoC).</summary>
        public static AppSettingsStore Instance => _instance.Value;

        [Obsolete("Используйте экземпляр ISettingsStore из IoC (пункт 7.5 плана). Удаляется в следующем релизе.")]
        public static GCodeSettings Current => Instance.Current;

        [Obsolete("Используйте экземпляр ISettingsStore из IoC (пункт 7.5 плана). Удаляется в следующем релизе.")]
        public static void Save() => Instance.Save();
    }
}
