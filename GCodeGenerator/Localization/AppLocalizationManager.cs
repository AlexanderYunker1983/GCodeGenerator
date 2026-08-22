using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace GCodeGenerator.Localization
{
    /// <summary>
    /// Менеджер локализации приложения (пункт 1.3 плана): замена
    /// <c>MugenLocalizationManager</c> без зависимости от Mugen Binding.
    /// Реализует <see cref="INotifyPropertyChanged"/> и уведомляет об изменении
    /// при смене культуры, чтобы XAML-привязки <c>{loc:Loc Key}</c> обновлялись.
    /// </summary>
    public class AppLocalizationManager : LocalizationManager, INotifyPropertyChanged
    {
        public override void ChangeCulture(CultureInfo cultureInfo)
        {
            base.ChangeCulture(cultureInfo);
            OnPropertyChanged(null);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
