using System.Collections.Generic;
using System.Linq;

namespace BrainIn.DevTools.Editor.Validation.Core
{
    /// <summary>
    /// Executes a configured set of validation rules and aggregates their results.
    /// </summary>
    public sealed class ValidationRunner
    {
        private readonly List<IValidationRule> _rules = new List<IValidationRule>();

        /// <summary>
        /// Adds a validation rule to the runner.
        /// </summary>
        /// <param name="rule">Validation rule to register.</param>
        /// <returns>The current runner instance for fluent configuration.</returns>
        public ValidationRunner AddRule(IValidationRule rule)
        {
            _rules.Add(rule);
            return this;
        }

        /// <summary>
        /// Runs all registered validation rules with the provided validation context.
        /// </summary>
        /// <param name="context">Shared validation context containing project-level information.</param>
        /// <returns>Aggregated validation results produced by all registered rules.</returns>
        public IReadOnlyList<ValidationResult> Run(ValidationContext context)
        {
            return _rules
                .SelectMany(rule => rule.Validate(context))
                .ToList();
        }
    }
}