using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BrainIn.DevTools.Editor.Validation.Core;
using UnityEditor;
using UnityEngine;

namespace BrainIn.DevTools.Editor.Reports
{
    /// <summary>
    /// Provides functionality for exporting BrainIn validation results into external report files.
    /// </summary>
    public static class ValidationReportExporter
    {
        /// <summary>
        /// Exports the provided validation results into a machine-readable JSON report.
        /// </summary>
        /// <param name="results">Validation results produced by the validation runner.</param>
        /// <param name="outputPath">Absolute file path where the JSON report should be written.</param>
        /// <exception cref="ArgumentException">Thrown when the output path is null, empty, or whitespace.</exception>
        public static void ExportJson(IReadOnlyList<ValidationResult> results, string outputPath)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentException("Output path cannot be empty.", nameof(outputPath));

            var report = CreateReport(results);
            var json = JsonUtility.ToJson(report, true);

            var directory = Path.GetDirectoryName(outputPath);

            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(outputPath, json);

            Debug.Log($"[BrainIn DevTools] Validation report exported to: {outputPath}");
        }

        /// <summary>
        /// Creates a serializable validation report model from validation results.
        /// </summary>
        /// <param name="results">Validation results to include in the report.</param>
        /// <returns>A serializable report data object.</returns>
        private static ValidationReportData CreateReport(IReadOnlyList<ValidationResult> results)
        {
            var safeResults = results ?? Array.Empty<ValidationResult>();

            return new ValidationReportData
            {
                generatedAtUtc = DateTime.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion,
                projectName = PlayerSettings.productName,
                projectPath = GetProjectRootPath(),
                summary = new ValidationSummaryData
                {
                    errors = safeResults.Count(r => r.Severity == ValidationSeverity.Error),
                    warnings = safeResults.Count(r => r.Severity == ValidationSeverity.Warning),
                    infos = safeResults.Count(r => r.Severity == ValidationSeverity.Info),
                    total = safeResults.Count
                },
                results = safeResults
                    .Select(result => new ValidationResultData
                    {
                        severity = result.Severity.ToString(),
                        ruleName = result.RuleName,
                        message = result.Message,
                        assetPath = result.AssetPath
                    })
                    .ToList()
            };
        }

        /// <summary>
        /// Gets the root directory of the currently opened Unity project.
        /// </summary>
        /// <returns>Absolute path to the Unity project root directory.</returns>
        private static string GetProjectRootPath()
        {
            var dataPath = Application.dataPath;

            if (string.IsNullOrWhiteSpace(dataPath))
                return string.Empty;

            var directory = Directory.GetParent(dataPath);
            return directory?.FullName ?? string.Empty;
        }

        /// <summary>
        /// Serializable root object of the JSON validation report.
        /// </summary>
        [Serializable]
        private sealed class ValidationReportData
        {
            public string generatedAtUtc;
            public string unityVersion;
            public string projectName;
            public string projectPath;
            public ValidationSummaryData summary;
            public List<ValidationResultData> results;
        }

        /// <summary>
        /// Serializable summary section of the validation report.
        /// </summary>
        [Serializable]
        private sealed class ValidationSummaryData
        {
            public int errors;
            public int warnings;
            public int infos;
            public int total;
        }

        /// <summary>
        /// Serializable representation of one validation result.
        /// </summary>
        [Serializable]
        private sealed class ValidationResultData
        {
            public string severity;
            public string ruleName;
            public string message;
            public string assetPath;
        }
    }
}