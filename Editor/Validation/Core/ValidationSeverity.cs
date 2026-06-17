namespace BrainIn.DevTools.Editor.Validation.Core
{
    /// <summary>
    /// Defines the severity level of a validation result.
    /// </summary>
    public enum ValidationSeverity
    {
        /// <summary>
        /// Informational validation message that does not indicate a problem.
        /// </summary>
        Info,

        /// <summary>
        /// Validation warning that should be reviewed but does not necessarily block the build.
        /// </summary>
        Warning,

        /// <summary>
        /// Validation error that indicates a serious issue and should normally block the build.
        /// </summary>
        Error
    }
}