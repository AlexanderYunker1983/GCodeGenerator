using System;
using System.Windows;
using GCodeGenerator.ViewModels;

namespace GCodeGenerator.Services
{
    /// <summary>
    /// WPF-реализация <see cref="IDialogHost"/>: показывает окно, найденное
    /// по имени view-модели, и блокирует до его закрытия.
    /// </summary>
    public class WpfDialogHost : IDialogHost
    {
        public void ShowDialog(object viewModel)
        {
            if (viewModel == null) throw new ArgumentNullException(nameof(viewModel));

            var window = (Window)Activator.CreateInstance(GetViewType(viewModel.GetType()));
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
