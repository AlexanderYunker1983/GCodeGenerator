#nullable enable
using System.ComponentModel;

namespace GCodeGenerator.Localization
{
    /// <summary>
    /// Источник переведённых строк для привязок разметки.
    ///
    /// Разметка обращается к нему по ключу как к словарю, а он сообщает об
    /// изменении всех значений сразу, когда меняется язык: тогда каждая
    /// привязка перечитывает свою строку, и надписи в открытых окнах
    /// меняются на месте. Прежде <c>{loc:Loc Key}</c> подставляло строку
    /// один раз при загрузке разметки, поэтому сменить язык можно было
    /// только перезапуском программы.
    /// </summary>
    public sealed class LocalizationSource : INotifyPropertyChanged
    {
        /// <summary>Имя, которым WPF обозначает «изменились все элементы индексатора».</summary>
        private const string IndexerName = "Item[]";

        private LocalizationSource()
        {
        }

        /// <summary>Единственный источник: разметка ссылается на него статически.</summary>
        public static LocalizationSource Instance { get; } = new LocalizationSource();

        /// <summary>
        /// Строка по ключу на текущем языке. Отсутствующий ключ менеджер
        /// вернёт как «?ключ?» и запишет в журнал.
        /// </summary>
        /// <param name="key">Ключ строки в словаре перевода.</param>
        public string this[string key]
        {
            get
            {
                var manager = LocalizationProvider.Instance;
                if (manager == null || string.IsNullOrEmpty(key))
                    return key;
                return manager.GetString(key);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>Сообщает привязкам, что все строки нужно перечитать.</summary>
        public void Refresh()
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(IndexerName));
    }
}
