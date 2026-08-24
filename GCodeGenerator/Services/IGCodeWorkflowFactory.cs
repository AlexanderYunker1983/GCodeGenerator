using GCodeGenerator.Models;
using GCodeGenerator.ViewModels;
using System.Collections.Generic;

namespace GCodeGenerator.Services
{
    /// <summary>Creates a G-code workflow bound to one project operation list.</summary>
    public interface IGCodeWorkflowFactory
    {
        GCodeWorkflowViewModel Create(IList<OperationBase> operations, GCodeSettings settings);
    }
}
