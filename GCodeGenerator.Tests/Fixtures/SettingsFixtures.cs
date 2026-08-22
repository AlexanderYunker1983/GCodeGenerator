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
            s.UseLineNumbers = false;
            return s;
        }

        /// <summary>G-коды с ведущим нулём (G01 вместо G1).</summary>
        public static GCodeSettings PaddedGCodes()
        {
            var s = Default();
            s.UsePaddedGCodes = true;
            return s;
        }

        /// <summary>Дуги G2/G3 запрещены — генератор обязан разбить дуги на полилинии.</summary>
        public static GCodeSettings ArcsOff()
        {
            var s = Default();
            s.AllowArcs = false;
            return s;
        }

        /// <summary>Шпиндель и СОЖ выключены (нет M3/M4/M5/M8/M9).</summary>
        public static GCodeSettings SpindleCoolantOff()
        {
            var s = Default();
            s.SpindleControlEnabled = false;
            s.CoolantControlEnabled = false;
            return s;
        }

        /// <summary>Установка рабочей системы координат G55 в начале программы.</summary>
        public static GCodeSettings WcsG55()
        {
            var s = Default();
            s.SetWorkCoordinateSystem = true;
            s.WorkCoordinateSystem = "G55";
            return s;
        }

        /// <summary>G92-старт в начале и быстрый переход в заданную точку в конце программы.</summary>
        public static GCodeSettings G92StartEnd()
        {
            var s = Default();
            s.AddStartPosition = true;
            s.StartX = 0.0;
            s.StartY = 0.0;
            s.StartZ = 5.0;
            s.AddEndPosition = true;
            s.EndX = 100.0;
            s.EndY = 0.0;
            s.EndZ = 5.0;
            return s;
        }
    }
}
