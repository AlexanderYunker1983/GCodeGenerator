using System;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GCodeGenerator.Core.Enums;
using GCodeGenerator.Core.Helpers;
using GCodeGenerator.Core.Interfaces;
using GCodeGenerator.Core.Localization;
using GCodeGenerator.Core.Settings;
using Theme = GCodeGenerator.Core.Enums.Theme;

namespace GCodeGenerator.Core.ViewModels.SettingsViewModel;

public partial class SettingsViewModel : ViewModelBase, IHasDisplayName
{
    public SettingsViewModel()
    {
        InitializeResources();
        LoadSettings();
    }

    [ObservableProperty]
    private int _selectedLanguageIndex = 0;

    [ObservableProperty]
    private int _selectedThemeIndex = 0;

    public Language[] Languages { get; } = Enum.GetValues<Language>();
    public Theme[] Themes { get; } = Enum.GetValues<Theme>();

    /// <summary>
    /// Загрузить настройки из файла
    /// </summary>
    private void LoadSettings()
    {
        var settings = SettingsService.Instance.LoadSettings();
        
        // Устанавливаем индекс языка с проверкой границ
        if (Languages.Length > 0)
        {
            SelectedLanguageIndex = Array.IndexOf(Languages, settings.Language);
            if (SelectedLanguageIndex < 0 || SelectedLanguageIndex >= Languages.Length)
                SelectedLanguageIndex = 0;
        }
        else
        {
            SelectedLanguageIndex = 0;
        }
        
        // Устанавливаем индекс темы с проверкой границ
        if (Themes.Length > 0)
        {
            SelectedThemeIndex = Array.IndexOf(Themes, settings.Theme);
            if (SelectedThemeIndex < 0 || SelectedThemeIndex >= Themes.Length)
                SelectedThemeIndex = 0;
        }
        else
        {
            SelectedThemeIndex = 0;
        }
    }

    [RelayCommand]
    private void Apply()
    {
        // Проверяем валидность индексов перед применением
        if (Languages.Length == 0 || Themes.Length == 0)
            return;
        
        // Нормализуем индексы, если они выходят за границы
        if (SelectedLanguageIndex < 0 || SelectedLanguageIndex >= Languages.Length)
            SelectedLanguageIndex = 0;
        
        if (SelectedThemeIndex < 0 || SelectedThemeIndex >= Themes.Length)
            SelectedThemeIndex = 0;
        
        // Применяем выбранный язык и тему
        var selectedLanguage = Languages[SelectedLanguageIndex];
        var selectedTheme = Themes[SelectedThemeIndex];
        ApplyLanguage(selectedLanguage);
        ApplyTheme(selectedTheme);
        var settings = new AppSettings
        {
            Language = selectedLanguage,
            Theme = selectedTheme
        };

        SettingsService.Instance.SaveSettings(settings);
    }

    private void ApplyLanguage(Language language)
    {
        var culture = language switch
        {
            Language.System => null, // null означает системную культуру
            Language.English => new System.Globalization.CultureInfo("en"),
            Language.Russian => new System.Globalization.CultureInfo("ru"),
            _ => null
        };
        
        LocalizationService.Instance.CurrentCulture = culture;
    }

    private void ApplyTheme(Theme theme)
    {
        if (Application.Current == null)
            return;

        var themeVariant = theme switch
        {
            Theme.System => Avalonia.Styling.ThemeVariant.Default, // Default следует системной теме
            Theme.Light => Avalonia.Styling.ThemeVariant.Light,
            Theme.Dark => Avalonia.Styling.ThemeVariant.Dark,
            _ => Avalonia.Styling.ThemeVariant.Default
        };

        Application.Current.RequestedThemeVariant = themeVariant;
    }

    [RelayCommand]
    private void Cancel()
    {
        this.Close();
    }
}

