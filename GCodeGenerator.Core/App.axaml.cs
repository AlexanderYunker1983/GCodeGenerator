using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using GCodeGenerator.Core.Helpers;
using GCodeGenerator.Core.Localization;
using GCodeGenerator.Core.Settings;
using GCodeGenerator.Core.ViewModels.MainViewModel;

namespace GCodeGenerator.Core;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();
            
            // Загружаем и применяем сохраненные настройки ПЕРЕД созданием главного окна
            // Это гарантирует, что язык и тема применятся до отображения UI
            var settings = SettingsService.Instance.LoadSettings();
            
            // Применяем язык сразу, чтобы локализованные строки загрузились правильно
            ApplyLanguage(settings.Language);
            
            // Создаем главное окно через ViewModel и extension метод
            var mainViewModel = WindowHelper.GetViewModel<MainViewModel>();
            mainViewModel.Show();
            
            // Применяем тему после создания окна через Dispatcher, чтобы гарантировать применение
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                ApplyTheme(settings.Theme);
            }, Avalonia.Threading.DispatcherPriority.Loaded);
        }

        base.OnFrameworkInitializationCompleted();
    }


    /// <summary>
    /// Применяет выбранный язык
    /// </summary>
    private void ApplyLanguage(Enums.Language language)
    {
        var culture = language switch
        {
            Enums.Language.System => null,
            Enums.Language.English => new System.Globalization.CultureInfo("en"),
            Enums.Language.Russian => new System.Globalization.CultureInfo("ru"),
            _ => null
        };
        
        LocalizationService.Instance.CurrentCulture = culture;
    }

    /// <summary>
    /// Применяет выбранную тему
    /// </summary>
    private void ApplyTheme(Enums.Theme theme)
    {
        if (Application.Current == null)
            return;

        var themeVariant = theme switch
        {
            Enums.Theme.System => Avalonia.Styling.ThemeVariant.Default,
            Enums.Theme.Light => Avalonia.Styling.ThemeVariant.Light,
            Enums.Theme.Dark => Avalonia.Styling.ThemeVariant.Dark,
            _ => Avalonia.Styling.ThemeVariant.Default
        };

        Application.Current.RequestedThemeVariant = themeVariant;
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}