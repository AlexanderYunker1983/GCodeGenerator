using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GCodeGenerator.GCodeGenerators.Helpers;
using GCodeGenerator.Models;

namespace GCodeGenerator.GCodeGenerators
{
    /// <summary>
    /// Renders a structured G-code program to text lines (plan item 4.2).
    /// Applies line numbers (UseLineNumbers/LineNumberStart/LineNumberStep),
    /// G/M code padding (UsePaddedGCodes) and comment visibility
    /// (UseComments). The logic was moved here from SimpleGCodeGenerator
    /// (its FormatG/FormatM/AddLine local functions).
    /// </summary>
    public static class GCodeFormatter
    {
        /// <summary>
        /// Renders the program blocks to text lines, stores the result in
        /// <see cref="GCodeProgram.Lines"/> and assigns line numbers to the
        /// rendered blocks.
        /// </summary>
        public static List<string> Format(GCodeProgram program, GCodeSettings settings)
        {
            var lines = new List<string>(program.Blocks.Count);
            int lineNumber = settings.UseLineNumbers ? settings.LineNumberStart : 0;

            foreach (var block in program.Blocks)
            {
                // Legacy behavior: with comments disabled, comment lines are
                // not emitted at all and do not consume a line number.
                if (block.Words.Count == 0 && !settings.UseComments)
                    continue;

                var text = RenderBlock(block, settings);
                if (settings.UseLineNumbers)
                {
                    block.LineNumber = lineNumber;
                    lines.Add($"N{lineNumber} {text}");
                    lineNumber += settings.LineNumberStep;
                }
                else
                {
                    lines.Add(text);
                }
            }

            program.Lines.Clear();
            foreach (var line in lines)
                program.Lines.Add(line);
            return lines;
        }

        private static string RenderBlock(GCodeBlock block, GCodeSettings settings)
        {
            if (block.Words.Count == 0)
                return $"({block.Comment})";

            var sb = new StringBuilder();
            foreach (var word in block.Words)
            {
                if (sb.Length > 0)
                    sb.Append(' ');
                sb.Append(RenderWord(word, settings));
            }

            // Inline comments are not produced by the generators today;
            // the support is kept for future phases.
            if (block.Comment != null)
                sb.Append(' ').Append('(').Append(block.Comment).Append(')');
            return sb.ToString();
        }

        private static string RenderWord(GCodeWord word, GCodeSettings settings)
        {
            // Raw words (G92, M30) are rendered verbatim — legacy behavior
            // never padded them.
            if (word.Text != null)
                return word.Text;

            if (word.Letter == 'G' || word.Letter == 'M')
            {
                if (!settings.UsePaddedGCodes)
                    return $"{word.Letter}{(int)word.Number}";
                return $"{word.Letter}{(int)word.Number:00}";
            }

            if (word.Decimals >= 0)
                return word.Letter + GCodeGenerationHelper.FormatNumber(word.Number, "0." + new string('0', word.Decimals));
            return word.Letter + word.Number.ToString(CultureInfo.InvariantCulture);
        }
    }
}
