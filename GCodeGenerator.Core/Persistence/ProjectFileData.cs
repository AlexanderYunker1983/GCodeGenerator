#nullable enable
using System.Collections.Generic;
using GCodeGenerator.Models;

namespace GCodeGenerator.Persistence
{
    /// <summary>
    /// Результат чтения файла проекта .ygc: операции и необязательные секции
    /// настроек, влияющих на генерацию G-code.
    /// </summary>
    public sealed class ProjectFileData
    {
        /// <summary>
        /// Версия формата, в которой файл был прочитан. Сохранение всегда
        /// пишет текущую версию: файл старой версии после первого же
        /// сохранения не откроется прежними сборками, и вызывающая сторона
        /// по этой версии решает, предупреждать ли об апгрейде.
        /// </summary>
        public int Version { get; init; }

        /// <summary>Операции (null — в файле нет секции операций).</summary>
        public List<OperationBase>? Operations { get; init; }

        /// <summary>
        /// Настройки формата G-code из секции "format" (null — секции нет в
        /// legacy/v2-файле; в этом случае используются глобальные настройки).
        /// </summary>
        public GCodeFormatSettings? Format { get; init; }

        /// <summary>
        /// Настройки шпинделя из секции "spindle" (null — секции нет в файле,
        /// напр. старый .ygc; в этом случае сохраняются глобальные настройки).
        /// </summary>
        public SpindleSettings? Spindle { get; init; }

        /// <summary>
        /// Настройки СОЖ из секции "coolant" (null — секции нет в файле,
        /// напр. старый .ygc; в этом случае сохраняются глобальные настройки).
        /// </summary>
        public CoolantSettings? Coolant { get; init; }

        /// <summary>
        /// Настройки рабочей системы координат из секции "workCoordinate"
        /// (null — секции нет в legacy/v2-файле).
        /// </summary>
        public WorkCoordinateSettings? WorkCoordinate { get; init; }
    }
}
