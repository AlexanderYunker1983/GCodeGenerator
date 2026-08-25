#nullable enable
namespace GCodeGenerator.Services
{
    /// <summary>
    /// Показ view-модели как модального окна.
    ///
    /// Прежде тот же контракт умел ещё и создавать view-модели по типу,
    /// то есть был доступом к контейнеру: по нему нельзя было понять, какие
    /// окна открывает view-модель, и подменить их в тесте. Теперь view-модель
    /// получает фабрику именно того окна, которое открывает, а здесь остаётся
    /// только показ.
    /// </summary>
    public interface IDialogHost
    {
        /// <summary>
        /// Показывает view-модель как модальное диалоговое окно (окно ищется
        /// по конвенции <c>XxxViewModel → XxxView</c>). Блокирует до закрытия
        /// окна; после закрытия вызывает <c>CloseableViewModel.OnClosed()</c>.
        /// </summary>
        void ShowDialog(object viewModel);
    }
}
