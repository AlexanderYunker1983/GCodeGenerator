using System;

namespace GCodeGenerator.Localization
{
    /// <summary>
    /// Статический доступ к менеджеру локализации для XAML-привязок (пункт 1.3 плана).
    /// Заполняется при инициализации (composition root) до загрузки окон.
    ///
    /// Здесь же смена языка доводится до разметки: менеджер сообщает о смене
    /// культуры, а источник строк — привязкам, которые перечитывают надписи.
    /// </summary>
    public static class LocalizationProvider
    {
        private static ILocalizationManager _instance;

        public static ILocalizationManager Instance
        {
            get => _instance;
            set
            {
                if (ReferenceEquals(_instance, value))
                    return;

                if (_instance != null)
                    _instance.CultureChanged -= OnCultureChanged;

                _instance = value;

                if (_instance != null)
                    _instance.CultureChanged += OnCultureChanged;

                LocalizationSource.Instance.Refresh();
            }
        }

        private static void OnCultureChanged(object sender, EventArgs e)
            => LocalizationSource.Instance.Refresh();
    }
}
