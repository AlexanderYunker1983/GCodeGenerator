#nullable enable
using System;
using System.Globalization;
using System.Threading;
using GCodeGenerator.Models;

using GCodeGenerator.Toolpath;

namespace GCodeGenerator.GCodeGenerators.Helpers
{
    /// <summary>
    /// Класс-помощник для генерации G-кода карманов: цикл обработки по слоям
    /// с проверкой, что контур ещё не выродился.
    /// </summary>
    public class PocketGenerationHelper
    {
        /// <summary>
        /// Генерирует цикл обработки по слоям.
        /// Пункт 4.4 плана: пишет структурированные блоки через ToolPathBuilder.
        /// </summary>
        /// <param name="op">Операция кармана</param>
        /// <param name="generateLayer">Делегат для генерации одного слоя (currentZ, nextZ, passNumber) - возвращает false, если обработку нужно прекратить</param>
        /// <param name="builder">Построитель траектории</param>
        /// <param name="settings">Настройки генерации G-кода</param>
        /// <param name="cancellation">Отмена: проверяется перед каждым слоем.</param>
        public void GenerateLayerLoop(
            PocketOperationBase op,
            Func<double, double, int, bool> generateLayer,
            ToolPathBuilder builder,
            GCodeSettings settings,
            CancellationToken cancellation = default)
        {
            // Пункт 3.8 плана: StepDepth <= 0 не двигает Z вниз — цикл по слоям
            // превращается в бесконечный. Бросаем исключение вместо зависания.
            if (op.StepDepth <= 0)
                throw new ArgumentOutOfRangeException(nameof(op),
                    $"StepDepth must be greater than zero (got {op.StepDepth.ToString(CultureInfo.InvariantCulture)}); otherwise the layer loop would run forever.");

            int decimals = op.Decimals;

            double currentZ = op.ContourHeight;
            double finalZ = op.ContourHeight - op.TotalDepth;
            int pass = 0;

            while (currentZ > finalZ)
            {
                // Слой — единица работы: глубокий карман строится из сотен
                // слоёв, и отмена не должна ждать конца операции.
                cancellation.ThrowIfCancellationRequested();

                double nextZ = currentZ - op.StepDepth;
                if (nextZ < finalZ) nextZ = finalZ;
                pass++;

                builder.Comment(ProgramComments.Pass(pass, GCodeGenerationHelper.FormatNumber(nextZ, GCodeGenerationHelper.DecimalFormat(decimals))));

                // Если generateLayer возвращает false, прекращаем обработку
                if (!generateLayer(currentZ, nextZ, pass))
                {
                    builder.Comment(ProgramComments.ContourTooSmall);
                    break;
                }

                currentZ = nextZ;
            }
        }
    }
}

