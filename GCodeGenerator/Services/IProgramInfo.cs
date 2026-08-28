#nullable enable
namespace GCodeGenerator.Services
{
    /// <summary>
    /// Сведения о самой программе: версия, правообладатель, где лежит журнал.
    ///
    /// Всё это известно окружению, а не документу: версия проставлена в сборку
    /// при её создании, правообладатель — свойство файла, путь к журналу знает
    /// журнал. View-моделям остаётся показать готовые строки — читать атрибуты
    /// сборки и работать с файловой системой они не должны.
    /// </summary>
    public interface IProgramInfo
    {
        /// <summary>Строка версии для заголовка окна (например «1.0» или «1.0.3-rc5»).</summary>
        string Version { get; }

        /// <summary>Правообладатель — то же, что в свойствах файла программы.</summary>
        string Copyright { get; }

        /// <summary>Полный путь к файлу журнала работы.</summary>
        string LogFilePath { get; }
    }

    /// <summary>Неизменяемая реализация <see cref="IProgramInfo"/>.</summary>
    public sealed class ProgramInfo : IProgramInfo
    {
        public ProgramInfo(string version, string copyright = "", string logFilePath = "")
        {
            Version = version ?? string.Empty;
            Copyright = copyright ?? string.Empty;
            LogFilePath = logFilePath ?? string.Empty;
        }

        public string Version { get; }

        public string Copyright { get; }

        public string LogFilePath { get; }
    }
}
