#nullable enable
namespace GCodeGenerator.Models
{
    /// <summary>Порядок снятия материала внутри слоя кармана.</summary>
    public enum PocketProcessingDirection
    {
        /// <summary>От центральной части области к её внешней границе.</summary>
        CenterOutward,

        /// <summary>От внешней границы области к центральной части.</summary>
        OutsideIn,
    }
}
