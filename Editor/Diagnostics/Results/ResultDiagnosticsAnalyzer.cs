using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace BrainIn.DevTools.Editor.Diagnostics.Results
{
    /// <summary>
    /// Analyzes BrainIn ToWeb_GameFinished JSON output.
    /// </summary>
    public sealed class ResultDiagnosticsAnalyzer
    {
        /// <summary>
        /// Analyzes a BrainIn result JSON or Unity log containing "Content: { ... }".
        /// </summary>
        /// <param name="input">Raw JSON or Unity console log text.</param>
        /// <returns>Diagnostic report.</returns>
        public ResultDiagnosticsReport Analyze(string input)
        {
            return Analyze(input, Array.Empty<string>());
        }

        /// <summary>
        /// Analyzes a BrainIn result JSON or Unity log containing "Content: { ... }".
        /// </summary>
        /// <param name="input">Raw JSON or Unity console log text.</param>
        /// <param name="expectedCustomDataKeys">Expected customData keys.</param>
        /// <returns>Diagnostic report.</returns>
        public ResultDiagnosticsReport Analyze(string input, IEnumerable<string> expectedCustomDataKeys)
        {
            var report = new ResultDiagnosticsReport();

            foreach (var key in NormalizeExpectedCustomDataKeys(expectedCustomDataKeys))
            {
                report.ExpectedCustomDataKeys.Add(key);
            }

            if (string.IsNullOrWhiteSpace(input))
            {
                report.Findings.Add(new ResultDiagnosticFinding(
                    ResultDiagnosticSeverity.Error,
                    "Empty input",
                    "Paste a BrainIn ToWeb_GameFinished JSON or a Unity log containing Content: { ... }."
                ));

                return report;
            }

            if (!TryExtractJson(input, out var json, out var extractionError))
            {
                report.Findings.Add(new ResultDiagnosticFinding(
                    ResultDiagnosticSeverity.Error,
                    "JSON not found",
                    extractionError
                ));

                return report;
            }

            JObject root;

            try
            {
                root = JObject.Parse(json);
                report.ParseSucceeded = true;
            }
            catch (Exception exception)
            {
                report.Findings.Add(new ResultDiagnosticFinding(
                    ResultDiagnosticSeverity.Error,
                    "Invalid JSON",
                    exception.Message
                ));

                return report;
            }

            PopulateSummary(root, report);
            AnalyzeTopLevel(root, report);
            AnalyzeRounds(root, report);

            if (report.Findings.Count == 0)
            {
                report.Findings.Add(new ResultDiagnosticFinding(
                    ResultDiagnosticSeverity.Info,
                    "Result output looks consistent",
                    "No structural or consistency issues were detected in the BrainIn result output."
                ));
            }

            return report;
        }

        /// <summary>
        /// Populates top-level report summary values.
        /// </summary>
        /// <param name="root">Parsed JSON root.</param>
        /// <param name="report">Report to populate.</param>
        private static void PopulateSummary(JObject root, ResultDiagnosticsReport report)
        {
            report.TotalTime = GetString(root, "totalTime");
            report.Success = GetString(root, "success");
            report.TotalPlayingTime = GetString(root, "totalPlayingTime");
            report.StartTime = GetString(root, "startTime");
            report.Locale = GetString(root, "locale");
            report.Seed = GetString(root, "seed");
            report.RoundsCount = GetString(root, "roundsCount");
        }

        /// <summary>
        /// Analyzes required top-level fields.
        /// </summary>
        /// <param name="root">Parsed JSON root.</param>
        /// <param name="report">Report to append findings to.</param>
        private static void AnalyzeTopLevel(JObject root, ResultDiagnosticsReport report)
        {
            RequireTopLevelField(root, report, "totalTime");
            RequireTopLevelField(root, report, "success");
            RequireTopLevelField(root, report, "totalPlayingTime");
            RequireTopLevelField(root, report, "roundsCount");
            RequireTopLevelField(root, report, "seed");
            RequireTopLevelField(root, report, "locale");

            ValidateTopLevelNumber(GetString(root, "totalTime"), "totalTime", report, true);
            ValidateTopLevelNumber(GetString(root, "totalPlayingTime"), "totalPlayingTime", report, true);
            ValidateTopLevelNumber(GetString(root, "success"), "success", report, false);

            if (root["rounds"] is JArray)
                return;

            report.Findings.Add(new ResultDiagnosticFinding(
                ResultDiagnosticSeverity.Error,
                "Missing rounds",
                "The result JSON does not contain a valid 'rounds' array."
            ));
        }

        /// <summary>
        /// Analyzes all rounds.
        /// </summary>
        /// <param name="root">Parsed JSON root.</param>
        /// <param name="report">Report to append findings to.</param>
        private static void AnalyzeRounds(JObject root, ResultDiagnosticsReport report)
        {
            if (!(root["rounds"] is JArray rounds))
                return;

            ValidateRoundsCount(root, rounds, report);

            for (var index = 0; index < rounds.Count; index++)
            {
                if (!(rounds[index] is JObject round))
                {
                    report.Findings.Add(new ResultDiagnosticFinding(
                        ResultDiagnosticSeverity.Error,
                        "Invalid round",
                        "Round entry is not a JSON object.",
                        $"Round {index + 1}"
                    ));

                    continue;
                }

                AnalyzeRound(round, index + 1, report);
            }
        }

        /// <summary>
        /// Analyzes one round.
        /// </summary>
        /// <param name="round">Round JSON object.</param>
        /// <param name="roundNumber">One-based round number.</param>
        /// <param name="report">Report to append findings to.</param>
        private static void AnalyzeRound(JObject round, int roundNumber, ResultDiagnosticsReport report)
        {
            var roundSummary = new ResultRoundDiagnostics
            {
                RoundNumber = roundNumber,
                RoundTime = GetString(round, "roundTime"),
                PlayingTime = GetString(round, "playingTime"),
                Finished = GetString(round, "finished"),
                Successfully = GetString(round, "successfully"),
                FinalClickId = GetString(round, "finalClickId"),
                MouseClickCount = GetArrayCount(round, "mouseClicks"),
                KeystrokeCount = GetArrayCount(round, "keystrokes")
            };

            RequireRoundField(round, report, roundNumber, "roundTime");
            RequireRoundField(round, report, roundNumber, "playingTime");
            RequireRoundField(round, report, roundNumber, "finished");
            RequireRoundField(round, report, roundNumber, "successfully");
            RequireRoundField(round, report, roundNumber, "finalClickId");

            ParseCustomData(round, roundNumber, report, roundSummary);

            roundSummary.SelectedAnswer = GetCustom(roundSummary, "selectedAnswer");
            roundSummary.SelectedAnswerActionId = GetCustom(roundSummary, "selectedAnswerActionId");
            roundSummary.CorrectAnswer = GetCustom(roundSummary, "correctAnswer");
            roundSummary.TimedOut = GetCustom(roundSummary, "timedOut");
            roundSummary.ReactionTimeSeconds = GetCustom(roundSummary, "reactionTimeSeconds");

            ValidateRoundNumbers(roundSummary, report);
            ValidateTimeoutConsistency(roundSummary, report);
            ValidateClickConsistency(roundSummary, report);
            ValidateSuccessConsistency(roundSummary, report);
            ValidateExpectedCustomDataKeys(roundSummary, report);

            report.Rounds.Add(roundSummary);
        }

        /// <summary>
        /// Validates whether roundsCount matches the actual rounds array count.
        /// </summary>
        /// <param name="root">Parsed JSON root.</param>
        /// <param name="rounds">Rounds array.</param>
        /// <param name="report">Report to append findings to.</param>
        private static void ValidateRoundsCount(JObject root, JArray rounds, ResultDiagnosticsReport report)
        {
            var roundsCountRaw = GetString(root, "roundsCount");

            if (string.IsNullOrWhiteSpace(roundsCountRaw))
                return;

            if (!int.TryParse(roundsCountRaw, NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out var expectedRoundsCount))
            {
                report.Findings.Add(new ResultDiagnosticFinding(
                    ResultDiagnosticSeverity.Warning,
                    "Invalid roundsCount",
                    $"Value '{roundsCountRaw}' is not a valid integer."
                ));

                return;
            }

            if (expectedRoundsCount == rounds.Count)
                return;

            report.Findings.Add(new ResultDiagnosticFinding(
                ResultDiagnosticSeverity.Warning,
                "Rounds count mismatch",
                $"roundsCount is {expectedRoundsCount}, but the rounds array contains {rounds.Count} entries."
            ));
        }

        /// <summary>
        /// Parses BrainIn customData array into key-value pairs.
        /// </summary>
        /// <param name="round">Round JSON object.</param>
        /// <param name="roundNumber">One-based round number.</param>
        /// <param name="report">Report to append findings to.</param>
        /// <param name="roundSummary">Round summary to populate.</param>
        private static void ParseCustomData(
            JObject round,
            int roundNumber,
            ResultDiagnosticsReport report,
            ResultRoundDiagnostics roundSummary)
        {
            if (!(round["customData"] is JArray customData))
            {
                report.Findings.Add(new ResultDiagnosticFinding(
                    ResultDiagnosticSeverity.Warning,
                    "Missing customData",
                    "Round does not contain a valid customData array.",
                    $"Round {roundNumber}"
                ));

                return;
            }

            foreach (var entryToken in customData)
            {
                var entry = entryToken?.ToString();

                if (string.IsNullOrWhiteSpace(entry))
                    continue;

                var separatorIndex = entry.IndexOf('=');

                if (separatorIndex <= 0)
                {
                    report.Findings.Add(new ResultDiagnosticFinding(
                        ResultDiagnosticSeverity.Warning,
                        "Invalid customData entry",
                        $"Custom data entry '{entry}' is not in key=value format.",
                        $"Round {roundNumber}"
                    ));

                    continue;
                }

                var key = entry.Substring(0, separatorIndex).Trim();
                var value = entry.Substring(separatorIndex + 1).Trim();

                if (roundSummary.CustomData.ContainsKey(key))
                {
                    report.Findings.Add(new ResultDiagnosticFinding(
                        ResultDiagnosticSeverity.Warning,
                        "Duplicate customData key",
                        $"Custom data key '{key}' appears multiple times. The last value is used in diagnostics.",
                        $"Round {roundNumber}"
                    ));
                }

                roundSummary.CustomData[key] = value;
            }
        }

        /// <summary>
        /// Validates numeric values in a round.
        /// </summary>
        /// <param name="round">Round summary.</param>
        /// <param name="report">Report to append findings to.</param>
        private static void ValidateRoundNumbers(ResultRoundDiagnostics round, ResultDiagnosticsReport report)
        {
            ValidateNumber(round.RoundTime, "roundTime", report, round.RoundNumber, true);
            ValidateNumber(round.PlayingTime, "playingTime", report, round.RoundNumber, true);

            if (round.CustomData.ContainsKey("displayTimeSeconds"))
                ValidateNumber(round.CustomData["displayTimeSeconds"], "displayTimeSeconds", report, round.RoundNumber,
                    true);

            if (round.CustomData.ContainsKey("reactionTimeSeconds"))
                ValidateNumber(round.CustomData["reactionTimeSeconds"], "reactionTimeSeconds", report,
                    round.RoundNumber, true);
        }

        /// <summary>
        /// Validates timeout-specific consistency.
        /// </summary>
        /// <param name="round">Round summary.</param>
        /// <param name="report">Report to append findings to.</param>
        private static void ValidateTimeoutConsistency(ResultRoundDiagnostics round, ResultDiagnosticsReport report)
        {
            if (!TryParseBrainInBool(round.TimedOut, out var timedOut))
                return;

            if (!timedOut)
                return;

            if (!IsNullLike(round.FinalClickId))
            {
                report.Findings.Add(new ResultDiagnosticFinding(
                    ResultDiagnosticSeverity.Warning,
                    "Timeout has final click",
                    "Round is marked as timedOut=true, but finalClickId is not null.",
                    $"Round {round.RoundNumber}"
                ));
            }

            if (!string.Equals(round.SelectedAnswer, "timeout", StringComparison.OrdinalIgnoreCase))
            {
                report.Findings.Add(new ResultDiagnosticFinding(
                    ResultDiagnosticSeverity.Warning,
                    "Timeout answer mismatch",
                    "Round is marked as timedOut=true, but selectedAnswer is not 'timeout'.",
                    $"Round {round.RoundNumber}"
                ));
            }

            if (TryParseBrainInBool(round.Successfully, out var successfully) && successfully)
            {
                report.Findings.Add(new ResultDiagnosticFinding(
                    ResultDiagnosticSeverity.Warning,
                    "Timeout marked successful",
                    "Round is marked as timedOut=true, but successfully is true.",
                    $"Round {round.RoundNumber}"
                ));
            }
        }

        /// <summary>
        /// Validates finalClickId and selectedAnswerActionId consistency.
        /// </summary>
        /// <param name="round">Round summary.</param>
        /// <param name="report">Report to append findings to.</param>
        private static void ValidateClickConsistency(ResultRoundDiagnostics round, ResultDiagnosticsReport report)
        {
            if (string.IsNullOrWhiteSpace(round.SelectedAnswer) ||
                string.Equals(round.SelectedAnswer, "timeout", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (IsNullLike(round.FinalClickId))
            {
                report.Findings.Add(new ResultDiagnosticFinding(
                    ResultDiagnosticSeverity.Warning,
                    "Answer without final click",
                    "Round contains selectedAnswer, but finalClickId is null.",
                    $"Round {round.RoundNumber}"
                ));
            }

            if (IsNullLike(round.SelectedAnswerActionId))
            {
                report.Findings.Add(new ResultDiagnosticFinding(
                    ResultDiagnosticSeverity.Warning,
                    "Answer without action ID",
                    "Round contains selectedAnswer, but selectedAnswerActionId is null or missing.",
                    $"Round {round.RoundNumber}"
                ));

                return;
            }

            if (IsNullLike(round.FinalClickId))
                return;

            if (round.SelectedAnswerActionId == round.FinalClickId)
                return;

            report.Findings.Add(new ResultDiagnosticFinding(
                ResultDiagnosticSeverity.Warning,
                "Click ID mismatch",
                $"selectedAnswerActionId '{round.SelectedAnswerActionId}' does not match finalClickId '{round.FinalClickId}'.",
                $"Round {round.RoundNumber}"
            ));
        }

        /// <summary>
        /// Validates custom isCorrect against BrainIn successfully field.
        /// </summary>
        /// <param name="round">Round summary.</param>
        /// <param name="report">Report to append findings to.</param>
        private static void ValidateSuccessConsistency(ResultRoundDiagnostics round, ResultDiagnosticsReport report)
        {
            if (!round.CustomData.ContainsKey("isCorrect"))
                return;

            if (!TryParseBrainInBool(round.CustomData["isCorrect"], out var isCorrect))
            {
                report.Findings.Add(new ResultDiagnosticFinding(
                    ResultDiagnosticSeverity.Warning,
                    "Invalid isCorrect value",
                    $"Value '{round.CustomData["isCorrect"]}' is not a valid boolean.",
                    $"Round {round.RoundNumber}"
                ));

                return;
            }

            if (!TryParseBrainInBool(round.Successfully, out var successfully))
                return;

            if (isCorrect == successfully)
                return;

            report.Findings.Add(new ResultDiagnosticFinding(
                ResultDiagnosticSeverity.Warning,
                "Success mismatch",
                $"customData isCorrect={isCorrect.ToString().ToLowerInvariant()} does not match successfully={round.Successfully}.",
                $"Round {round.RoundNumber}"
            ));
        }

        /// <summary>
        /// Requires a top-level field.
        /// </summary>
        /// <param name="root">Parsed JSON root.</param>
        /// <param name="report">Report to append findings to.</param>
        /// <param name="fieldName">Required field name.</param>
        private static void RequireTopLevelField(JObject root, ResultDiagnosticsReport report, string fieldName)
        {
            if (root[fieldName] != null)
                return;

            report.Findings.Add(new ResultDiagnosticFinding(
                ResultDiagnosticSeverity.Warning,
                "Missing top-level field",
                $"The result JSON does not contain top-level field '{fieldName}'."
            ));
        }

        /// <summary>
        /// Requires a round field.
        /// </summary>
        /// <param name="round">Round JSON object.</param>
        /// <param name="report">Report to append findings to.</param>
        /// <param name="roundNumber">One-based round number.</param>
        /// <param name="fieldName">Required field name.</param>
        private static void RequireRoundField(
            JObject round,
            ResultDiagnosticsReport report,
            int roundNumber,
            string fieldName)
        {
            if (round[fieldName] != null)
                return;

            report.Findings.Add(new ResultDiagnosticFinding(
                ResultDiagnosticSeverity.Warning,
                "Missing round field",
                $"Round does not contain field '{fieldName}'.",
                $"Round {roundNumber}"
            ));
        }

        /// <summary>
        /// Tries to parse a diagnostic number using invariant culture.
        /// A comma decimal separator is accepted for compatibility, but reported separately as a warning.
        /// </summary>
        /// <param name="value">Raw numeric value.</param>
        /// <param name="parsedValue">Parsed number.</param>
        /// <returns>True if the value was parsed; otherwise false.</returns>
        private static bool TryParseDiagnosticNumber(string value, out double parsedValue)
        {
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsedValue))
                return true;

            var normalized = value.Replace(',', '.');

            return double.TryParse(
                normalized,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out parsedValue
            );
        }

        /// <summary>
        /// Validates a numeric round value.
        /// </summary>
        /// <param name="value">Raw numeric value.</param>
        /// <param name="fieldName">Field name.</param>
        /// <param name="report">Report to append findings to.</param>
        /// <param name="roundNumber">One-based round number.</param>
        /// <param name="warnAboutComma">Whether to warn about comma decimal separator.</param>
        private static void ValidateNumber(
            string value,
            string fieldName,
            ResultDiagnosticsReport report,
            int roundNumber,
            bool warnAboutComma)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            if (warnAboutComma && value.Contains(","))
            {
                report.Findings.Add(new ResultDiagnosticFinding(
                    ResultDiagnosticSeverity.Warning,
                    "Culture-specific decimal separator",
                    $"Value '{fieldName}={value}' uses comma as decimal separator. Prefer invariant format with dot for JSON diagnostics.",
                    $"Round {roundNumber}"
                ));
            }

            if (!TryParseDiagnosticNumber(value, out var parsedValue))
            {
                report.Findings.Add(new ResultDiagnosticFinding(
                    ResultDiagnosticSeverity.Warning,
                    "Invalid numeric value",
                    $"Value '{fieldName}={value}' is not a valid invariant numeric value.",
                    $"Round {roundNumber}"
                ));

                return;
            }

            if (parsedValue >= 0d)
                return;

            report.Findings.Add(new ResultDiagnosticFinding(
                ResultDiagnosticSeverity.Error,
                "Negative numeric value",
                $"Value '{fieldName}={value}' is negative, but round time values must be non-negative.",
                $"Round {roundNumber}"
            ));
        }

        /// <summary>
        /// Tries to extract JSON object from pure JSON or Unity log text.
        /// </summary>
        /// <param name="input">Raw input.</param>
        /// <param name="json">Extracted JSON.</param>
        /// <param name="error">Error message.</param>
        /// <returns>True if JSON was extracted; otherwise false.</returns>
        private static bool TryExtractJson(string input, out string json, out string error)
        {
            json = null;
            error = null;

            var trimmed = input.Trim();

            if (trimmed.StartsWith("{", StringComparison.Ordinal) &&
                trimmed.EndsWith("}", StringComparison.Ordinal))
            {
                json = trimmed;
                return true;
            }

            var contentIndex = input.IndexOf("Content:", StringComparison.OrdinalIgnoreCase);
            var startIndex = contentIndex >= 0
                ? input.IndexOf('{', contentIndex)
                : input.IndexOf('{');

            if (startIndex < 0)
            {
                error = "No JSON object was found. Paste either pure JSON or a log containing 'Content: { ... }'.";
                return false;
            }

            var depth = 0;
            var inString = false;
            var escaped = false;

            for (var index = startIndex; index < input.Length; index++)
            {
                var character = input[index];

                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                        continue;
                    }

                    if (character == '\\')
                    {
                        escaped = true;
                        continue;
                    }

                    if (character == '"')
                        inString = false;

                    continue;
                }

                if (character == '"')
                {
                    inString = true;
                    continue;
                }

                if (character == '{')
                {
                    depth++;
                    continue;
                }

                if (character != '}')
                    continue;

                depth--;

                if (depth != 0)
                    continue;

                json = input.Substring(startIndex, index - startIndex + 1);
                return true;
            }

            error = "JSON object start was found, but its closing brace could not be located.";
            return false;
        }

        /// <summary>
        /// Gets a string property from a token.
        /// </summary>
        /// <param name="token">JSON token.</param>
        /// <param name="propertyName">Property name.</param>
        /// <returns>Property value as string.</returns>
        private static string GetString(JToken token, string propertyName)
        {
            var value = token?[propertyName];

            return value == null || value.Type == JTokenType.Null
                ? null
                : value.ToString();
        }

        /// <summary>
        /// Gets array count from a JSON property.
        /// </summary>
        /// <param name="token">JSON token.</param>
        /// <param name="propertyName">Array property name.</param>
        /// <returns>Array count or zero.</returns>
        private static int GetArrayCount(JToken token, string propertyName)
        {
            return token?[propertyName] is JArray array
                ? array.Count
                : 0;
        }

        /// <summary>
        /// Gets a parsed custom data value.
        /// </summary>
        /// <param name="round">Round summary.</param>
        /// <param name="key">Custom data key.</param>
        /// <returns>Custom data value or null.</returns>
        private static string GetCustom(ResultRoundDiagnostics round, string key)
        {
            return round.CustomData.ContainsKey(key)
                ? round.CustomData[key]
                : null;
        }

        /// <summary>
        /// Determines whether click ID is empty or null-like.
        /// </summary>
        /// <param name="clickId">Click ID to inspect.</param>
        /// <returns>True if the click ID is null-like; otherwise false.</returns>
        private static bool IsNullLike(string clickId)
        {
            return string.IsNullOrWhiteSpace(clickId) ||
                   string.Equals(clickId, "null", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Parses BrainIn boolean values such as 1, 0, true and false.
        /// </summary>
        /// <param name="value">Raw value.</param>
        /// <param name="result">Parsed boolean.</param>
        /// <returns>True if the value was parsed; otherwise false.</returns>
        private static bool TryParseBrainInBool(string value, out bool result)
        {
            result = false;

            if (string.IsNullOrWhiteSpace(value))
                return false;

            if (string.Equals(value, "1", StringComparison.Ordinal) ||
                string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
            {
                result = true;
                return true;
            }

            if (string.Equals(value, "0", StringComparison.Ordinal) ||
                string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
            {
                result = false;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Validates a top-level numeric value.
        /// </summary>
        /// <param name="value">Raw numeric value.</param>
        /// <param name="fieldName">Field name.</param>
        /// <param name="report">Report to append findings to.</param>
        /// <param name="mustBeNonNegative">Whether the value must be non-negative.</param>
        private static void ValidateTopLevelNumber(
            string value,
            string fieldName,
            ResultDiagnosticsReport report,
            bool mustBeNonNegative)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            if (value.Contains(","))
            {
                report.Findings.Add(new ResultDiagnosticFinding(
                    ResultDiagnosticSeverity.Warning,
                    "Culture-specific decimal separator",
                    $"Value '{fieldName}={value}' uses comma as decimal separator. Prefer invariant format with dot for JSON diagnostics."
                ));
            }

            if (!TryParseDiagnosticNumber(value, out var parsedValue))
            {
                report.Findings.Add(new ResultDiagnosticFinding(
                    ResultDiagnosticSeverity.Warning,
                    "Invalid numeric value",
                    $"Value '{fieldName}={value}' is not a valid invariant numeric value."
                ));

                return;
            }

            if (!mustBeNonNegative || parsedValue >= 0d)
                return;

            report.Findings.Add(new ResultDiagnosticFinding(
                ResultDiagnosticSeverity.Error,
                "Negative numeric value",
                $"Value '{fieldName}={value}' is negative, but BrainIn result time values must be non-negative."
            ));
        }

        /// <summary>
        /// Normalizes expected customData keys.
        /// </summary>
        /// <param name="expectedCustomDataKeys">Raw expected customData keys.</param>
        /// <returns>Distinct non-empty keys.</returns>
        private static IEnumerable<string> NormalizeExpectedCustomDataKeys(IEnumerable<string> expectedCustomDataKeys)
        {
            if (expectedCustomDataKeys == null)
                yield break;

            var seenKeys = new HashSet<string>();

            foreach (var key in expectedCustomDataKeys)
            {
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                var normalizedKey = key.Trim();

                if (seenKeys.Add(normalizedKey))
                    yield return normalizedKey;
            }
        }

        /// <summary>
        /// Validates that expected customData keys are present in the round.
        /// </summary>
        /// <param name="round">Round summary.</param>
        /// <param name="report">Report to append findings to.</param>
        private static void ValidateExpectedCustomDataKeys(ResultRoundDiagnostics round, ResultDiagnosticsReport report)
        {
            if (report.ExpectedCustomDataKeys.Count == 0)
                return;

            foreach (var expectedKey in report.ExpectedCustomDataKeys)
            {
                if (round.CustomData.ContainsKey(expectedKey))
                    continue;

                report.Findings.Add(new ResultDiagnosticFinding(
                    ResultDiagnosticSeverity.Error,
                    "Missing expected customData key",
                    $"Expected customData key '{expectedKey}' was not found in the round result.",
                    $"Round {round.RoundNumber}"
                ));
            }
        }
    }
}