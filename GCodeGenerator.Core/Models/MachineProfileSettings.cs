#nullable enable
namespace GCodeGenerator.Models
{
    /// <summary>
    /// Проверяемый паспорт станка в программных координатах, миллиметрах и
    /// мм/мин. По умолчанию выключен: границы конкретного оборудования
    /// нельзя безопасно угадать.
    /// </summary>
    public sealed class MachineProfileSettings
    {
        /// <summary>Проверять каждую построенную траекторию по этому профилю.</summary>
        public bool Enabled { get; set; }

        public double MinX { get; set; } = 0;
        public double MaxX { get; set; } = 300;
        public double MinY { get; set; } = 0;
        public double MaxY { get; set; } = 300;
        public double MinZ { get; set; } = -100;
        public double MaxZ { get; set; } = 100;

        /// <summary>Наибольшая рабочая подача, мм/мин.</summary>
        public double MaxWorkFeed { get; set; } = 3000;

        /// <summary>Наибольшая подача холостого хода, мм/мин.</summary>
        public double MaxRapidFeed { get; set; } = 6000;

        /// <summary>Наибольшая скорость шпинделя, об/мин.</summary>
        public int MaxSpindleSpeedRpm { get; set; } = 24000;
    }
}
