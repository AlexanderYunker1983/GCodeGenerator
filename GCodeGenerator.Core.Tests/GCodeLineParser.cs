using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Простой парсер строк G-кода для поведенческих тестов (пункт 5.7 плана).
    /// Извлекает перемещения G0/G1 со словами X/Y/Z/F (порядок слов фиксирован
    /// генератором: G, X, Y, Z, I, J, F). Строки без номеров (UseLineNumbers=false)
    /// и комментарии в тестах не используются.
    /// </summary>
    internal static class GCodeLineParser
    {
        /// <summary>Одно перемещение (G0/G1) со словами X/Y/Z/F.</summary>
        public sealed class Move
        {
            public string GCode;
            public double? X;
            public double? Y;
            public double? Z;
            public double? F;

            public bool IsRapid => GCode == "G0";
            public bool IsLinear => GCode == "G1";
            public bool HasXy => X.HasValue && Y.HasValue;
        }

        private static readonly Regex MoveRegex = new Regex(
            @"^(G[01])\b\s*(?:X(-?\d+(?:\.\d+)?))?\s*(?:Y(-?\d+(?:\.\d+)?))?\s*(?:Z(-?\d+(?:\.\d+)?))?\s*(?:F(-?\d+(?:\.\d+)?))?\s*$",
            RegexOptions.Compiled);

        /// <summary>Парсит все перемещения G0/G1 из строк программы.</summary>
        public static List<Move> ParseMoves(IEnumerable<string> lines)
        {
            var moves = new List<Move>();
            foreach (var line in lines)
            {
                var m = MoveRegex.Match(line);
                if (!m.Success)
                    continue;
                moves.Add(new Move
                {
                    GCode = m.Groups[1].Value,
                    X = m.Groups[2].Success ? double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) : null,
                    Y = m.Groups[3].Success ? double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture) : null,
                    Z = m.Groups[4].Success ? double.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture) : null,
                    F = m.Groups[5].Success ? double.Parse(m.Groups[5].Value, CultureInfo.InvariantCulture) : null,
                });
            }
            return moves;
        }

        /// <summary>Все G1-перемещения с координатами X и Y.</summary>
        public static List<Move> LinearXyMoves(IEnumerable<string> lines)
        {
            var result = new List<Move>();
            foreach (var move in ParseMoves(lines))
                if (move.IsLinear && move.HasXy)
                    result.Add(move);
            return result;
        }

        /// <summary>Все Z-цели перемещений (G0 и G1), в порядке программы.</summary>
        public static List<double> ZTargets(IEnumerable<string> lines)
        {
            var result = new List<double>();
            foreach (var move in ParseMoves(lines))
                if (move.Z.HasValue)
                    result.Add(move.Z.Value);
            return result;
        }

        /// <summary>Минимальная достигнутая Z (наибольший провал).</summary>
        public static double? MinZ(IEnumerable<string> lines)
        {
            double? min = null;
            foreach (var z in ZTargets(lines))
                if (min == null || z < min)
                    min = z;
            return min;
        }

        /// <summary>Максимальная достигнутая Z (наименьший провал, т.е. «верх» обработки).</summary>
        public static double? MaxMillingZ(IEnumerable<string> lines)
        {
            // Рассматриваем только рабочие (G1) Z-опускания — глубины проходов,
            // не SafeZ (быстрые подъёмы).
            double? max = null;
            foreach (var move in ParseMoves(lines))
                if (move.IsLinear && move.Z.HasValue)
                    if (max == null || move.Z.Value > max)
                        max = move.Z.Value;
            return max;
        }
    }
}
