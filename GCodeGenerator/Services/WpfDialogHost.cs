using System;
using System.Windows;
using GCodeGenerator.ViewModels;
using GCodeGenerator.Views;

namespace GCodeGenerator.Services
{
    /// <summary>
    /// WPF-реализация <see cref="IDialogHost"/>: показывает окно, найденное
    /// для view-модели в <see cref="DialogViewRegistry"/>, и блокирует
    /// до его закрытия.
    /// </summary>
    public class WpfDialogHost : IDialogHost
    {
        public void ShowDialog(object viewModel)
        {
            if (viewModel == null) throw new ArgumentNullException(nameof(viewModel));

            var window = (Window)Activator.CreateInstance(DialogViewRegistry.ViewFor(viewModel.GetType()));
            window.DataContext = viewModel;
            window.Owner = Application.Current?.MainWindow;

            var closeable = viewModel as CloseableViewModel;
            Action closeHandler = null;
            if (closeable != null)
            {
                // Пункт 7.3 плана: VM может запросить закрытие окна (OK/Cancel).
                closeHandler = () => window.Close();
                closeable.CloseRequested += closeHandler;
            }

            // Модальный показ: блокирует до закрытия окна.
            window.ShowDialog();

            if (closeable != null)
            {
                closeable.CloseRequested -= closeHandler;
                closeable.OnClosed();
            }
        }
    }
}
