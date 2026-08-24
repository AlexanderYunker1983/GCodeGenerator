using System;
using GCodeGenerator.Models;

namespace GCodeGenerator.Services
{
    /// <summary>
    /// Пункт 7.5 плана: хранилище настроек через IoC (ранее статика
    /// <c>GCodeSettingsStore.Current</c>).
    /// </summary>
    public interface ISettingsStore
    {
        /// <summary>
        /// Настройки, влияющие на генерацию, были применены к <see cref="Current"/>.
        /// </summary>
        event EventHandler SettingsChanged;

        /// <summary>Текущие настройки (единый экземпляр на приложение).</summary>
        GCodeSettings Current { get; }

        /// <summary>Персистентность настроек (Properties.Settings).</summary>
        void Save();

        /// <summary>
        /// Восстанавливает все глобальные настройки генерации в
        /// <see cref="Current"/> из Properties.Settings. UI-настройки не меняет.
        /// Вызывается перед применением настроек проекта и при создании проекта.
        /// </summary>
        void RestoreGlobalGenerationSettings();
    }
}
