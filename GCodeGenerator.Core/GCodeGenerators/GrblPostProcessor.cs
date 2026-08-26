#nullable enable
namespace GCodeGenerator.GCodeGenerators
{
    /// <summary>
    /// Постпроцессор для GRBL и LinuxCNC: тот же состав программы, что у
    /// <see cref="GenericPostProcessor"/>, но аргумент паузы <c>G4 P</c> —
    /// в секундах, а не в миллисекундах.
    ///
    /// Это документированное отличие этих стоек от Fanuc-совместимых:
    /// программа с <c>G4 P2500</c> на GRBL простояла бы сорок минут вместо
    /// двух с половиной секунд. Прежде README предлагал пересчитывать
    /// значение вручную.
    /// </summary>
    public sealed class GrblPostProcessor : GenericPostProcessor
    {
        /// <inheritdoc />
        public override string Key => "GRBL";

        /// <inheritdoc />
        public override string Name => "GRBL / LinuxCNC";

        /// <inheritdoc />
        protected override double DwellArgument(double seconds) => seconds;
    }
}
