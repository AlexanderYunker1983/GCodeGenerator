#nullable enable
namespace GCodeGenerator.Models
{
    /// <summary>
    /// G-code formatting settings (line numbers, comments, arcs, padded G-codes).
    /// Пункт 8.1 плана: выделено из плоского <see cref="GCodeSettings"/>.
    /// </summary>
    public class GCodeFormatSettings
    {
        public bool UseLineNumbers { get; set; } = true;

        public int LineNumberStart { get; set; } = 10;

        public int LineNumberStep { get; set; } = 10;

        public bool UseComments { get; set; } = true;

        /// <summary>
        /// Allow arc moves (G2/G3). If false, arcs must be converted to linear moves.
        /// </summary>
        public bool AllowArcs { get; set; } = true;

        /// <summary>
        /// If true, G-codes are formatted with leading zero, e.g. G01 instead of G1.
        /// </summary>
        public bool UsePaddedGCodes { get; set; } = false;

        /// <summary>
        /// Ключ постпроцессора — стойки, для которой строится программа.
        /// Значение по умолчанию повторяет ключ GenericPostProcessor
        /// (ссылаться на него отсюда нельзя: слой моделей не зависит от
        /// генераторов, связь закреплена тестом реестра постпроцессоров).
        /// Файлы проектов прежних версий ключа не содержат — они читаются
        /// с этим же значением и дают прежнюю программу.
        /// </summary>
        public string PostProcessorName { get; set; } = "Generic";
    }
}
