using GCodeGenerator.GCodeGenerators.Interfaces;

namespace GCodeGenerator.Models
{
    /// <summary>
    /// Общая часть операций профильной обработки: сторона обхода контура,
    /// способ врезания и точность замены дуг ломаной.
    ///
    /// Эти параметры описывают не форму контура, а то, как инструмент по нему
    /// идёт, поэтому одинаковы для окружности, эллипса, многоугольника,
    /// прямоугольника и контура из чертежа. Раньше они объявлялись в каждой
    /// модели отдельно, причём безопасное расстояние между проходами было
    /// пропущено у операции по чертежу — диалог показывал поле, значение
    /// которого некуда было сохранить.
    /// </summary>
    public abstract class ProfileOperationBase : MillingOperationBase, IProfileOperation
    {
        protected ProfileOperationBase(OperationType type, OperationCategory category, string name)
            : base(type, category, name)
        {
        }

        /// <summary>С какой стороны контура идёт инструмент.</summary>
        public ToolPathMode ToolPathMode { get; set; } = ToolPathMode.OnLine;

        /// <summary>Врезание вертикально или по наклонной.</summary>
        public EntryMode EntryMode { get; set; } = EntryMode.Vertical;

        /// <summary>Угол наклонного врезания, градусы.</summary>
        public double EntryAngle { get; set; } = 5.0;

        /// <summary>
        /// Безопасное расстояние между проходами при наклонном врезании, мм:
        /// на столько инструмент поднимается над материалом, возвращаясь
        /// к началу контура между витками рампы и перед рабочим проходом.
        /// Ноль означает возврат через безопасную высоту.
        /// </summary>
        public double SafeDistanceBetweenPasses { get; set; } = 1.0;

        /// <summary>
        /// Наибольшая длина отрезка при замене дуги ломаной, мм. Задаёт
        /// точность контура, когда вывод дуг отключён в настройках.
        /// </summary>
        public double MaxSegmentLength { get; set; } = 0.5;
    }
}
