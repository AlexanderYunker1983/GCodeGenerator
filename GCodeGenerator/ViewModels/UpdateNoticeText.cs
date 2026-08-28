#nullable enable
using System.Globalization;
using GCodeGenerator.Localization;

namespace GCodeGenerator.ViewModels
{
    /// <summary>
    /// Сообщение о вышедшей версии.
    ///
    /// Его показывают два окна — главное строкой над списком операций и
    /// «О программе» после нажатия «проверить», — и говорить они обязаны
    /// одно и то же: два похожих, но разных сообщения об одном событии
    /// читаются как два разных события.
    /// </summary>
    internal static class UpdateNoticeText
    {
        private const string Key = "UpdateAvailable";

        /// <summary>
        /// «Доступна версия X» на языке интерфейса.
        ///
        /// Без словаря остаётся сам номер версии: подстановка в ключ
        /// «UpdateAvailable» дала бы слово «UpdateAvailable» и потеряла
        /// единственное, ради чего сообщение существует.
        /// </summary>
        /// <param name="localization">Словарь интерфейса; null — без перевода.</param>
        /// <param name="version">Версия вышедшего выпуска.</param>
        public static string For(ILocalizationManager? localization, string version)
        {
            var template = localization?.GetString(Key);
            return string.IsNullOrEmpty(template) || template == "?" + Key + "?"
                ? version
                : string.Format(CultureInfo.CurrentCulture, template, version);
        }
    }
}
