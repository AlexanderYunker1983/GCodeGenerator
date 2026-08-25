#nullable enable
using GCodeGenerator.Models;
using GCodeGenerator.ViewModels;
using System.Collections.ObjectModel;

namespace GCodeGenerator.Services
{
    /// <summary>Creates the project lifecycle workflow for one operation collection.</summary>
    public interface IProjectWorkflowFactory
    {
        ProjectWorkflowViewModel Create(
            ObservableCollection<OperationBase> operations,
            GCodeWorkflowViewModel gCodeWorkflow);
    }
}
