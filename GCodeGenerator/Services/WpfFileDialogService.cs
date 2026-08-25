using Microsoft.Win32;

namespace GCodeGenerator.Services
{
    /// <summary>WPF-реализация <see cref="IFileDialogService"/>: стандартные диалоги файлов.</summary>
    public class WpfFileDialogService : IFileDialogService
    {
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
    }
}
