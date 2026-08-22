using System;
using System.Windows;
using Autofac;
using GCodeGenerator.ViewModels;
using Microsoft.Win32;

namespace GCodeGenerator.Services
{
    /// <summary>
    /// WPF-реализация <see cref="IDialogService"/> (пункт 1.3 плана):
    /// MessageBox/FileDialog и показ диалоговых view-моделей как модальных окон.
    /// </summary>
    public class WpfDialogService : IDialogService
    {
        private readonly ILifetimeScope _scope;

        public WpfDialogService(ILifetimeScope scope)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        }

        public void ShowInfo(string message, string title = "")
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public void ShowError(string message, string title = "")
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public bool ShowConfirm(string message, string title = "")
        {
            return MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning)
                   == MessageBoxResult.Yes;
        }

        public string ShowOpenDialog(string title, string filter, string defaultExtension = "")
        {
            var dialog = new OpenFileDialog
            {
                Title = title,
                Filter = filter,
                DefaultExt = defaultExtension
            };
            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        public string ShowSaveDialog(string title, string filter, string defaultExtension = "", string fileName = "")
        {
            var dialog = new SaveFileDialog
            {
                Title = title,
                Filter = filter,
                DefaultExt = defaultExtension,
                FileName = fileName
            };
            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        public TViewModel CreateViewModel<TViewModel>() where TViewModel : class
        {
            return _scope.Resolve<TViewModel>();
        }

        public void ShowDialog<TViewModel>(TViewModel viewModel) where TViewModel : class
        {
            if (viewModel == null) throw new ArgumentNullException(nameof(viewModel));

            var window = (Window)Activator.CreateInstance(GetViewType(typeof(TViewModel)));
            window.DataContext = viewModel;
            window.Owner = Application.Current?.MainWindow;
            // Модальный показ: блокирует до закрытия окна.
            window.ShowDialog();
            (viewModel as CloseableViewModel)?.OnClosed();
        }

        /// <summary>
        /// Конвенция соответствия view-модели и окна:
        /// <c>GCodeGenerator.ViewModels[.Sub].XxxViewModel → GCodeGenerator.Views[.Sub].XxxView</c>.
        /// </summary>
        private static Type GetViewType(Type viewModelType)
        {
            var vmNamespace = viewModelType.Namespace ?? string.Empty;
            var viewNamespace = vmNamespace.Replace(".ViewModels", ".Views");
            const string suffix = "ViewModel";
            var baseName = viewModelType.Name.EndsWith(suffix)
                ? viewModelType.Name.Substring(0, viewModelType.Name.Length - suffix.Length)
                : viewModelType.Name;
            var viewName = baseName + "View";
            var viewType = Type.GetType($"{viewNamespace}.{viewName}");
            if (viewType == null)
                throw new InvalidOperationException(
                    $"Не найден тип окна для view-модели {viewModelType.FullName} (ожидался {viewNamespace}.{viewName}).");
            return viewType;
        }
    }
}
