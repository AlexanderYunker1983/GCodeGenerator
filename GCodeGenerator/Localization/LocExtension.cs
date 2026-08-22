using System;
using System.Windows.Markup;

namespace GCodeGenerator.Localization
{
    /// <summary>
    /// XAML-расширение для локализации (пункт 1.3 плана): замена Mugen Binding
    /// <c>{DataBinding '$i18n.Key'}</c> на <c>{loc:Loc Key}</c>.
    /// Возвращает локализованную строку, полученную из <see cref="LocalizationProvider"/>.
    ///
    /// Замечание: возвращаем обычную строку, а не WPF <see cref="System.Windows.Data.Binding"/>,
    /// потому что нативная привязка со статическим <c>Source</c> в этом приложении (на базе
    /// Mugen) некорректно применяется при загрузке вложенных UserControls и приводит к
    /// зависанию при старте (окно не отображается). Культура в приложении не меняется во
    /// время выполнения (нет вызовов <c>ChangeCulture</c>), поэтому динамическое обновление
    /// привязки не требуется — строка резолвится один раз при загрузке XAML.
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
            var manager = LocalizationProvider.Instance;
            if (manager == null || string.IsNullOrEmpty(_key))
                return _key;
            return manager.GetString(_key);
        }
    }
}
