using System.Collections.Generic;
using BrainIn.DevTools.Editor.Validation.Rules;

namespace BrainIn.DevTools.Editor.Validation.Core
{
    /// <summary>
    /// Provides a single place where default BrainIn validation rules are registered.
    /// </summary>
    public static class ValidationRuleRegistry
    {
        /// <summary>
        /// Creates the default validation runner used by the editor window and future batch mode execution.
        /// </summary>
        /// <returns>Validation runner with all default validation rules registered.</returns>
        public static ValidationRunner CreateDefaultRunner()
        {
            var runner = new ValidationRunner();

            foreach (var rule in CreateDefaultRules())
            {
                runner.AddRule(rule);
            }

            return runner;
        }

        /// <summary>
        /// Creates the default list of validation rules.
        /// </summary>
        /// <returns>List of default validation rules.</returns>
        public static IReadOnlyList<IValidationRule> CreateDefaultRules()
        {
            return new IValidationRule[]
            {
                new WebCompatibilityRule(),
                new BuildReadinessRule(),
                new LocalizationRule(),
                new BrainInIntegrationRule(),
                new ContractRule(),
                new RequiredReferencesRule(),
                new SceneRuntimePrerequisitesRule(),
                new MissingReferencesRule()
            };
        }
    }
}