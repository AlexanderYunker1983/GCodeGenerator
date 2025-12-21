using System;
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
        
        // TODO: Применить тему
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

    [RelayCommand]
    private void Cancel()
    {
        this.Close();
    }
}

