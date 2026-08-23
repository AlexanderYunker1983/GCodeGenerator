using GCodeGenerator.Models;

namespace GCodeGenerator.Services
{
    /// <summary>
    /// Пункт 7.5 плана: хранилище настроек через IoC (ранее статика
    /// <c>GCodeSettingsStore.Current</c>).
    /// </summary>
    public interface ISettingsStore
    {
        /// <summary>Текущие настройки (единый экземпляр на приложение).</summary>
        GCodeSettings Current { get; }

        /// <summary>Персистентность настроек (Properties.Settings).</summary>
        void Save();
    }
}
