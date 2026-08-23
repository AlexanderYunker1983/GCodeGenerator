using System.Collections.Generic;
using GCodeGenerator.Models;

namespace GCodeGenerator.GCodeGenerators
{
    public interface IGCodeGenerator
    {
        GCodeProgram Generate(IList<OperationBase> operations, GCodeSettings settings);
    }
}


