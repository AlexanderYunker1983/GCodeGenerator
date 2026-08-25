#nullable enable
using CommunityToolkit.Mvvm.ComponentModel;

namespace GCodeGenerator.ViewModels
{
    /// <summary>
    /// Базовый класс view-моделей (пункт 1.3 плана): замена
    /// <c>MugenMvvmToolkit.ViewModels.ViewModelBase</c>.
    /// Наследуется от <see cref="ObservableObject"/> (CommunityToolkit.Mvvm) —
    /// предоставляет <c>INotifyPropertyChanged</c>. Безаргументный
    /// <c>OnPropertyChanged()</c> (с <c>[CallerMemberName]</c>) работает так же, как
    /// в Mugen, поэтому объявления свойств в VM не меняются.
    /// </summary>
    public class ViewModelBase : ObservableObject
    {
    }
}
