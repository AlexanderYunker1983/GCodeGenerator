using System;
using System.Globalization;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GCodeGenerator.Core.Localization;

/// <summary>
/// Сервис для управления локализацией с поддержкой переключения языка на лету
/// </summary>
public class LocalizationService : INotifyPropertyChanged
{
    private static LocalizationService? _instance;
    private CultureInfo? _currentCulture;

    private LocalizationService()
    {
        // Инициализация культуры по умолчанию
        _currentCulture = null; // null означает использование системной культуры
    }

    /// <summary>
    /// Единственный экземпляр сервиса (Singleton)
    /// </summary>
    public static LocalizationService Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new LocalizationService();
            }
            return _instance;
        }
    }

    /// <summary>
    /// Текущая культура. null означает использование системной культуры
    /// </summary>
    public CultureInfo? CurrentCulture
    {
        get => _currentCulture;
        set
        {
            if (_currentCulture != value)
            {
                _currentCulture = value;
                Resources.Culture = value;
                OnPropertyChanged();
                OnCultureChanged();
            }
        }
    }

    /// <summary>
    /// Событие изменения культуры
    /// </summary>
    public event EventHandler? CultureChanged;

    /// <summary>
    /// Событие изменения свойства (для INotifyPropertyChanged)
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected virtual void OnCultureChanged()
    {
        CultureChanged?.Invoke(this, EventArgs.Empty);
    }
}

