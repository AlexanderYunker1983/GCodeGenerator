#nullable enable
using System;
using System.Collections.Generic;

namespace GCodeGenerator.GCodeGenerators.Helpers
{
    /// <summary>
    /// Строит последовательность проходов по глубине без накопления
    /// погрешности в координате Z.
    /// </summary>
    internal static class DepthPassPlanner
    {
        private const double AbsoluteTolerance = 1e-12;
        private const double RelativeTolerance = 1e-12;

        /// <summary>
        /// Возвращает пары глубин от поверхности: начало и конец прохода.
        /// Последний конец всегда равен <paramref name="totalDepth"/> точно.
        /// </summary>
        public static IEnumerable<(double CurrentDepth, double NextDepth)> Plan(
            double totalDepth,
            double stepDepth)
        {
            if (totalDepth <= 0)
                yield break;

            var tolerance = Math.Max(
                AbsoluteTolerance,
                Math.Max(Math.Abs(totalDepth), Math.Abs(stepDepth)) * RelativeTolerance);
            var currentDepth = 0.0;

            while (currentDepth < totalDepth)
            {
                var remaining = totalDepth - currentDepth;
                var nextDepth = remaining <= stepDepth + tolerance
                    ? totalDepth
                    : currentDepth + stepDepth;

                // Защита от шага, который положителен, но слишком мал,
                // чтобы изменить число такого масштаба в представлении double.
                if (!(nextDepth > currentDepth))
                    throw new InvalidOperationException("Step depth is too small to advance the layer depth.");

                yield return (currentDepth, nextDepth);

                if (nextDepth == totalDepth)
                    yield break;

                currentDepth = nextDepth;
            }
        }
    }
}
