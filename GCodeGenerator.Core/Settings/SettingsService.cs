using System;
using System.IO;
using System.Text.Json;

namespace GCodeGenerator.Core.Settings;

/// <summary>
/// Сервис для сохранения и загрузки настроек приложения
/// </summary>
public class SettingsService
{
    private static SettingsService? _instance;
    private static readonly object _lock = new object();
    
    private readonly string _settingsFilePath;
    private AppSettings? _currentSettings;

    private SettingsService()
    {
        // Получаем путь к папке ApplicationData для текущей платформы
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appDataPath, "GCodeGenerator");
        
        // Создаем папку, если она не существует
        if (!Directory.Exists(appFolder))
        {
            Directory.CreateDirectory(appFolder);
        }
        
        _settingsFilePath = Path.Combine(appFolder, "settings.json");
    }

    /// <summary>
    /// Получить экземпляр SettingsService (Singleton)
    /// </summary>
    public static SettingsService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new SettingsService();
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// Загрузить настройки из файла
    /// </summary>
    public AppSettings LoadSettings()
    {
        if (_currentSettings != null)
            return _currentSettings;

        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = File.ReadAllText(_settingsFilePath);
                _currentSettings = JsonSerializer.Deserialize<AppSettings>(json);
                
                if (_currentSettings != null)
                    return _currentSettings;
            }
        }
        catch (Exception ex)
        {
            // Логируем ошибку, но не прерываем работу приложения
            System.Diagnostics.Debug.WriteLine($"Ошибка при загрузке настроек: {ex.Message}");
        }

        // Возвращаем настройки по умолчанию, если не удалось загрузить
        _currentSettings = new AppSettings();
        return _currentSettings;
    }

    /// <summary>
    /// Сохранить настройки в файл
    /// </summary>
    public void SaveSettings(AppSettings settings)
    {
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));

        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            var json = JsonSerializer.Serialize(settings, options);
            
            // Создаем временный файл для атомарной записи
            var tempFilePath = _settingsFilePath + ".tmp";
            File.WriteAllText(tempFilePath, json);
            
            // Атомарно заменяем старый файл новым
            File.Move(tempFilePath, _settingsFilePath, overwrite: true);
            
            _currentSettings = settings;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка при сохранении настроек: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Получить текущие настройки (загружает из файла, если еще не загружены)
    /// </summary>
    public AppSettings GetSettings()
    {
        return LoadSettings();
    }

    /// <summary>
    /// Сбросить настройки к значениям по умолчанию
    /// </summary>
    public void ResetToDefaults()
    {
        _currentSettings = new AppSettings();
        SaveSettings(_currentSettings);
    }
}

