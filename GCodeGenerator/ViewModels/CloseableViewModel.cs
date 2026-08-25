#nullable enable
using System;

namespace GCodeGenerator.ViewModels
{
    /// <summary>
    /// Базовый класс view-моделей диалогов (пункт 1.3 плана): замена
    /// <c>MugenMvvmToolkit.ViewModels.CloseableViewModel</c>.
    /// <see cref="OnClosed"/> вызывается <c>IDialogHost</c> после закрытия
    /// диалогового окна (до этого — в момент, когда Mugen вызывал
    /// <c>OnClosed(IDataContext)</c> при закрытии окна). Параметр <c>IDataContext</c>
    /// удалён: он не использовался ни в одном из диалоговых VM.
    ///
    /// Пункт 7.3: <see cref="RequestClose"/> — VM запрашивает закрытие окна
    /// (кнопки OK/Cancel); <c>IDialogHost</c> подписывается на
    /// <see cref="CloseRequested"/> и закрывает окно.
    /// </summary>
    public class CloseableViewModel : ViewModelBase
    {
        /// <summary>
        /// Запрос закрытия диалогового окна из VM (кнопки OK/Cancel, пункт 7.3 плана).
        /// Подписывается <c>IDialogHost</c> при показе диалога.
        /// </summary>
        public event Action? CloseRequested;

        /// <summary>Запрашивает закрытие диалогового окна (пункт 7.3 плана).</summary>
        public void RequestClose()
        {
            CloseRequested?.Invoke();
        }

        /// <summary>
        /// Вызывается при закрытии диалогового окна. Переопределяется в диалоговых VM
        /// для сохранения изменений (аналог Mugen <c>OnClosed</c>).
        /// </summary>
        public virtual void OnClosed()
        {
        }
    }
}
