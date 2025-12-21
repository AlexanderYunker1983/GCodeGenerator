using System;
using System.Linq;
using GCodeGenerator.Core.Attributes;
using GCodeGenerator.Core.Helpers;
using GCodeGenerator.Core.Localization;

namespace GCodeGenerator.Core.ViewModels.SettingsViewModel;

public partial class SettingsViewModel
{
    /// <summary>
    /// Переопределяем обработчик изменения культуры для сохранения и восстановления индексов ComboBox
    /// </summary>
    protected override void OnCultureChanged(object? sender, EventArgs e)
    {
        var savedLanguageIndex = SelectedLanguageIndex;
        var savedThemeIndex = SelectedThemeIndex;

        base.OnCultureChanged(sender, e);

        // Восстанавливаем индексы в следующем кадре UI, после обновления ItemsSource
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (ArrayHelper.IsValidIndex(savedLanguageIndex, LanguageStrings.Length))
                SelectedLanguageIndex = savedLanguageIndex;
            
            if (ArrayHelper.IsValidIndex(savedThemeIndex, ThemeStrings.Length))
                SelectedThemeIndex = savedThemeIndex;
        }, Avalonia.Threading.DispatcherPriority.Background);
    }

    [Localized]
    public string DisplayName => Resources.GetString("Settings_DisplayName");
    
    [Localized]
    public string LanguageLabel => Resources.GetString("Settings_Language");
    
    [Localized]
    public string ThemeLabel => Resources.GetString("Settings_Theme");
    
    [Localized]
    public string ApplyButtonText => Resources.GetString("Settings_Apply");
    
    [Localized]
    public string CancelButtonText => Resources.GetString("Settings_Cancel");

    [Localized]
    public string[] LanguageStrings => Languages.Select(lang => Resources.GetEnumString(lang)).ToArray();
    
    [Localized]
    public string[] ThemeStrings => Themes.Select(theme => Resources.GetEnumString(theme)).ToArray();
}

