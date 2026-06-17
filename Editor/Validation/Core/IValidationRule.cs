using System.Collections.Generic;

namespace BrainIn.DevTools.Editor.Validation.Core
{
    /// <summary>
    /// Represents one validation rule that can inspect a Unity project and return validation results.
    /// </summary>
    public interface IValidationRule
    {
        /// <summary>
        /// Gets the human-readable name of the validation rule.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Executes the validation rule.
        /// </summary>
        /// <param name="context">Shared validation context containing project-level information.</param>
        /// <returns>Validation results produced by this rule.</returns>
        IEnumerable<ValidationResult> Validate(ValidationContext context);
    }
}