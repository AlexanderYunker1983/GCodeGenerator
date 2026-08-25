using System;
using System.Windows.Data;
using System.Windows.Markup;

namespace GCodeGenerator.Localization
{
    /// <summary>
    /// XAML-расширение для локализации (пункт 1.3 плана): замена Mugen Binding
    /// <c>{DataBinding '$i18n.Key'}</c> на <c>{loc:Loc Key}</c>.
    ///
    /// Возвращает привязку к <see cref="LocalizationSource"/>, а не готовую
    /// строку. Прежде строка подставлялась один раз при загрузке разметки —
    /// в комментарии это объяснялось зависанием, которое вызывала нативная
    /// привязка на прежнем интерфейсном стеке (Mugen). Стека давно нет,
    /// а разовая подстановка мешает: язык нельзя сменить, не перезапустив
    /// программу. Теперь надписи перечитываются, как только меняется язык.
    /// </summary>
    public class LocExtension : MarkupExtension
    {
        private readonly string _key;

        public LocExtension(string key)
        {
            _key = key ?? string.Empty;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (string.IsNullOrEmpty(_key))
                return _key;

            var binding = new Binding($"[{_key}]")
            {
                Source = LocalizationSource.Instance,
                Mode = BindingMode.OneWay,
            };

            // Привязка годится не везде: в свойство, не являющееся свойством
            // зависимости, WPF её не примет. ProvideValue самой привязки
            // решает это сам — там, где привязка невозможна, возвращается
            // готовое значение.
            return binding.ProvideValue(serviceProvider);
        }
    }
}
