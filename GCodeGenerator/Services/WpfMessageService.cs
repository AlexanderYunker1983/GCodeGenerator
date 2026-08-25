#nullable enable
using System.Windows;

namespace GCodeGenerator.Services
{
    /// <summary>WPF-реализация <see cref="IMessageService"/>: MessageBox.</summary>
    public class WpfMessageService : IMessageService
    {
        public void ShowInfo(string message, string title = "")
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public void ShowError(string message, string title = "")
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public SaveConfirmation ShowSaveConfirmation(string message, string title = "")
        {
            var answer = MessageBox.Show(
                message, title, MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
            switch (answer)
            {
                case MessageBoxResult.Yes:
                    return SaveConfirmation.Save;
                case MessageBoxResult.No:
                    return SaveConfirmation.Discard;
                default:
                    // Закрытие окна вопроса крестиком или Esc — то же, что «Отмена»:
                    // непонятый ответ не должен стоить пользователю работы.
                    return SaveConfirmation.Cancel;
            }
        }
    }
}
