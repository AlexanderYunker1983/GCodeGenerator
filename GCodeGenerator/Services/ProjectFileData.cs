using System.Collections.Generic;
using GCodeGenerator.Models;

namespace GCodeGenerator.Services
{
    /// <summary>
    /// Результат чтения файла проекта .ygc (пункт 8.2 плана, D4):
    /// операции + необязательные секции spindle/coolant.
    /// </summary>
    public sealed class ProjectFileData
    {
        /// <summary>Операции (null — в файле нет секции операций).</summary>
        public List<OperationBase> Operations { get; init; }

        /// <summary>
        /// Настройки шпинделя из секции "spindle" (null — секции нет в файле,
        /// напр. старый .ygc; в этом случае сохраняются глобальные настройки).
        /// </summary>
        public SpindleSettings Spindle { get; init; }

        /// <summary>
        /// Настройки СОЖ из секции "coolant" (null — секции нет в файле,
        /// напр. старый .ygc; в этом случае сохраняются глобальные настройки).
        /// </summary>
        public CoolantSettings Coolant { get; init; }
    }
}
