using System;
using GCodeGenerator.Models;

namespace GCodeGenerator.GCodeGenerators
{
    public interface IOperationGenerator
    {
        void Generate(OperationBase operation, Action<string> addLine, string g0, string g1, GCodeSettings settings);
    }
}

