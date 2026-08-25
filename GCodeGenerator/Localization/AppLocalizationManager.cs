using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using GCodeGenerator.Diagnostics;

namespace GCodeGenerator.Localization
{
    /// <summary>
    /// Менеджер локализации приложения (пункт 1.3 плана): замена
    /// <c>MugenLocalizationManager</c> без зависимости от Mugen Binding.
    /// Реализует <see cref="INotifyPropertyChanged"/> и уведомляет об изменении
    /// при смене культуры, чтобы XAML-привязки <c>{loc:Loc Key}</c> обновлялись.
    ///
    /// Отсутствующий ключ пишется в журнал приложения: базовый класс из ядра
    /// умеет только <c>Debug.WriteLine</c>, который в релизной сборке невидим.
    /// </summary>
    public class AppLocalizationManager : LocalizationManager, INotifyPropertyChanged
    {
        private readonly IAppLogger _logger;

        /// <summary>Менеджер без журнала (конструктор для XAML-дизайнера и тестов).</summary>
        public AppLocalizationManager()
            : this(NullAppLogger.Instance)
        {
        }

        /// <summary>Менеджер, сообщающий об отсутствующих ключах в журнал приложения.</summary>
        /// <param name="logger">Журнал приложения.</param>
        public AppLocalizationManager(IAppLogger logger)
        {
            _logger = logger ?? NullAppLogger.Instance;
        }

        /// <inheritdoc />
        public override void ChangeCulture(CultureInfo cultureInfo)
        {
            base.ChangeCulture(cultureInfo);
            OnPropertyChanged(null);
        }

        /// <inheritdoc />
        protected override void LogMissingKey(string key)
        {
            base.LogMissingKey(key);
            _logger.Warning($"Отсутствует ключ локализации: {key}");
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
