#nullable enable
using System;
using System.Runtime.InteropServices;
using System.Text;
using GCodeGenerator.Diagnostics;

namespace GCodeGenerator.Services
{
    /// <summary>
    /// Интеграция с Windows Restart Manager. Регистрация относится только к
    /// текущему процессу; установщик сопоставляет её с файлами своего {app},
    /// поэтому одноимённая portable-копия не попадает под обновление.
    /// </summary>
    public sealed class ApplicationRestartService : IApplicationRestartService
    {
        private const int MaxCommandLineLength = 1024;
        private readonly IAppLogger _logger;
        private readonly Func<string?, RestartRestrictions, int> _register;

        public ApplicationRestartService(IAppLogger? logger = null)
            : this(RegisterApplicationRestart, logger)
        {
        }

        internal ApplicationRestartService(
            Func<string?, RestartRestrictions, int> register,
            IAppLogger? logger = null)
        {
            _register = register ?? throw new ArgumentNullException(nameof(register));
            _logger = logger ?? NullAppLogger.Instance;
        }

        public void Register(string? projectFile)
        {
            var commandLine = CommandLineFor(projectFile);
            if (commandLine?.Length >= MaxCommandLineLength)
            {
                _logger.Warning(
                    $"Restart Manager project path is too long; restarting without it: {projectFile}");
                commandLine = null;
            }

            try
            {
                // Перезапуск нужен после обновления/patch. При падении,
                // зависании или перезагрузке действует recovery-механизм:
                // автоматический безусловный рестарт там мог бы зациклиться.
                var result = _register(
                    commandLine,
                    RestartRestrictions.NoCrash
                    | RestartRestrictions.NoHang
                    | RestartRestrictions.NoReboot);
                if (result != 0)
                    _logger.Warning($"RegisterApplicationRestart failed with HRESULT 0x{result:X8}");
            }
            catch (Exception failure)
            {
                // Интеграция с оболочкой не является условием запуска. Сам
                // установщик всё равно сможет обновить файлы после закрытия.
                _logger.Error("RegisterApplicationRestart failed", failure);
            }
        }

        /// <summary>Безопасно кодирует один Windows-аргумент с путём.</summary>
        internal static string? CommandLineFor(string? projectFile)
        {
            if (string.IsNullOrWhiteSpace(projectFile))
                return null;

            var result = new StringBuilder(projectFile!.Length + 2);
            result.Append('"');
            var backslashes = 0;
            foreach (var character in projectFile)
            {
                if (character == '\\')
                {
                    backslashes++;
                    continue;
                }

                if (character == '"')
                {
                    result.Append('\\', backslashes * 2 + 1);
                    result.Append('"');
                    backslashes = 0;
                    continue;
                }

                result.Append('\\', backslashes);
                backslashes = 0;
                result.Append(character);
            }

            // Перед закрывающей кавычкой обратные слеши удваиваются, иначе
            // последний из них экранировал бы саму кавычку.
            result.Append('\\', backslashes * 2);
            result.Append('"');
            return result.ToString();
        }

        [Flags]
        internal enum RestartRestrictions
        {
            None = 0,
            NoCrash = 1,
            NoHang = 2,
            NoPatch = 4,
            NoReboot = 8,
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern int RegisterApplicationRestart(
            string? commandLine,
            RestartRestrictions flags);
    }
}
