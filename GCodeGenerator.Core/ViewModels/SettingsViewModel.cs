using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GCodeGenerator.Core.Helpers;
using GCodeGenerator.Core.Interfaces;

namespace GCodeGenerator.Core.ViewModels;

public partial class SettingsViewModel : ViewModelBase, IHasDisplayName
{
    public string DisplayName => "Настройки приложения";
    
    [ObservableProperty]
    private string _selectedLanguage = "Системный";

    [ObservableProperty]
    private string _selectedTheme = "Системная";

    public string[] Languages { get; } = { "Системный", "Английский", "Русский" };
    public string[] Themes { get; } = { "Системная", "Светлая", "Темная" };

    [RelayCommand]
    private void Apply()
    {
        // TODO: Применить настройки
        this.Close();
    }

    [RelayCommand]
    private void Cancel()
    {
        this.Close();
    }
}

