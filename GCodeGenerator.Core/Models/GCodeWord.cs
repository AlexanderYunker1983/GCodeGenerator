#nullable enable
using System;

namespace GCodeGenerator.Models
{
    /// <summary>
    /// A single word of a G-code line (plan item 4.1): a letter plus a value.
    /// Examples: <c>G1</c> → ('G', 1), <c>X10.5</c> → ('X', 10.5), <c>M30</c> → ('M', 30).
    /// A word with non-null <see cref="Text"/> is rendered verbatim (raw word).
    /// </summary>
    public sealed class GCodeWord
    {
        public GCodeWord(char letter, double number, int decimals = -1)
        {
            if (!double.IsFinite(number))
                throw new ArgumentOutOfRangeException(nameof(number), number, "G-code word value must be finite.");

            Letter = letter;
            Number = number;
            Decimals = decimals;
        }

        /// <summary>Creates a raw word rendered verbatim (e.g. "G92", which is never padded).</summary>
        public GCodeWord(string text)
        {
            if (string.IsNullOrEmpty(text))
                throw new ArgumentException("Raw word text must not be null or empty.", nameof(text));
            Text = text;
        }

        /// <summary>Word letter (G, M, X, Y, Z, I, J, F, S, P, ...). Unused for raw words.</summary>
        public char Letter { get; }

        /// <summary>Numeric value. Unused for raw words.</summary>
        public double Number { get; }

        /// <summary>Raw text; non-null means the word is rendered verbatim.</summary>
        public string? Text { get; }

        /// <summary>
        /// Decimal places for axis/word numbers; -1 means plain
        /// <c>InvariantCulture</c> <c>ToString()</c> (legacy preamble formatting).
        /// Ignored for G/M and raw words.
        /// </summary>
        public int Decimals { get; }

        public static GCodeWord G(int code) => new GCodeWord('G', code);
        public static GCodeWord M(int code) => new GCodeWord('M', code);
        public static GCodeWord X(double value, int decimals) => new GCodeWord('X', value, decimals);
        public static GCodeWord Y(double value, int decimals) => new GCodeWord('Y', value, decimals);
        public static GCodeWord Z(double value, int decimals) => new GCodeWord('Z', value, decimals);
        public static GCodeWord I(double value, int decimals) => new GCodeWord('I', value, decimals);
        public static GCodeWord J(double value, int decimals) => new GCodeWord('J', value, decimals);
        public static GCodeWord F(double value, int decimals) => new GCodeWord('F', value, decimals);
        public static GCodeWord S(int value) => new GCodeWord('S', value, -1);
        public static GCodeWord P(double value) => new GCodeWord('P', value, -1);
        public static GCodeWord Raw(string text) => new GCodeWord(text);
    }
}
