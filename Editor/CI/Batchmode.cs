using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BrainIn.DevTools.Editor.Reports;
using BrainIn.DevTools.Editor.Validation.Core;
using UnityEditor;
using UnityEngine;

namespace BrainIn.DevTools.Editor.CI
{
    /// <summary>
    /// Provides a command-line entry point for running BrainIn validation in Unity batchmode.
    /// </summary>
    public static class Batchmode
    {
        private const string ReportPathArgumentName = "-braininValidationReportPath";
        private const string DefaultReportDirectoryName = "ValidationReports";
        private const string DefaultReportFileName = "BrainInValidationReport.json";

        /// <summary>
        /// Runs the default BrainIn validation rules, exports a JSON report, and exits Unity with an appropriate exit code.
        /// </summary>
        /// <remarks>
        /// This method is intended to be used through Unity's command-line argument:
        /// -executeMethod BrainIn.DevTools.Editor.CI.Batchmode.Run
        /// </remarks>
        public static void Run()
        {
            try
            {
                var context = CreateValidationContext();
                var runner = ValidationRuleRegistry.CreateDefaultRunner();
                var results = runner.Run(context);

                var reportPath = ResolveReportPath(context.ProjectPath);
                ValidationReportExporter.ExportJson(results, reportPath);

                LogSummary(results, reportPath);

                var exitCode = HasErrors(results) ? 1 : 0;
                Exit(exitCode);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[BrainIn DevTools] Batchmode validation failed: {exception}");
                Exit(1);
            }
        }

        /// <summary>
        /// Creates validation context from the currently opened Unity project.
        /// </summary>
        /// <returns>Validation context containing project and assets paths.</returns>
        private static ValidationContext CreateValidationContext()
        {
            var assetsPath = Application.dataPath;
            var projectPath = GetProjectRootPath(assetsPath);

            return new ValidationContext(projectPath, assetsPath);
        }

        /// <summary>
        /// Resolves the JSON report output path from command-line arguments or returns the default project report path.
        /// </summary>
        /// <param name="projectPath">Absolute path to the Unity project root directory.</param>
        /// <returns>Absolute path where the JSON validation report should be written.</returns>
        private static string ResolveReportPath(string projectPath)
        {
            var commandLineReportPath = GetCommandLineArgumentValue(ReportPathArgumentName);

            if (!string.IsNullOrWhiteSpace(commandLineReportPath))
            {
                return Path.GetFullPath(commandLineReportPath);
            }

            return Path.Combine(
                projectPath,
                DefaultReportDirectoryName,
                DefaultReportFileName
            );
        }

        /// <summary>
        /// Gets the value of a command-line argument in the form "-argumentName value".
        /// </summary>
        /// <param name="argumentName">Name of the command-line argument to find.</param>
        /// <returns>Argument value if found; otherwise null.</returns>
        private static string GetCommandLineArgumentValue(string argumentName)
        {
            var args = Environment.GetCommandLineArgs();

            for (var i = 0; i < args.Length - 1; i++)
            {
                if (!string.Equals(args[i], argumentName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return args[i + 1];
            }

            return null;
        }

        /// <summary>
        /// Logs a short validation summary into the Unity console.
        /// </summary>
        /// <param name="results">Validation results produced by the validation runner.</param>
        /// <param name="reportPath">Path where the JSON report was exported.</param>
        private static void LogSummary(IReadOnlyList<ValidationResult> results, string reportPath)
        {
            var errorCount = CountBySeverity(results, ValidationSeverity.Error);
            var warningCount = CountBySeverity(results, ValidationSeverity.Warning);
            var infoCount = CountBySeverity(results, ValidationSeverity.Info);

            Debug.Log(
                "[BrainIn DevTools] Batchmode validation finished.\n" +
                $"Errors: {errorCount}\n" +
                $"Warnings: {warningCount}\n" +
                $"Infos: {infoCount}\n" +
                $"Total: {results.Count}\n" +
                $"Report: {reportPath}"
            );
        }

        /// <summary>
        /// Determines whether validation produced at least one error.
        /// </summary>
        /// <param name="results">Validation results to inspect.</param>
        /// <returns>True if at least one validation error exists; otherwise false.</returns>
        private static bool HasErrors(IReadOnlyList<ValidationResult> results)
        {
            return results.Any(result => result.Severity == ValidationSeverity.Error);
        }

        /// <summary>
        /// Counts validation results with the specified severity.
        /// </summary>
        /// <param name="results">Validation results to inspect.</param>
        /// <param name="severity">Severity to count.</param>
        /// <returns>Number of validation results with the specified severity.</returns>
        private static int CountBySeverity(
            IReadOnlyList<ValidationResult> results,
            ValidationSeverity severity)
        {
            return results.Count(result => result.Severity == severity);
        }

        /// <summary>
        /// Gets the absolute path to the root directory of the Unity project.
        /// </summary>
        /// <param name="assetsPath">Absolute path to the Unity project's Assets directory.</param>
        /// <returns>Absolute path to the Unity project root directory.</returns>
        private static string GetProjectRootPath(string assetsPath)
        {
            if (string.IsNullOrWhiteSpace(assetsPath))
            {
                return string.Empty;
            }

            var projectDirectory = Directory.GetParent(assetsPath);
            return projectDirectory?.FullName ?? string.Empty;
        }

        /// <summary>
        /// Exits Unity when running in batchmode. In editor mode, only logs the intended exit code.
        /// </summary>
        /// <param name="exitCode">Process exit code to return.</param>
        private static void Exit(int exitCode)
        {
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(exitCode);
                return;
            }

            Debug.Log($"[BrainIn DevTools] Validation finished with exit code {exitCode}. Unity was not closed because it is not running in batchmode.");
        }
    }
}