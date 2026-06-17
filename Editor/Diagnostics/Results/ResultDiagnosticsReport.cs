using System;
using System.Collections.Generic;
using System.Linq;

namespace BrainIn.DevTools.Editor.Diagnostics.Results
{
    /// <summary>
    /// Contains the parsed and analyzed BrainIn result diagnostics.
    /// </summary>
    public sealed class ResultDiagnosticsReport
    {
        /// <summary>
        /// Gets or sets report generation time in UTC.
        /// </summary>
        public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets whether the input was parsed as JSON successfully.
        /// </summary>
        public bool ParseSucceeded { get; set; }

        /// <summary>
        /// Gets or sets totalTime value.
        /// </summary>
        public string TotalTime { get; set; }

        /// <summary>
        /// Gets or sets success value.
        /// </summary>
        public string Success { get; set; }

        /// <summary>
        /// Gets or sets totalPlayingTime value.
        /// </summary>
        public string TotalPlayingTime { get; set; }

        /// <summary>
        /// Gets or sets startTime value.
        /// </summary>
        public string StartTime { get; set; }

        /// <summary>
        /// Gets or sets locale value.
        /// </summary>
        public string Locale { get; set; }

        /// <summary>
        /// Gets or sets seed value.
        /// </summary>
        public string Seed { get; set; }

        /// <summary>
        /// Gets or sets roundsCount value.
        /// </summary>
        public string RoundsCount { get; set; }

        /// <summary>
        /// Gets parsed round diagnostics.
        /// </summary>
        public List<ResultRoundDiagnostics> Rounds { get; } = new List<ResultRoundDiagnostics>();

        /// <summary>
        /// Gets diagnostic findings.
        /// </summary>
        public List<ResultDiagnosticFinding> Findings { get; } = new List<ResultDiagnosticFinding>();

        /// <summary>
        /// Gets number of error findings.
        /// </summary>
        public int ErrorCount => Findings.Count(finding => finding.Severity == ResultDiagnosticSeverity.Error);

        /// <summary>
        /// Gets number of warning findings.
        /// </summary>
        public int WarningCount => Findings.Count(finding => finding.Severity == ResultDiagnosticSeverity.Warning);

        /// <summary>
        /// Gets number of informational findings.
        /// </summary>
        public int InfoCount => Findings.Count(finding => finding.Severity == ResultDiagnosticSeverity.Info);
        
        /// <summary>
        /// Gets expected customData keys used during diagnostics.
        /// </summary>
        public List<string> ExpectedCustomDataKeys { get; } = new List<string>();
    }
}