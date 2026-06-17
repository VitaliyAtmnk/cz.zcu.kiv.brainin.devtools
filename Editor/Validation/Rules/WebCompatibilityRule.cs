using System.Collections.Generic;
using System.IO;
using System.Linq;
using BrainIn.DevTools.Editor.Validation.Core;
using UnityEditor;

namespace BrainIn.DevTools.Editor.Validation.Rules
{
    /// <summary>
    /// Scans C# scripts for code patterns that may be problematic in Unity WebGL / WebAssembly builds.
    /// </summary>
    public sealed class WebCompatibilityRule : IValidationRule
    {
        /// <summary>
        /// Gets the display name of the validation rule.
        /// </summary>
        public string Name => "WebGL / WASM compatibility scanner";

        private readonly RiskPattern[] _patterns =
        {
            new RiskPattern("System.Threading", ValidationSeverity.Error,
                "System.Threading is risky or unsupported in WebGL builds. Prefer Unity coroutines or async workflows without blocking waits."),

            new RiskPattern("new Thread", ValidationSeverity.Error,
                "Manual thread creation is not suitable for Unity WebGL builds."),

            new RiskPattern(".Wait()", ValidationSeverity.Warning,
                "Blocking wait detected. Blocking waits can freeze WebGL applications."),

            new RiskPattern(".Result", ValidationSeverity.Warning,
                "Synchronous access to async Result detected. This can block execution in WebGL."),

            new RiskPattern("System.Net", ValidationSeverity.Warning,
                "System.Net usage detected. Networking in WebGL should usually go through UnityWebRequest or browser-compatible APIs."),

            new RiskPattern("Socket", ValidationSeverity.Warning,
                "Socket usage detected. Raw sockets are not suitable for browser-based WebGL builds."),

            new RiskPattern("TcpClient", ValidationSeverity.Warning,
                "TcpClient usage detected. Raw TCP networking is not suitable for browser-based WebGL builds."),

            new RiskPattern("File.", ValidationSeverity.Warning,
                "Direct file system access detected. WebGL runs in a browser sandbox; use supported storage APIs instead."),

            new RiskPattern("Directory.", ValidationSeverity.Warning,
                "Direct directory access detected. WebGL runs in a browser sandbox; use supported storage APIs instead."),

            new RiskPattern("Application.dataPath", ValidationSeverity.Warning,
                "Application.dataPath usage detected. Paths behave differently in WebGL builds."),

            new RiskPattern("Microphone", ValidationSeverity.Warning,
                "Microphone usage detected. WebGL audio input support is limited and browser-dependent."),

            new RiskPattern("Reflection.Emit", ValidationSeverity.Error,
                "Dynamic code generation is not supported in AOT/WebGL environments."),

            new RiskPattern("Activator.CreateInstance", ValidationSeverity.Warning,
                "Reflection-based instance creation detected. This may be problematic with IL2CPP stripping/AOT builds.")
        };

        /// <summary>
        /// Runs the WebGL / WebAssembly compatibility scan over C# scripts under the Assets folder.
        /// </summary>
        /// <param name="context">Validation context containing project paths and shared validation data.</param>
        /// <returns>Validation results describing detected WebGL compatibility risks.</returns>
        public IEnumerable<ValidationResult> Validate(ValidationContext context)
        {
            var results = new List<ValidationResult>();
            var scriptGuids = AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets" });

            foreach (var guid in scriptGuids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);

                if (string.IsNullOrWhiteSpace(assetPath) || !assetPath.EndsWith(".cs"))
                {
                    continue;
                }

                var fullPath = Path.GetFullPath(assetPath);

                if (!File.Exists(fullPath))
                {
                    continue;
                }

                string[] lines;

                try
                {
                    lines = File.ReadAllLines(fullPath);
                }
                catch
                {
                    results.Add(new ValidationResult(
                        ValidationSeverity.Warning,
                        Name,
                        $"Could not read script file: {assetPath}",
                        assetPath
                    ));

                    continue;
                }

                // Scan the script line by line to report the exact location (line number)
                // of each detected WebGL compatibility risk.
                for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    var line = lines[lineIndex];

                    results.AddRange(from pattern in _patterns
                        where line.Contains(pattern.Text)
                        select new ValidationResult(pattern.Severity, Name,
                            $"Line {lineIndex + 1}: {pattern.Message} Pattern: `{pattern.Text}`", assetPath));
                }
            }

            return results;
        }

        /// <summary>
        /// Represents one risky source-code pattern and the message reported when it is found.
        /// </summary>
        private readonly struct RiskPattern
        {
            /// <summary>
            /// Gets the source-code text pattern to search for.
            /// </summary>
            public string Text { get; }

            /// <summary>
            /// Gets the severity that should be reported when the pattern is found.
            /// </summary>
            public ValidationSeverity Severity { get; }

            /// <summary>
            /// Gets the validation message associated with the pattern.
            /// </summary>
            public string Message { get; }

            /// <summary>
            /// Initializes a new instance of the <see cref="RiskPattern"/> struct.
            /// </summary>
            /// <param name="text">Source-code text pattern to search for.</param>
            /// <param name="severity">Severity to report when the pattern is found.</param>
            /// <param name="message">Validation message associated with the pattern.</param>
            public RiskPattern(string text, ValidationSeverity severity, string message)
            {
                Text = text;
                Severity = severity;
                Message = message;
            }
        }
    }
}