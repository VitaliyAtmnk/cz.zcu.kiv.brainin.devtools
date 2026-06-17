using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BrainIn.DevTools.Editor.Reports;
using BrainIn.DevTools.Editor.Validation.Core;
using UnityEditor;
using UnityEngine;

namespace BrainIn.DevTools.Editor.UI
{
    /// <summary>
    /// Unity Editor window that provides access to BrainIn validation and diagnostic tools.
    /// </summary>
    public sealed class BrainInDevToolsWindow : EditorWindow
    {
        private IReadOnlyList<ValidationResult> _results = new List<ValidationResult>();
        private Vector2 _scrollPosition;
        private DateTime? _lastValidationTime;

        private bool _showErrors = true;
        private bool _showWarnings = true;
        private bool _showInfos = true;

        /// <summary>
        /// Opens the BrainIn Development Tools window from the Unity Editor menu.
        /// </summary>
        [MenuItem("BrainIn/DevTools/Validation Report")]
        public static void Open()
        {
            var window = GetWindow<BrainInDevToolsWindow>();
            window.titleContent = new GUIContent("BrainIn DevTools");
            window.minSize = new Vector2(760, 540);
            window.Show();
        }

        /// <summary>
        /// Draws the full editor window user interface.
        /// </summary>
        private void OnGUI()
        {
            DrawHeader();
            DrawToolbar();
            DrawSummary();
            DrawFilters();
            DrawResults();
        }

        /// <summary>
        /// Draws the title and short description of the tool window.
        /// </summary>
        private void DrawHeader()
        {
            GUILayout.Label("BrainIn Development Tools", EditorStyles.boldLabel);
            GUILayout.Space(4);

            EditorGUILayout.HelpBox(
                "Validation and diagnostic tools for BrainIn Unity tasks.",
                MessageType.Info
            );

            GUILayout.Space(8);
        }

        /// <summary>
        /// Draws the main action buttons for running validation, clearing results, and exporting reports.
        /// </summary>
        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Run Validation", GUILayout.Height(28)))
                {
                    RunValidation();
                }

                if (GUILayout.Button("Clear Results", GUILayout.Height(28)))
                {
                    ClearResults();
                }

                using (new EditorGUI.DisabledScope(_results.Count == 0))
                {
                    if (GUILayout.Button("Export JSON Report", GUILayout.Height(28)))
                    {
                        ExportJsonReport();
                    }
                }
            }

            GUILayout.Space(8);
        }

        /// <summary>
        /// Draws a summary of the latest validation run.
        /// </summary>
        private void DrawSummary()
        {
            var errorCount = CountBySeverity(ValidationSeverity.Error);
            var warningCount = CountBySeverity(ValidationSeverity.Warning);
            var infoCount = CountBySeverity(ValidationSeverity.Info);

            EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Errors", errorCount.ToString());
                EditorGUILayout.LabelField("Warnings", warningCount.ToString());
                EditorGUILayout.LabelField("Infos", infoCount.ToString());
                EditorGUILayout.LabelField("Total", _results.Count.ToString());

                if (_lastValidationTime.HasValue)
                {
                    EditorGUILayout.LabelField(
                        "Last validation",
                        _lastValidationTime.Value.ToString("yyyy-MM-dd HH:mm:ss")
                    );
                }
            }

            GUILayout.Space(8);
        }

        /// <summary>
        /// Draws severity filters for validation results.
        /// </summary>
        private void DrawFilters()
        {
            EditorGUILayout.LabelField("Filters", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                _showErrors = EditorGUILayout.ToggleLeft("Errors", _showErrors, GUILayout.Width(90));
                _showWarnings = EditorGUILayout.ToggleLeft("Warnings", _showWarnings, GUILayout.Width(110));
                _showInfos = EditorGUILayout.ToggleLeft("Infos", _showInfos, GUILayout.Width(90));
            }

            GUILayout.Space(8);
        }

        /// <summary>
        /// Draws the list of validation results in a scrollable area.
        /// </summary>
        private void DrawResults()
        {
            EditorGUILayout.LabelField("Results", EditorStyles.boldLabel);

            if (_results.Count == 0)
            {
                EditorGUILayout.HelpBox("No validation results yet.", MessageType.Info);
                return;
            }

            var visibleResults = GetVisibleResults();

            if (visibleResults.Count == 0)
            {
                EditorGUILayout.HelpBox("No validation results match the selected filters.", MessageType.Info);
                return;
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            foreach (var result in visibleResults)
                DrawResultCard(result);

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// Draws a single validation result including a severity icon and an optional asset action button.
        /// </summary>
        /// <param name="result">Validation result to draw.</param>
        private void DrawResultCard(ValidationResult result)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var message = $"[{result.Severity}] [{result.RuleName}]\n{result.Message}";
        
                EditorGUILayout.HelpBox(
                    message,
                    ToMessageType(result.Severity)
                );
        
                if (!string.IsNullOrWhiteSpace(result.AssetPath))
                {
                    GUILayout.Space(4);
        
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.TextField("Asset", result.AssetPath);
        
                        if (GUILayout.Button("Open Asset", GUILayout.Width(100)))
                            OpenAsset(result.AssetPath);
                    }
                }
            }
        
            GUILayout.Space(4);
        }

        /// <summary>
        /// Runs all currently registered validation rules and stores their results for display.
        /// </summary>
        private void RunValidation()
        {
            var context = new ValidationContext(
                GetProjectRootPath(),
                Application.dataPath
            );

            var runner = ValidationRuleRegistry.CreateDefaultRunner();

            _results = runner.Run(context);
            _lastValidationTime = DateTime.Now;

            Debug.Log($"[BrainIn DevTools] Validation finished. Results: {_results.Count}");
        }

        /// <summary>
        /// Clears all validation results from the window.
        /// </summary>
        private void ClearResults()
        {
            _results = new List<ValidationResult>();
            _lastValidationTime = null;
            Debug.Log("[BrainIn DevTools] Validation results cleared.");
        }

        /// <summary>
        /// Opens a save file dialog and exports the current validation results into a JSON report.
        /// </summary>
        private void ExportJsonReport()
        {
            var defaultFileName = $"BrainInValidationReport_{DateTime.Now:yyyyMMdd_HHmmss}.json";

            var outputPath = EditorUtility.SaveFilePanel(
                "Export BrainIn Validation Report",
                Application.dataPath,
                defaultFileName,
                "json"
            );

            if (string.IsNullOrWhiteSpace(outputPath))
                return;

            try
            {
                ValidationReportExporter.ExportJson(_results, outputPath);
                EditorUtility.RevealInFinder(outputPath);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[BrainIn DevTools] Failed to export validation report: {exception}");
                EditorUtility.DisplayDialog(
                    "Export failed",
                    $"Failed to export validation report:\n{exception.Message}",
                    "OK"
                );
            }
        }

        /// <summary>
        /// Opens and highlights a Unity asset referenced by a validation result.
        /// </summary>
        /// <param name="assetPath">Unity asset path to open.</param>
        private static void OpenAsset(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                return;

            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);

            if (!asset)
            {
                EditorUtility.DisplayDialog(
                    "Asset not found",
                    $"Could not find asset at path:\n{assetPath}",
                    "OK"
                );

                return;
            }

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            AssetDatabase.OpenAsset(asset);
        }

        /// <summary>
        /// Gets validation results matching the currently selected severity filters.
        /// </summary>
        /// <returns>Filtered and sorted validation results.</returns>
        private IReadOnlyList<ValidationResult> GetVisibleResults()
        {
            return _results
                .Where(IsVisibleByFilter)
                .OrderBy(result => GetSeveritySortOrder(result.Severity))
                .ThenBy(result => result.RuleName)
                .ThenBy(result => result.AssetPath)
                .ToList();
        }

        /// <summary>
        /// Determines whether a validation result should be visible based on the selected severity filters.
        /// </summary>
        /// <param name="result">Validation result to check.</param>
        /// <returns>True if the result should be visible; otherwise false.</returns>
        private bool IsVisibleByFilter(ValidationResult result)
        {
            return result.Severity switch
            {
                ValidationSeverity.Error => _showErrors,
                ValidationSeverity.Warning => _showWarnings,
                ValidationSeverity.Info => _showInfos,
                _ => true
            };
        }

        /// <summary>
        /// Counts validation results with the specified severity.
        /// </summary>
        /// <param name="severity">Severity to count.</param>
        /// <returns>Number of validation results with the specified severity.</returns>
        private int CountBySeverity(ValidationSeverity severity)
        {
            return _results.Count(result => result.Severity == severity);
        }

        /// <summary>
        /// Gets a numeric sort order for validation severities.
        /// </summary>
        /// <param name="severity">Validation severity.</param>
        /// <returns>Sort order where lower value means higher priority.</returns>
        private static int GetSeveritySortOrder(ValidationSeverity severity)
        {
            return severity switch
            {
                ValidationSeverity.Error => 0,
                ValidationSeverity.Warning => 1,
                ValidationSeverity.Info => 2,
                _ => 3
            };
        }

        /// <summary>
        /// Gets the absolute path to the root directory of the currently opened Unity project.
        /// </summary>
        /// <returns>Absolute path to the Unity project root directory.</returns>
        private static string GetProjectRootPath()
        {
            var assetsPath = Application.dataPath;

            if (string.IsNullOrWhiteSpace(assetsPath))
                return string.Empty;

            var projectDirectory = Directory.GetParent(assetsPath);
            return projectDirectory?.FullName ?? string.Empty;
        }
  
		/// <summary>
        /// Converts validation severity into a Unity Editor message type.
        /// </summary>
        /// <param name="severity">Validation severity to convert.</param>
        /// <returns>Corresponding Unity Editor message type.</returns>
        private static MessageType ToMessageType(ValidationSeverity severity)
        {
            return severity switch
            {
                ValidationSeverity.Error => MessageType.Error,
                ValidationSeverity.Warning => MessageType.Warning,
                ValidationSeverity.Info => MessageType.Info,
                _ => MessageType.None
            };
        }
  }
}