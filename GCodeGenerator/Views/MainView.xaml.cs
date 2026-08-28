#nullable enable
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using GCodeGenerator.ViewModels;

namespace GCodeGenerator.Views
{
    /// <summary>
    /// Interaction logic for MainView.xaml
    /// </summary>
    public partial class MainView : Window
    {
        /// <summary>Расширение файла проекта — единственное, что окно принимает.</summary>
        private const string ProjectExtension = ".ygc";

        public MainView()
        {
            InitializeComponent();
            Closing += OnClosing;
            DragOver += OnDragOver;
            Drop += OnDrop;
        }

        /// <summary>
        /// Курсор над окном показывает, примут ли перетаскиваемый файл:
        /// проект — да, всё остальное — нет.
        /// </summary>
        private static void OnDragOver(object? sender, DragEventArgs e)
        {
            e.Effects = ProjectFileFrom(e.Data) != null ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        /// <summary>
        /// Файл, брошенный в окно, открывается как проект. Вопрос о
        /// несохранённых изменениях задаёт сама операция открытия.
        /// </summary>
        private void OnDrop(object? sender, DragEventArgs e)
        {
            var fileName = ProjectFileFrom(e.Data);
            if (fileName == null || DataContext is not MainViewModel vm)
                return;

            e.Handled = true;
            _ = vm.OpenProjectAsync(fileName);
        }

        /// <summary>
        /// Путь к файлу проекта среди перетаскиваемых данных; <c>null</c>,
        /// если это не он. Из нескольких файлов берётся первый подходящий:
        /// открыть можно только один проект, и молча открыть «какой-то» из
        /// набора хуже, чем открыть тот, что назван первым.
        /// </summary>
        /// <param name="data">Перетаскиваемые данные.</param>
        internal static string? ProjectFileFrom(IDataObject? data)
        {
            if (data?.GetDataPresent(DataFormats.FileDrop) != true)
                return null;

            if (data.GetData(DataFormats.FileDrop) is not string[] files)
                return null;

            foreach (var file in files)
            {
                if (file != null && file.EndsWith(ProjectExtension, System.StringComparison.OrdinalIgnoreCase))
                    return file;
            }

            return null;
        }

        /// <summary>
        /// Закрытие окна спрашивает о несохранённом проекте: до этого любое
        /// закрытие — крестиком, Alt+F4, завершением сеанса — молча теряло
        /// работу.
        /// </summary>
        private void OnClosing(object? sender, CancelEventArgs e)
        {
            if (DataContext is MainViewModel vm && !vm.ConfirmClose())
                e.Cancel = true;
        }

        private void OperationsList_MouseDoubleClick(object? sender, MouseButtonEventArgs e)
        {
            if (DataContext is MainViewModel vm &&
                vm.OperationsWorkspace.EditOperationCommand?.CanExecute(null) == true)
            {
                vm.OperationsWorkspace.EditOperationCommand.Execute(null);
            }
        }
    }
}