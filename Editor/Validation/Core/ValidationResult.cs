namespace BrainIn.DevTools.Editor.Validation.Core
{
    /// <summary>
    /// Represents one validation finding produced by a validation rule.
    /// </summary>
    public sealed class ValidationResult
    {
        /// <summary>
        /// Gets the severity of the validation result.
        /// </summary>
        public ValidationSeverity Severity { get; private set; }

        /// <summary>
        /// Gets the name of the rule that produced this result.
        /// </summary>
        public string RuleName { get; private set; }

        /// <summary>
        /// Gets the human-readable validation message.
        /// </summary>
        public string Message { get; private set; }

        /// <summary>
        /// Gets the Unity asset path related to this result, if available.
        /// </summary>
        public string AssetPath { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationResult"/> class.
        /// </summary>
        /// <param name="severity">Severity of the validation result.</param>
        /// <param name="ruleName">Name of the rule that produced the result.</param>
        /// <param name="message">Human-readable validation message.</param>
        /// <param name="assetPath">Optional Unity asset path related to the result.</param>
        public ValidationResult(
            ValidationSeverity severity,
            string ruleName,
            string message,
            string assetPath = "")
        {
            Severity = severity;
            RuleName = ruleName;
            Message = message;
            AssetPath = assetPath;
        }
    }
}