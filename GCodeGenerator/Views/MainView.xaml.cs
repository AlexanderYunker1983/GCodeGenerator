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
                vm.DrillOperations != null &&
                vm.DrillOperations.EditOperationCommand != null &&
                vm.DrillOperations.EditOperationCommand.CanExecute(null))
            {
                vm.DrillOperations.EditOperationCommand.Execute(null);
            }
        }
    }
}