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
        /// Пункт 8.2 плана (D4): восстанавливает шпиндель/СОЖ в
        /// <see cref="Current"/> из персистентных глобальных значений
        /// (Properties.Settings). Вызывается при открытии проекта без секций
        /// spindle/coolant (старые .ygc) и при создании нового проекта.
        /// </summary>
        void RestoreGlobalSpindleAndCoolant();
    }
}
