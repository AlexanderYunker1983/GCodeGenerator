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
    private int _selectedLanguageIndex = ArrayHelper.DefaultIndex;

    [ObservableProperty]
    private int _selectedThemeIndex = ArrayHelper.DefaultIndex;

    public Language[] Languages { get; } = Enum.GetValues<Language>();
    public Theme[] Themes { get; } = Enum.GetValues<Theme>();

    /// <summary>
    /// Загрузить настройки из файла
    /// </summary>
    private void LoadSettings()
    {
        var settings = SettingsService.Instance.LoadSettings();
        
        SelectedLanguageIndex = ArrayHelper.FindValidIndex(Languages, settings.Language);
        SelectedThemeIndex = ArrayHelper.FindValidIndex(Themes, settings.Theme);
    }

    [RelayCommand]
    private void Apply()
    {
        if (!ArrayHelper.AreIndicesValid(
            (SelectedLanguageIndex, Languages.Length),
            (SelectedThemeIndex, Themes.Length)))
            return;
        
        var languageIndex = SelectedLanguageIndex;
        var themeIndex = SelectedThemeIndex;
        
        ArrayHelper.NormalizeIndices(
            ref languageIndex, Languages.Length,
            ref themeIndex, Themes.Length);
        
        SelectedLanguageIndex = languageIndex;
        SelectedThemeIndex = themeIndex;
        
        var selectedLanguage = Languages[SelectedLanguageIndex];
        var selectedTheme = Themes[SelectedThemeIndex];
        
        ApplyLanguage(selectedLanguage);
        ApplyTheme(selectedTheme);
        
        SaveSettings(selectedLanguage, selectedTheme);
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

    /// <summary>
    /// Сохраняет настройки в файл
    /// </summary>
    private void SaveSettings(Language language, Theme theme)
    {
        var settings = new AppSettings
        {
            Language = language,
            Theme = theme
        };

        SettingsService.Instance.SaveSettings(settings);
    }
}

