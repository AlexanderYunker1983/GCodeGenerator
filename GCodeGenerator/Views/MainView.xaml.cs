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