using System.Collections.Generic;

namespace GCodeGenerator.Models
{
    /// <summary>
    /// Domain validation for operations (plan item 3.7). Implementations report
    /// physically impossible parameter values (non-positive depths, steps,
    /// diameters, hole counts, degenerate contours) as a list of issues instead
    /// of throwing, so callers can aggregate and display all problems at once.
    /// </summary>
    public interface IValidatable
    {
        /// <summary>
        /// Returns all validation issues; an empty list means the operation is
        /// valid and can be generated.
        /// </summary>
        IReadOnlyList<ValidationIssue> Validate();
    }
}
