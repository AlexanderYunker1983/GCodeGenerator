using System;

namespace GCodeGenerator.Services
{
    /// <summary>
    /// Ответ на вопрос о несохранённых изменениях.
    /// </summary>
    public enum SaveConfirmation
    {
        /// <summary>Сохранить изменения и продолжить.</summary>
        Save,

        /// <summary>Продолжить, потеряв изменения.</summary>
        Discard,

        /// <summary>Ничего не делать: действие отменено.</summary>
        Cancel
    }

    /// <summary>
    /// Сервис диалогов (пункт 1.3 плана): заменяет в view-моделях
    /// Mugen <c>GetViewModel&lt;T&gt;()</c> + <c>ShowAsync()</c> (диалоговые окна VM)
    /// и прямые <c>MessageBox</c>/<c>OpenFileDialog</c>/<c>SaveFileDialog</c>.
    /// Реализация — <see cref="WpfDialogService"/> (WPF).
    /// </summary>
    public interface IDialogService
    {
        /// <summary>Информационное сообщение (кнопка OK).</summary>
        void ShowInfo(string message, string title = "");

        /// <summary>Сообщение об ошибке (кнопка OK, иконка Error).</summary>
        void ShowError(string message, string title = "");

        /// <summary>Подтверждение (кнопки Да/Нет, иконка Warning). true — «Да».</summary>
        bool ShowConfirm(string message, string title = "");

        /// <summary>
        /// Вопрос о несохранённых изменениях (Да/Нет/Отмена): сохранить,
        /// потерять изменения или отменить само действие.
        /// </summary>
        SaveConfirmation ShowSaveConfirmation(string message, string title = "");

        /// <summary>Диалог открытия файла. Возвращает путь или null, если отменено.</summary>
        string ShowOpenDialog(string title, string filter, string defaultExtension = "");

        /// <summary>Диалог сохранения файла. Возвращает путь или null, если отменено.</summary>
        string ShowSaveDialog(string title, string filter, string defaultExtension = "", string fileName = "");

        /// <summary>Создаёт view-модель диалога через IoC (Autofac).</summary>
        TViewModel CreateViewModel<TViewModel>() where TViewModel : class;

        /// <summary>Создаёт view-модель диалога по типу через IoC (Autofac).</summary>
        object CreateViewModel(Type viewModelType);

        /// <summary>
        /// Показывает view-модель как модальное диалоговое окно (view ищется по конвенции
        /// <c>XxxViewModel → XxxView</c>). Блокирует до закрытия окна; после закрытия
        /// вызывает <c>CloseableViewModel.OnClosed()</c>.
        /// </summary>
        void ShowDialog<TViewModel>(TViewModel viewModel) where TViewModel : class;

        /// <summary>Показывает view-модель как модальное диалоговое окно по типу (см. <see cref="ShowDialog{TViewModel}"/>).</summary>
        void ShowDialog(Type viewModelType, object viewModel);
    }
}
