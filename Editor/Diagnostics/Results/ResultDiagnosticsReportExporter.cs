using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace BrainIn.DevTools.Editor.Diagnostics.Results
{
    /// <summary>
    /// Exports BrainIn result diagnostics reports to JSON files.
    /// </summary>
    public static class ResultDiagnosticsReportExporter
    {
        /// <summary>
        /// Creates a default file name for a BrainIn result diagnostics report.
        /// </summary>
        /// <returns>Default report file name.</returns>
        public static string CreateDefaultFileName()
        {
            return $"BrainInResultDiagnostics_{DateTime.Now:yyyyMMdd_HHmmss}.json";
        }

        /// <summary>
        /// Exports the diagnostics report to a JSON file.
        /// </summary>
        /// <param name="report">Diagnostics report to export.</param>
        /// <param name="filePath">Target JSON file path.</param>
        public static void Export(ResultDiagnosticsReport report, string filePath)
        {
            if (report == null)
                throw new ArgumentNullException(nameof(report));

            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Export file path cannot be empty.", nameof(filePath));

            var directory = Path.GetDirectoryName(filePath);

            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var exportModel = CreateExportModel(report);
            var json = JsonConvert.SerializeObject(exportModel, Formatting.Indented);

            File.WriteAllText(filePath, json);
        }

        /// <summary>
        /// Creates a serializable export model from the diagnostics report.
        /// </summary>
        /// <param name="report">Diagnostics report.</param>
        /// <returns>Serializable export model.</returns>
        private static ResultDiagnosticsExportModel CreateExportModel(ResultDiagnosticsReport report)
        {
            return new ResultDiagnosticsExportModel
            {
                GeneratedAtUtc = report.GeneratedAtUtc,
                ExpectedCustomDataKeys = report.ExpectedCustomDataKeys.ToList(),
                Summary = new ResultDiagnosticsSummaryExportModel
                {
                    ParseSucceeded = report.ParseSucceeded,
                    Errors = report.ErrorCount,
                    Warnings = report.WarningCount,
                    Infos = report.InfoCount,
                    Rounds = report.Rounds.Count
                },
                GameResult = new ResultDiagnosticsGameResultExportModel
                {
                    TotalTime = report.TotalTime,
                    Success = report.Success,
                    TotalPlayingTime = report.TotalPlayingTime,
                    StartTime = report.StartTime,
                    Locale = report.Locale,
                    Seed = report.Seed,
                    RoundsCount = report.RoundsCount
                },
                Rounds = report.Rounds
                    .Select(CreateRoundExportModel)
                    .ToList(),
                Findings = report.Findings
                    .Select(CreateFindingExportModel)
                    .ToList()
            };
        }

        /// <summary>
        /// Creates a serializable round export model.
        /// </summary>
        /// <param name="round">Round diagnostics.</param>
        /// <returns>Serializable round model.</returns>
        private static ResultDiagnosticsRoundExportModel CreateRoundExportModel(ResultRoundDiagnostics round)
        {
            return new ResultDiagnosticsRoundExportModel
            {
                RoundNumber = round.RoundNumber,
                RoundTime = round.RoundTime,
                PlayingTime = round.PlayingTime,
                Finished = round.Finished,
                Successfully = round.Successfully,
                FinalClickId = round.FinalClickId,
                SelectedAnswer = round.SelectedAnswer,
                SelectedAnswerActionId = round.SelectedAnswerActionId,
                CorrectAnswer = round.CorrectAnswer,
                TimedOut = round.TimedOut,
                ReactionTimeSeconds = round.ReactionTimeSeconds,
                MouseClickCount = round.MouseClickCount,
                KeystrokeCount = round.KeystrokeCount,
                CustomData = new Dictionary<string, string>(round.CustomData)
            };
        }

        /// <summary>
        /// Creates a serializable finding export model.
        /// </summary>
        /// <param name="finding">Diagnostic finding.</param>
        /// <returns>Serializable finding model.</returns>
        private static ResultDiagnosticsFindingExportModel CreateFindingExportModel(ResultDiagnosticFinding finding)
        {
            return new ResultDiagnosticsFindingExportModel
            {
                Severity = finding.Severity.ToString(),
                Title = finding.Title,
                Message = finding.Message,
                Location = finding.Location
            };
        }

        /// <summary>
        /// Serializable root export model.
        /// </summary>
        private sealed class ResultDiagnosticsExportModel
        {
            public DateTime GeneratedAtUtc { get; set; }
            public ResultDiagnosticsSummaryExportModel Summary { get; set; }
            public ResultDiagnosticsGameResultExportModel GameResult { get; set; }
            public List<ResultDiagnosticsRoundExportModel> Rounds { get; set; }
            public List<ResultDiagnosticsFindingExportModel> Findings { get; set; }
            public List<string> ExpectedCustomDataKeys { get; set; }
        }

        /// <summary>
        /// Serializable summary export model.
        /// </summary>
        private sealed class ResultDiagnosticsSummaryExportModel
        {
            public bool ParseSucceeded { get; set; }
            public int Errors { get; set; }
            public int Warnings { get; set; }
            public int Infos { get; set; }
            public int Rounds { get; set; }
        }

        /// <summary>
        /// Serializable top-level game result export model.
        /// </summary>
        private sealed class ResultDiagnosticsGameResultExportModel
        {
            public string TotalTime { get; set; }
            public string Success { get; set; }
            public string TotalPlayingTime { get; set; }
            public string StartTime { get; set; }
            public string Locale { get; set; }
            public string Seed { get; set; }
            public string RoundsCount { get; set; }
        }

        /// <summary>
        /// Serializable round export model.
        /// </summary>
        private sealed class ResultDiagnosticsRoundExportModel
        {
            public int RoundNumber { get; set; }
            public string RoundTime { get; set; }
            public string PlayingTime { get; set; }
            public string Finished { get; set; }
            public string Successfully { get; set; }
            public string FinalClickId { get; set; }
            public string SelectedAnswer { get; set; }
            public string SelectedAnswerActionId { get; set; }
            public string CorrectAnswer { get; set; }
            public string TimedOut { get; set; }
            public string ReactionTimeSeconds { get; set; }
            public int MouseClickCount { get; set; }
            public int KeystrokeCount { get; set; }
            public Dictionary<string, string> CustomData { get; set; }
        }

        /// <summary>
        /// Serializable finding export model.
        /// </summary>
        private sealed class ResultDiagnosticsFindingExportModel
        {
            public string Severity { get; set; }
            public string Title { get; set; }
            public string Message { get; set; }
            public string Location { get; set; }
        }
    }
}