#nullable enable
using System;
using System.Collections.Generic;

namespace GCodeGenerator.Models
{
    /// <summary>
    /// A single line of a G-code program (plan item 4.1): words and/or a comment.
    /// A line is either a command (words) or a comment, matching the shapes the
    /// generators currently emit.
    /// </summary>
    public sealed class GCodeBlock
    {
        public GCodeBlock(IReadOnlyList<GCodeWord> words, string? comment = null)
        {
            Words = words ?? (IReadOnlyList<GCodeWord>)Array.Empty<GCodeWord>();
            Comment = comment;
        }

        /// <summary>
        /// Line number assigned by the formatter when rendering with line
        /// numbers enabled; 0 means "no number" (or not rendered yet).
        /// </summary>
        public int LineNumber { get; set; }

        /// <summary>Command words of the line; empty for comment lines.</summary>
        public IReadOnlyList<GCodeWord> Words { get; }

        /// <summary>Line comment text (rendered as "(...)"); null for command lines.</summary>
        public string? Comment { get; }
    }
}
