namespace GCodeGenerator.ViewModels
{
    /// <summary>
    /// Базовый класс view-моделей диалогов (пункт 1.3 плана): замена
    /// <c>MugenMvvmToolkit.ViewModels.CloseableViewModel</c>.
    /// <see cref="OnClosed"/> вызывается <c>IDialogService</c> после закрытия
    /// диалогового окна (до этого — в момент, когда Mugen вызывал
    /// <c>OnClosed(IDataContext)</c> при закрытии окна). Параметр <c>IDataContext</c>
    /// удалён: он не использовался ни в одном из диалоговых VM.
    /// </summary>
    public class CloseableViewModel : ViewModelBase
    {
        /// <summary>
        /// Вызывается при закрытии диалогового окна. Переопределяется в диалоговых VM
        /// для сохранения изменений (аналог Mugen <c>OnClosed</c>).
        /// </summary>
        public virtual void OnClosed()
        {
        }
    }
}
