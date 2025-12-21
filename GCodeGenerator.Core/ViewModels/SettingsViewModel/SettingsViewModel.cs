using System;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GCodeGenerator.Core.Enums;
using GCodeGenerator.Core.Helpers;
using GCodeGenerator.Core.Interfaces;
using GCodeGenerator.Core.Localization;

namespace GCodeGenerator.Core.ViewModels.SettingsViewModel;

public partial class SettingsViewModel : ViewModelBase, IHasDisplayName
{
    public SettingsViewModel()
    {
        InitializeResources();
    }

    [ObservableProperty]
    private int _selectedLanguageIndex = 0;

    [ObservableProperty]
    private int _selectedThemeIndex = 0;

    public Language[] Languages { get; } = Enum.GetValues<Language>();
    public Theme[] Themes { get; } = Enum.GetValues<Theme>();

    [RelayCommand]
    private void Apply()
    {
        // Применяем выбранный язык
        var selectedLanguage = Languages[SelectedLanguageIndex];
        ApplyLanguage(selectedLanguage);
        
        // Применяем выбранную тему
        var selectedTheme = Themes[SelectedThemeIndex];
        ApplyTheme(selectedTheme);
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

