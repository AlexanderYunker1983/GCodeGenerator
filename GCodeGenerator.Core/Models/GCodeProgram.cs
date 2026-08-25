#nullable enable
using System.Collections.Generic;

namespace GCodeGenerator.Models
{
    /// <summary>
    /// A generated G-code program (plan item 4.1).
    /// <see cref="Blocks"/> holds the structured lines (built by
    /// <c>ProgramBuilder</c>); <see cref="Lines"/> holds the rendered text
    /// (filled by <c>GCodeFormatter</c>, plan item 4.2).
    /// </summary>
    public class GCodeProgram
    {
        /// <summary>Structured lines of the program.</summary>
        public IList<GCodeBlock> Blocks { get; } = new List<GCodeBlock>();

        /// <summary>Rendered text lines (filled by the formatter).</summary>
        public IList<string> Lines { get; } = new List<string>();

        public override string ToString()
        {
            return string.Join("\n", Lines);
        }
    }
}
