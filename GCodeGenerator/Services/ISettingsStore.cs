#nullable enable
using System;
using GCodeGenerator.Models;

namespace GCodeGenerator.Services
{
    /// <summary>
    /// Хранилище настроек генерации и UI, получаемое через IoC.
    ///
    /// Генерационные секции <see cref="Current"/> принадлежат открытому
    /// документу и сохраняются в файл проекта; постоянное хранилище
    /// (Properties.Settings) держит их отдельную копию — умолчания для новых
    /// проектов, которые меняются только явной командой
    /// <see cref="SaveGenerationDefaults"/>. Группы Ui и Machine принадлежат
    /// приложению и сохраняются всегда; проект не может заменить профиль
    /// физического оборудования.
    /// </summary>
    public interface ISettingsStore
    {
        /// <summary>
        /// Генерационные настройки <see cref="Current"/> фактически
        /// изменились: программа устарела, проект несохранён. Смена темы или
        /// языка события не поднимает — они не влияют ни на программу,
        /// ни на файл проекта.
        /// </summary>
        event EventHandler GenerationSettingsChanged;

        /// <summary>
        /// Изменился локальный профиль станка. Готовую программу нужно
        /// проверить заново, но документ не менялся и не становится грязным.
        /// </summary>
        event EventHandler MachineProfileChanged;

        /// <summary>Текущие настройки (единый экземпляр на приложение).</summary>
        GCodeSettings Current { get; }

        /// <summary>
        /// Записывает Ui-настройки и профиль станка в постоянное хранилище. Генерационные
        /// секции не пишет: они принадлежат документу, и OK окна настроек
        /// не должен превращать настройки открытого проекта в умолчания
        /// приложения.
        /// </summary>
        void Save();

        /// <summary>
        /// Записывает генерационные секции <paramref name="source"/> в
        /// постоянное хранилище как умолчания для новых проектов.
        /// <see cref="Current"/> не меняет.
        /// </summary>
        /// <param name="source">Значения, которые становятся умолчаниями.</param>
        void SaveGenerationDefaults(GCodeSettings source);

        /// <summary>
        /// Восстанавливает все глобальные настройки генерации в
        /// <see cref="Current"/> из Properties.Settings. UI-настройки не
        /// меняет. Вызывается при создании нового проекта.
        /// </summary>
        void RestoreGlobalGenerationSettings();

        /// <summary>
        /// Применяет настройки открытого файла проекта к <see cref="Current"/>:
        /// сначала глобальные умолчания, поверх — секции файла. Отсутствующая
        /// секция (null — файл прежней версии) остаётся умолчанием.
        /// </summary>
        /// <param name="format">Секция format файла или null.</param>
        /// <param name="spindle">Секция spindle файла или null.</param>
        /// <param name="coolant">Секция coolant файла или null.</param>
        /// <param name="workCoordinate">Секция workCoordinate файла или null.</param>
        void ApplyProjectSettings(
            GCodeFormatSettings? format,
            SpindleSettings? spindle,
            CoolantSettings? coolant,
            WorkCoordinateSettings? workCoordinate);
    }
}
