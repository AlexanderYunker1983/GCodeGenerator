using System.Collections.Generic;
using GCodeGenerator.Models;

namespace GCodeGenerator.Services
{
    public interface IGCodeGenerator
    {
        GCodeProgram Generate(IList<OperationBase> operations, GCodeSettings settings);
    }
}


