using GCodeGenerator.Models;

namespace GCodeGenerator.Tests.Fixtures
{
    /// <summary>
    /// Варианты <see cref="GCodeSettings"/> для фикстур.
    /// Default — точные значения по умолчанию модели GCodeSettings.
    /// </summary>
    public static class SettingsFixtures
    {
        /// <summary>Все значения по умолчанию (линейные номера вкл., padded G выкл., дуги вкл., шпиндель/СОЖ вкл.).</summary>
        public static GCodeSettings Default()
        {
            return new GCodeSettings();
        }

        /// <summary>Без линейных номеров (UseLineNumbers = false).</summary>
        public static GCodeSettings NoLineNumbers()
        {
            var s = Default();
            s.Format.UseLineNumbers = false;
            return s;
        }

        /// <summary>G-коды с ведущим нулём (G01 вместо G1).</summary>
        public static GCodeSettings PaddedGCodes()
        {
            var s = Default();
            s.Format.UsePaddedGCodes = true;
            return s;
        }

        /// <summary>Дуги G2/G3 запрещены — генератор обязан разбить дуги на полилинии.</summary>
        public static GCodeSettings ArcsOff()
        {
            var s = Default();
            s.Format.AllowArcs = false;
            return s;
        }

        /// <summary>Шпиндель и СОЖ выключены (нет M3/M4/M5/M8/M9).</summary>
        public static GCodeSettings SpindleCoolantOff()
        {
            var s = Default();
            s.Spindle.SpindleControlEnabled = false;
            s.Coolant.CoolantControlEnabled = false;
            return s;
        }

        /// <summary>
        /// Задержка после пуска шпинделя: единственная настройка, дающая в
        /// программе команду G4, и до сих пор не попадавшая ни в один
        /// golden-файл. Значение аргумента P — миллисекунды.
        /// </summary>
        public static GCodeSettings SpindleDelay()
        {
            var s = Default();
            s.Spindle.SpindleControlEnabled = true;
            s.Spindle.SpindleStartEnabled = true;
            s.Spindle.SpindleDelayEnabled = true;
            s.Spindle.SpindleDelaySeconds = 2.5;
            return s;
        }

        /// <summary>
        /// Та же задержка шпинделя, но для стойки GRBL: единственное отличие
        /// программы от <see cref="SpindleDelay"/> — аргумент P команды G4
        /// в секундах, а не в миллисекундах.
        /// </summary>
        public static GCodeSettings GrblSpindleDelay()
        {
            var s = SpindleDelay();
            s.Format.PostProcessorName = "GRBL";
            return s;
        }

        /// <summary>Установка рабочей системы координат G55 в начале программы.</summary>
        public static GCodeSettings WcsG55()
        {
            var s = Default();
            s.WorkCoordinate.SetWorkCoordinateSystem = true;
            s.WorkCoordinate.WorkCoordinateSystem = "G55";
            return s;
        }

        /// <summary>G92-старт в начале и быстрый переход в заданную точку в конце программы.</summary>
        public static GCodeSettings G92StartEnd()
        {
            var s = Default();
            s.WorkCoordinate.AddStartPosition = true;
            s.WorkCoordinate.StartX = 0.0;
            s.WorkCoordinate.StartY = 0.0;
            s.WorkCoordinate.StartZ = 5.0;
            s.WorkCoordinate.AddEndPosition = true;
            s.WorkCoordinate.EndX = 100.0;
            s.WorkCoordinate.EndY = 0.0;
            s.WorkCoordinate.EndZ = 5.0;
            return s;
        }
    }
}
