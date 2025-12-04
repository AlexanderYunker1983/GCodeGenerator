using System.Collections.Generic;

namespace GCodeGenerator.Models
{
    public class GCodeProgram
    {
        public IList<string> Lines { get; } = new List<string>();

        public override string ToString()
        {
            return string.Join("\n", Lines);
        }
    }
}


