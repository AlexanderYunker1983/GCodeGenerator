namespace GCodeGenerator.Core.Localization;

/// <summary>
/// Класс для доступа к строковым ресурсам
/// </summary>
public static class Resources
{
    private static global::System.Resources.ResourceManager? _resourceManager;

    /// <summary>
    /// Возвращает кэшированный экземпляр ResourceManager, используемый этим классом.
    /// </summary>
    [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
    public static global::System.Resources.ResourceManager ResourceManager
    {
        get
        {
            if (_resourceManager is null)
            {
                _resourceManager = new global::System.Resources.ResourceManager("GCodeGenerator.Core.Localization.Resources", typeof(Resources).Assembly);
            }
            return _resourceManager;
        }
    }

    /// <summary>
    /// Переопределяет свойство CurrentUICulture текущего потока для всех
    /// обращений к ресурсу с помощью этого класса ресурсов со строгой типизацией.
    /// </summary>
    [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
    public static global::System.Globalization.CultureInfo? Culture
    {
        get => LocalizationService.Instance.CurrentCulture;
        set => LocalizationService.Instance.CurrentCulture = value;
    }

    /// <summary>
    /// Ищет локализованную строку, похожую на Настройки приложения.
    /// </summary>
    public static string Settings_DisplayName => ResourceManager.GetString("Settings_DisplayName", Culture) ?? string.Empty;

    /// <summary>
    /// Ищет локализованную строку, похожую на Язык:.
    /// </summary>
    public static string Settings_Language => ResourceManager.GetString("Settings_Language", Culture) ?? string.Empty;

    /// <summary>
    /// Ищет локализованную строку, похожую на Тема:.
    /// </summary>
    public static string Settings_Theme => ResourceManager.GetString("Settings_Theme", Culture) ?? string.Empty;

    /// <summary>
    /// Ищет локализованную строку, похожую на Применить.
    /// </summary>
    public static string Settings_Apply => ResourceManager.GetString("Settings_Apply", Culture) ?? string.Empty;

    /// <summary>
    /// Ищет локализованную строку, похожую на Отменить.
    /// </summary>
    public static string Settings_Cancel => ResourceManager.GetString("Settings_Cancel", Culture) ?? string.Empty;

    /// <summary>
    /// Ищет локализованную строку, похожую на Системный.
    /// </summary>
    public static string Language_System => ResourceManager.GetString("Language_System", Culture) ?? string.Empty;

    /// <summary>
    /// Ищет локализованную строку, похожую на Английский.
    /// </summary>
    public static string Language_English => ResourceManager.GetString("Language_English", Culture) ?? string.Empty;

    /// <summary>
    /// Ищет локализованную строку, похожую на Русский.
    /// </summary>
    public static string Language_Russian => ResourceManager.GetString("Language_Russian", Culture) ?? string.Empty;

    /// <summary>
    /// Ищет локализованную строку, похожую на Системная.
    /// </summary>
    public static string Theme_System => ResourceManager.GetString("Theme_System", Culture) ?? string.Empty;

    /// <summary>
    /// Ищет локализованную строку, похожую на Светлая.
    /// </summary>
    public static string Theme_Light => ResourceManager.GetString("Theme_Light", Culture) ?? string.Empty;

    /// <summary>
    /// Ищет локализованную строку, похожую на Темная.
    /// </summary>
    public static string Theme_Dark => ResourceManager.GetString("Theme_Dark", Culture) ?? string.Empty;

    /// <summary>
    /// Ищет локализованную строку, похожую на Настройки.
    /// </summary>
    public static string Menu_Settings => ResourceManager.GetString("Menu_Settings", Culture) ?? string.Empty;

    /// <summary>
    /// Ищет локализованную строку, похожую на Настройки приложения.
    /// </summary>
    public static string Menu_Settings_Application => ResourceManager.GetString("Menu_Settings_Application", Culture) ?? string.Empty;

    /// <summary>
    /// Ищет локализованную строку, похожую на GCodeGenerator v.{0}.
    /// </summary>
    public static string Main_DisplayName => ResourceManager.GetString("Main_DisplayName", Culture) ?? string.Empty;

    /// <summary>
    /// Получает локализованную строку по ключу
    /// </summary>
    public static string GetString(string key)
    {
        return ResourceManager.GetString(key, Culture) ?? string.Empty;
    }

    /// <summary>
    /// Получает локализованную строку для значения enum
    /// </summary>
    public static string GetEnumString<T>(T enumValue) where T : struct, System.Enum
    {
        var enumType = enumValue.GetType();
        var name = System.Enum.GetName(enumType, enumValue) ?? enumValue.ToString() ?? string.Empty;
        var key = $"{enumType.Name}_{name}";
        return GetString(key);
    }

    /// <summary>
    /// Получает локализованную строку для значения enum (через object)
    /// </summary>
    public static string GetEnumString(object enumValue)
    {
        if (enumValue == null)
            return string.Empty;

        var enumType = enumValue.GetType();
        if (!enumType.IsEnum)
            return enumValue.ToString() ?? string.Empty;

        var name = System.Enum.GetName(enumType, enumValue) ?? enumValue.ToString() ?? string.Empty;
        var key = $"{enumType.Name}_{name}";
        return GetString(key);
    }
}

