namespace GCodeGenerator.Models
{
    public class PackageDefinition
    {
        public string Name { get; set; }
        public int PinsPerRow { get; set; }
        public double PinPitch { get; set; } // Шаг между выводами в ряду (мм)
        public double RowSpacing { get; set; } // Расстояние между рядами (мм)

        public PackageDefinition(string name, int pinsPerRow, double pinPitch, double rowSpacing)
        {
            Name = name;
            PinsPerRow = pinsPerRow;
            PinPitch = pinPitch;
            RowSpacing = rowSpacing;
        }
    }
}

