using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System;
using System.Linq;

namespace BrainIn.DevTools.Editor.Diagnostics.Results.UI
{
    /// <summary>
    /// Editor window for analyzing BrainIn ToWeb_GameFinished result JSON output.
    /// </summary>
    public sealed class ResultDiagnosticsWindow : EditorWindow
    {
        private const string GameFinishedMarker = "ToWeb_GameFinished";
        private const string ContentMarker = "Content:";

        private readonly ResultDiagnosticsAnalyzer _analyzer = new ResultDiagnosticsAnalyzer();
        private readonly Dictionary<int, bool> _roundFoldouts = new Dictionary<int, bool>();

        private string _input = string.Empty;
        private string _captureStatus = "Capture is stopped.";
        private ResultDiagnosticsReport _report;
        private Vector2 _inputScroll;
        private Vector2 _resultsScroll;
        private bool _isCapturing;
        private bool _autoAnalyze = true;
        private int _capturedResultCount;
        private string _expectedCustomDataKeysText = string.Empty;
        private string _expectedCustomDataKeysStatus = "No expected customData keys loaded.";
        private Vector2 _expectedCustomDataKeysScroll;
        private bool _useExpectedCustomDataKeys = true;

        /// <summary>
        /// Opens the BrainIn result diagnostics window.
        /// </summary>
        [MenuItem("BrainIn/DevTools/Result Diagnostics")]
        public static void Open()
        {
            var window = GetWindow<ResultDiagnosticsWindow>("Result Diagnostics");
            window.minSize = new Vector2(760f, 540f);
            window.Show();
        }

        /// <summary>
        /// Unsubscribes from Unity log capture when the window is closed or reloaded.
        /// </summary>
        private void OnDisable()
        {
            StopCapture();
        }

        /// <summary>
        /// Draws the editor window GUI.
        /// </summary>
        private void OnGUI()
        {
            DrawHeader();
            DrawCaptureControls();
            DrawExpectedCustomDataContractControls();
            DrawInput();
            DrawActions();

            EditorGUILayout.Space(8f);

            _resultsScroll = EditorGUILayout.BeginScrollView(_resultsScroll);
            DrawReport();
            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// Draws window header.
        /// </summary>
        private static void DrawHeader()
        {
            EditorGUILayout.LabelField("BrainIn Result Diagnostics", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Analyze BrainIn ToWeb_GameFinished output. You can paste JSON/log manually, or start capture before running the game in Play Mode.",
                MessageType.Info
            );
        }

        /// <summary>
        /// Draws log capture controls.
        /// </summary>
        private void DrawCaptureControls()
        {
            EditorGUILayout.LabelField("Automatic Log Capture", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField("Status", _captureStatus);
            EditorGUILayout.LabelField("Captured results", _capturedResultCount.ToString());

            _autoAnalyze = EditorGUILayout.ToggleLeft(
                "Analyze automatically when ToWeb_GameFinished is captured",
                _autoAnalyze
            );

            EditorGUILayout.BeginHorizontal();

            GUI.enabled = !_isCapturing;
            if (GUILayout.Button("Start Capture", GUILayout.Width(140f)))
            {
                StartCapture();
            }

            GUI.enabled = _isCapturing;
            if (GUILayout.Button("Stop Capture", GUILayout.Width(140f)))
            {
                StopCapture();
            }

            GUI.enabled = true;

            if (GUILayout.Button("Clear Captured Result", GUILayout.Width(180f)))
            {
                _input = string.Empty;
                _report = null;
                _roundFoldouts.Clear();
                _capturedResultCount = 0;
                _captureStatus = _isCapturing
                    ? "Capture is running. Waiting for ToWeb_GameFinished..."
                    : "Capture is stopped.";
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox(
                "Capture only receives new Unity log messages while this window is open and capture is running. It does not read old Console entries.",
                MessageType.None
            );

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Draws expected customData contract controls.
        /// </summary>
        private void DrawExpectedCustomDataContractControls()
        {
            EditorGUILayout.LabelField("Expected customData Contract", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _useExpectedCustomDataKeys = EditorGUILayout.ToggleLeft(
                "Validate expected customData keys",
                _useExpectedCustomDataKeys
            );

            EditorGUILayout.LabelField("Status", _expectedCustomDataKeysStatus);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Load From Open Scene", GUILayout.Width(170f)))
            {
                LoadExpectedCustomDataKeysFromOpenScene();
            }

            if (GUILayout.Button("Clear Expected Keys", GUILayout.Width(160f)))
            {
                _expectedCustomDataKeysText = string.Empty;
                _expectedCustomDataKeysStatus = "No expected customData keys loaded.";
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("Expected keys");
            _expectedCustomDataKeysScroll = EditorGUILayout.BeginScrollView(
                _expectedCustomDataKeysScroll,
                GUILayout.MinHeight(70f),
                GUILayout.MaxHeight(120f)
            );

            _expectedCustomDataKeysText = EditorGUILayout.TextArea(_expectedCustomDataKeysText);

            EditorGUILayout.EndScrollView();

            EditorGUILayout.HelpBox(
                "One key per line. Keys can be loaded from ExpectedCustomDataKey attributes on the custom game controller.",
                MessageType.None
            );

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Draws input text area.
        /// </summary>
        private void DrawInput()
        {
            EditorGUILayout.LabelField("Captured or Pasted Input", EditorStyles.boldLabel);

            _inputScroll = EditorGUILayout.BeginScrollView(
                _inputScroll,
                GUILayout.MinHeight(140f),
                GUILayout.MaxHeight(240f)
            );

            _input = EditorGUILayout.TextArea(_input, GUILayout.ExpandHeight(true));

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// Draws action buttons.
        /// </summary>
        private void DrawActions()
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Paste From Clipboard", GUILayout.Width(160f)))
            {
                _input = GUIUtility.systemCopyBuffer ?? string.Empty;
            }

            if (GUILayout.Button("Analyze", GUILayout.Width(120f)))
            {
                AnalyzeCurrentInput();
            }

            GUI.enabled = _report != null;
            if (GUILayout.Button("Export JSON Report", GUILayout.Width(160f)))
            {
                ExportCurrentReport();
            }

            GUI.enabled = true;

            if (GUILayout.Button("Clear", GUILayout.Width(120f)))
            {
                _input = string.Empty;
                _report = null;
                _roundFoldouts.Clear();
            }

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// Starts listening to Unity log messages.
        /// </summary>
        private void StartCapture()
        {
            if (_isCapturing)
                return;

            Application.logMessageReceived += OnLogMessageReceived;
            _isCapturing = true;
            _captureStatus = "Capture is running. Waiting for ToWeb_GameFinished...";
        }

        /// <summary>
        /// Stops listening to Unity log messages.
        /// </summary>
        private void StopCapture()
        {
            if (!_isCapturing)
                return;

            Application.logMessageReceived -= OnLogMessageReceived;
            _isCapturing = false;
            _captureStatus = "Capture is stopped.";
        }

        /// <summary>
        /// Handles Unity log messages and captures BrainIn ToWeb_GameFinished output.
        /// </summary>
        /// <param name="condition">Logged message.</param>
        /// <param name="stackTrace">Stack trace associated with the log.</param>
        /// <param name="type">Unity log type.</param>
        private void OnLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            if (!ContainsGameFinishedResult(condition))
                return;

            _capturedResultCount++;
            _input = condition;
            _captureStatus = $"Captured ToWeb_GameFinished result #{_capturedResultCount}.";

            if (_autoAnalyze)
                AnalyzeCurrentInput();

            Repaint();
        }

        /// <summary>
        /// Determines whether a log message contains BrainIn game finished result output.
        /// </summary>
        /// <param name="condition">Log message.</param>
        /// <returns>True if the message contains game finished output; otherwise false.</returns>
        private static bool ContainsGameFinishedResult(string condition)
        {
            return !string.IsNullOrWhiteSpace(condition) &&
                   condition.Contains(GameFinishedMarker) &&
                   condition.Contains(ContentMarker);
        }

        /// <summary>
        /// Analyzes the current input text.
        /// </summary>
        private void AnalyzeCurrentInput()
        {
            _report = _analyzer.Analyze(_input, GetExpectedCustomDataKeys());
            _roundFoldouts.Clear();
        }

        /// <summary>
        /// Draws parsed diagnostics report.
        /// </summary>
        private void DrawReport()
        {
            if (_report == null)
                return;

            DrawSummary();
            EditorGUILayout.Space(8f);
            DrawFindings();
            EditorGUILayout.Space(8f);
            DrawRounds();
        }

        /// <summary>
        /// Draws report summary.
        /// </summary>
        private void DrawSummary()
        {
            EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawSummaryRow("Parse succeeded", _report.ParseSucceeded ? "Yes" : "No");
            DrawSummaryRow("totalTime", _report.TotalTime);
            DrawSummaryRow("success", _report.Success);
            DrawSummaryRow("totalPlayingTime", _report.TotalPlayingTime);
            DrawSummaryRow("roundsCount", _report.RoundsCount);
            DrawSummaryRow("parsed rounds", _report.Rounds.Count.ToString());
            DrawSummaryRow("locale", _report.Locale);
            DrawSummaryRow("seed", _report.Seed);
            DrawSummaryRow(
                "findings",
                $"Errors: {_report.ErrorCount}, Warnings: {_report.WarningCount}, Infos: {_report.InfoCount}"
            );
            DrawSummaryRow("expected customData keys", _report.ExpectedCustomDataKeys.Count.ToString());
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Draws one summary row.
        /// </summary>
        /// <param name="label">Row label.</param>
        /// <param name="value">Row value.</param>
        private static void DrawSummaryRow(string label, string value)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(180f));
            EditorGUILayout.SelectableLabel(value ?? "-", GUILayout.Height(EditorGUIUtility.singleLineHeight));
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// Draws diagnostic findings.
        /// </summary>
        private void DrawFindings()
        {
            EditorGUILayout.LabelField("Findings", EditorStyles.boldLabel);

            if (_report.Findings.Count == 0)
            {
                EditorGUILayout.HelpBox("No findings.", MessageType.Info);
                return;
            }

            foreach (var finding in _report.Findings)
            {
                var locationPrefix = string.IsNullOrWhiteSpace(finding.Location)
                    ? string.Empty
                    : $"{finding.Location}: ";

                EditorGUILayout.HelpBox(
                    $"{locationPrefix}{finding.Title}\n{finding.Message}",
                    ToMessageType(finding.Severity)
                );
            }
        }

        /// <summary>
        /// Draws round summaries.
        /// </summary>
        private void DrawRounds()
        {
            EditorGUILayout.LabelField("Rounds", EditorStyles.boldLabel);

            if (_report.Rounds.Count == 0)
            {
                EditorGUILayout.HelpBox("No rounds were parsed.", MessageType.Info);
                return;
            }

            foreach (var round in _report.Rounds)
            {
                if (!_roundFoldouts.ContainsKey(round.RoundNumber))
                    _roundFoldouts[round.RoundNumber] = false;

                var roundTitle = CreateRoundTitle(round);
                _roundFoldouts[round.RoundNumber] = EditorGUILayout.Foldout(
                    _roundFoldouts[round.RoundNumber],
                    roundTitle,
                    true
                );

                if (!_roundFoldouts[round.RoundNumber])
                    continue;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                DrawSummaryRow("roundTime", round.RoundTime);
                DrawSummaryRow("playingTime", round.PlayingTime);
                DrawSummaryRow("finished", round.Finished);
                DrawSummaryRow("successfully", round.Successfully);
                DrawSummaryRow("finalClickId", round.FinalClickId);
                DrawSummaryRow("selectedAnswer", round.SelectedAnswer);
                DrawSummaryRow("selectedAnswerActionId", round.SelectedAnswerActionId);
                DrawSummaryRow("correctAnswer", round.CorrectAnswer);
                DrawSummaryRow("timedOut", round.TimedOut);
                DrawSummaryRow("reactionTimeSeconds", round.ReactionTimeSeconds);
                DrawSummaryRow("mouseClicks", round.MouseClickCount.ToString());
                DrawSummaryRow("keystrokes", round.KeystrokeCount.ToString());

                DrawCustomData(round);

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4f);
            }
        }

        /// <summary>
        /// Draws parsed custom data.
        /// </summary>
        /// <param name="round">Round diagnostics.</param>
        private static void DrawCustomData(ResultRoundDiagnostics round)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("customData", EditorStyles.boldLabel);

            if (round.CustomData.Count == 0)
            {
                EditorGUILayout.LabelField("-");
                return;
            }

            foreach (var pair in round.CustomData)
            {
                DrawSummaryRow(pair.Key, pair.Value);
            }
        }

        /// <summary>
        /// Creates foldout title for one round.
        /// </summary>
        /// <param name="round">Round diagnostics.</param>
        /// <returns>Foldout title.</returns>
        private static string CreateRoundTitle(ResultRoundDiagnostics round)
        {
            var result = round.Successfully == "1" || round.Successfully == "true"
                ? "success"
                : "failed";

            var answer = string.IsNullOrWhiteSpace(round.SelectedAnswer)
                ? "no selectedAnswer"
                : round.SelectedAnswer;

            return $"Round {round.RoundNumber}: {result}, answer={answer}, finalClickId={round.FinalClickId ?? "null"}";
        }

        /// <summary>
        /// Converts diagnostic severity to Unity message type.
        /// </summary>
        /// <param name="severity">Diagnostic severity.</param>
        /// <returns>Unity message type.</returns>
        private static MessageType ToMessageType(ResultDiagnosticSeverity severity)
        {
            switch (severity)
            {
                case ResultDiagnosticSeverity.Error:
                    return MessageType.Error;
                case ResultDiagnosticSeverity.Warning:
                    return MessageType.Warning;
                default:
                    return MessageType.Info;
            }
        }

        /// <summary>
        /// Exports the currently analyzed diagnostics report to a JSON file.
        /// </summary>
        private void ExportCurrentReport()
        {
            if (_report == null)
                return;

            var filePath = EditorUtility.SaveFilePanel(
                "Export BrainIn Result Diagnostics Report",
                "Assets",
                ResultDiagnosticsReportExporter.CreateDefaultFileName(),
                "json"
            );

            if (string.IsNullOrWhiteSpace(filePath))
                return;

            try
            {
                ResultDiagnosticsReportExporter.Export(_report, filePath);

                if (filePath.StartsWith(Application.dataPath, StringComparison.Ordinal))
                    AssetDatabase.Refresh();

                EditorUtility.DisplayDialog(
                    "Result Diagnostics Export",
                    "BrainIn result diagnostics report was exported successfully.",
                    "OK"
                );
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog(
                    "Result Diagnostics Export Failed",
                    exception.Message,
                    "OK"
                );
            }
        }

        /// <summary>
        /// Loads expected customData keys from the currently open scene.
        /// </summary>
        private void LoadExpectedCustomDataKeysFromOpenScene()
        {
            var result = ExpectedCustomDataContractLoader.LoadFromOpenScenes();

            _expectedCustomDataKeysText = string.Join(Environment.NewLine, result.Keys);

            if (result.Keys.Count > 0)
            {
                var sources = result.SourceNames.Count == 0
                    ? "unknown source"
                    : string.Join(", ", result.SourceNames);

                _expectedCustomDataKeysStatus = $"Loaded {result.Keys.Count} expected key(s) from {sources}.";
            }
            else
            {
                _expectedCustomDataKeysStatus = "No expected customData keys were loaded.";
            }

            foreach (var warning in result.Warnings)
            {
                Debug.LogWarning($"[BrainIn Result Diagnostics] {warning}");
            }
        }

        /// <summary>
        /// Gets expected customData keys entered in the diagnostics window.
        /// </summary>
        /// <returns>Expected customData keys.</returns>
        private IReadOnlyCollection<string> GetExpectedCustomDataKeys()
        {
            if (!_useExpectedCustomDataKeys)
                return Array.Empty<string>();

            return _expectedCustomDataKeysText
                .Split(new[] { '\r', '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(key => key.Trim())
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Distinct()
                .ToList();
        }
    }
}