using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using GCodeGenerator.Core.Interfaces;
using GCodeGenerator.Core.ViewModels;

namespace GCodeGenerator.Core.Helpers;

public static class WindowHelper
{
    /// <summary>
    /// Создает экземпляр ViewModel указанного типа
    /// </summary>
    public static T GetViewModel<T>() where T : ViewModelBase, new()
    {
        return new T();
    }

    /// <summary>
    /// Создает View для ViewModel через ViewLocator и показывает его как главное окно приложения
    /// </summary>
    public static void Show(this ViewModelBase viewModel)
    {
        if (viewModel == null)
            throw new ArgumentNullException(nameof(viewModel));

        var viewLocator = new ViewLocator();
        var view = viewLocator.Build(viewModel);

        if (view is UserControl userControl)
        {
            // Создаем окно-обертку для UserControl
            var window = new Window
            {
                Content = userControl,
                DataContext = viewModel,
                Title = GetWindowTitle(viewModel),
                Width = 800,
                Height = 450,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };
            
            // Устанавливаем иконку, если это главное окно
            try
            {
                var iconStream = AssetLoader.Open(new Uri("avares://GCodeGenerator.Core/Assets/avalonia-logo.ico"));
                window.Icon = new WindowIcon(iconStream);
            }
            catch
            {
                // Игнорируем ошибку загрузки иконки
            }

            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = window;
                window.Show();
            }
            else
            {
                window.Show();
            }
        }
        else if (view is Window window)
        {
            window.DataContext = viewModel;

            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = window;
                window.Show();
            }
            else
            {
                window.Show();
            }
        }
        else
        {
            throw new InvalidOperationException($"View для ViewModel {viewModel.GetType().Name} не является UserControl или Window");
        }
    }

    /// <summary>
    /// Создает View для ViewModel через ViewLocator и показывает его как диалог
    /// </summary>
    public static void ShowDialog(this ViewModelBase viewModel)
    {
        if (viewModel == null)
            throw new ArgumentNullException(nameof(viewModel));

        var viewLocator = new ViewLocator();
        var view = viewLocator.Build(viewModel);

        if (view is UserControl userControl)
        {
            // Создаем окно-обертку для UserControl
            var window = new Window
            {
                Content = userControl,
                DataContext = viewModel,
                Title = GetWindowTitle(viewModel),
                Width = 500,
                Height = 300,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false
            };

            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                && desktop.MainWindow is not null)
            {
                window.ShowDialog(desktop.MainWindow);
            }
            else
            {
                window.Show();
            }
        }
        else if (view is Window window)
        {
            window.DataContext = viewModel;

            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                && desktop.MainWindow is not null)
            {
                window.ShowDialog(desktop.MainWindow);
            }
            else
            {
                window.Show();
            }
        }
        else
        {
            throw new InvalidOperationException($"View для ViewModel {viewModel.GetType().Name} не является UserControl или Window");
        }
    }

    /// <summary>
    /// Закрывает окно, связанное с данной ViewModel
    /// </summary>
    public static void Close(this ViewModelBase viewModel)
    {
        if (viewModel == null)
            throw new ArgumentNullException(nameof(viewModel));

        // Находим окно через ApplicationLifetime
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = desktop.Windows.FirstOrDefault(w => w.DataContext == viewModel);
            window?.Close();
        }
    }

    private static string GetWindowTitle(ViewModelBase viewModel)
    {
        // Если ViewModel реализует IHasDisplayName, используем DisplayName
        if (viewModel is IHasDisplayName hasDisplayName)
        {
            return hasDisplayName.DisplayName;
        }

        // Иначе используем название класса ViewModel
        var typeName = viewModel.GetType().Name;
        return typeName.EndsWith("ViewModel") 
            ? typeName.Substring(0, typeName.Length - "ViewModel".Length) 
            : typeName;
    }
}

