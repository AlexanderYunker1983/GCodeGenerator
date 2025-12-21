using GCodeGenerator.Core.Enums;

namespace GCodeGenerator.Core.Settings;

/// <summary>
/// Класс для хранения настроек приложения
/// </summary>
public class AppSettings
{
    /// <summary>
    /// Выбранный язык
    /// </summary>
    public Language Language { get; set; } = Language.System;

    /// <summary>
    /// Выбранная тема
    /// </summary>
    public Theme Theme { get; set; } = Theme.System;

    /// <summary>
    /// Версия формата настроек (для будущих миграций)
    /// </summary>
    public int Version { get; set; } = 1;
}

