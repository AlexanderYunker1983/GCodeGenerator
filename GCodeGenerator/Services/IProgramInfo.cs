namespace GCodeGenerator.Services
{
    /// <summary>
    /// Пункт 7.5 плана: версия программы через IoC (ранее статика
    /// <c>PlatformVariables.ProgramVersion</c>).
    /// </summary>
    public interface IProgramInfo
    {
        /// <summary>Строка версии для заголовка окна (например «1.0» или «1.0.3-Developer Version»).</summary>
        string Version { get; }
    }

    /// <summary>Неизменяемая реализация <see cref="IProgramInfo"/>.</summary>
    public sealed class ProgramInfo : IProgramInfo
    {
        public ProgramInfo(string version)
        {
            Version = version ?? string.Empty;
        }

        public string Version { get; }
    }
}
