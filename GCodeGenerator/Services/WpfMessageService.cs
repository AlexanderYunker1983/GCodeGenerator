#nullable enable
using System.Windows;

namespace GCodeGenerator.Services
{
    /// <summary>WPF-реализация <see cref="IMessageService"/>: MessageBox.</summary>
    public class WpfMessageService : IMessageService
    {
        public void ShowInfo(string message, string title = "")
        {
            Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public void ShowError(string message, string title = "")
        {
            Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public SaveConfirmation ShowSaveConfirmation(string message, string title = "")
        {
            var answer = Show(message, title, MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
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

        /// <summary>
        /// Сообщение с окном-владельцем: без владельца MessageBox центрируется
        /// по экрану, не связывается с главным окном в переключателе задач
        /// и может уйти под него. До первого окна (сбой на запуске) владельца
        /// нет — тогда сообщение показывается как раньше.
        /// </summary>
        private static MessageBoxResult Show(
            string message, string title, MessageBoxButton button, MessageBoxImage image)
        {
            var owner = Application.Current?.MainWindow;
            return owner is { IsLoaded: true }
                ? MessageBox.Show(owner, message, title, button, image)
                : MessageBox.Show(message, title, button, image);
        }
    }
}
