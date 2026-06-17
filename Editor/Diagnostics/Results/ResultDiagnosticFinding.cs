namespace BrainIn.DevTools.Editor.Diagnostics.Results
{
    /// <summary>
    /// Represents one diagnostic finding detected in a BrainIn result JSON.
    /// </summary>
    public sealed class ResultDiagnosticFinding
    {
        /// <summary>
        /// Creates a new result diagnostic finding.
        /// </summary>
        /// <param name="severity">Finding severity.</param>
        /// <param name="title">Short finding title.</param>
        /// <param name="message">Detailed finding message.</param>
        /// <param name="location">Optional result location, for example Round 2.</param>
        public ResultDiagnosticFinding(
            ResultDiagnosticSeverity severity,
            string title,
            string message,
            string location = "")
        {
            Severity = severity;
            Title = title;
            Message = message;
            Location = location;
        }

        /// <summary>
        /// Gets the finding severity.
        /// </summary>
        public ResultDiagnosticSeverity Severity { get; }

        /// <summary>
        /// Gets the short finding title.
        /// </summary>
        public string Title { get; }

        /// <summary>
        /// Gets the detailed finding message.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Gets the optional result location.
        /// </summary>
        public string Location { get; }
    }
}