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
        public MainView()
        {
            InitializeComponent();
            Closing += OnClosing;
        }

        /// <summary>
        /// Закрытие окна спрашивает о несохранённом проекте: до этого любое
        /// закрытие — крестиком, Alt+F4, завершением сеанса — молча теряло
        /// работу.
        /// </summary>
        private void OnClosing(object sender, CancelEventArgs e)
        {
            if (DataContext is MainViewModel vm && !vm.ConfirmClose())
                e.Cancel = true;
        }

        private void OperationsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is MainViewModel vm &&
                vm.EditOperationCommand != null &&
                vm.EditOperationCommand.CanExecute(null))
            {
                vm.EditOperationCommand.Execute(null);
            }
        }
    }
}