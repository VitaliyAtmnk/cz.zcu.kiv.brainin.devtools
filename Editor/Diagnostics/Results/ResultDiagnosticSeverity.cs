namespace BrainIn.DevTools.Editor.Diagnostics.Results
{
    /// <summary>
    /// Severity of a BrainIn result diagnostic finding.
    /// </summary>
    public enum ResultDiagnosticSeverity
    {
        /// <summary>
        /// Informational diagnostic message.
        /// </summary>
        Info,

        /// <summary>
        /// Suspicious output that should be reviewed.
        /// </summary>
        Warning,

        /// <summary>
        /// Invalid output that likely indicates a broken result contract.
        /// </summary>
        Error
    }
}