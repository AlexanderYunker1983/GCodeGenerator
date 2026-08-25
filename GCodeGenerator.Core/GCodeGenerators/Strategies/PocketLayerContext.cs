using System.Collections.Generic;
using GCodeGenerator.GCodeGenerators.Geometry;
using GCodeGenerator.GCodeGenerators.Interfaces;
using GCodeGenerator.Models;

namespace GCodeGenerator.GCodeGenerators.Strategies
{
    /// <summary>
    /// Всё, что нужно знать о слое кармана, чтобы его обработать.
    ///
    /// Раньше те же сведения передавались стратегии десятью отдельными
    /// параметрами, и каждый новый параметр приходилось протаскивать через
    /// все пять стратегий, генератор DXF-слоя и обе точки вызова —
    /// независимо от того, нужен ли он кому-то, кроме одной из них. Порядок
    /// десяти позиционных аргументов при этом ничем не защищён: радиус
    /// инструмента, смещение уклона и шаг — три числа подряд, и перепутать
    /// их местами компилятор не мешает.
    ///
    /// Инструмент в момент вызова стоит в центре на рабочей высоте
    /// <see cref="WorkingZ"/>; возврат в центр и подъём выполняет вызывающий.
    /// </summary>
    public sealed class PocketLayerContext
    {
        public PocketLayerContext(
            IPocketOperation operation,
            IPocketGeometry geometry,
            double toolRadius,
            double taperOffset,
            double step,
            double workingZ,
            List<(double x, double y)> contourPoints,
            (double x, double y) center,
            GCodeSettings settings)
        {
            Operation = operation;
            Geometry = geometry;
            ToolRadius = toolRadius;
            TaperOffset = taperOffset;
            Step = step;
            WorkingZ = workingZ;
            ContourPoints = contourPoints;
            Center = center;
            Settings = settings;
        }

        /// <summary>Операция кармана: подачи, направление, число знаков.</summary>
        public IPocketOperation Operation { get; }

        /// <summary>Геометрия контура: смещённые контуры и проверка вырождения.</summary>
        public IPocketGeometry Geometry { get; }

        /// <summary>Радиус инструмента.</summary>
        public double ToolRadius { get; }

        /// <summary>Смещение контура из-за уклона стенок на глубине этого слоя.</summary>
        public double TaperOffset { get; }

        /// <summary>Шаг обработки: расстояние между соседними проходами.</summary>
        public double Step { get; }

        /// <summary>
        /// Рабочая высота слоя. Нужна стратегиям с отводами: инструмент
        /// возвращается на неё после подъёма.
        /// </summary>
        public double WorkingZ { get; }

        /// <summary>Контур слоя — траектория центра инструмента вдоль стенки.</summary>
        public List<(double x, double y)> ContourPoints { get; }

        /// <summary>Центр контура: отсюда инструмент начинает и сюда возвращается.</summary>
        public (double x, double y) Center { get; }

        /// <summary>Настройки генерации программы.</summary>
        public GCodeSettings Settings { get; }

        /// <summary>Число знаков после запятой в координатах — из операции.</summary>
        public int Decimals => Operation.Decimals;

        /// <summary>Рабочая подача в плоскости — из операции.</summary>
        public double FeedXYWork => Operation.FeedXYWork;
    }
}
