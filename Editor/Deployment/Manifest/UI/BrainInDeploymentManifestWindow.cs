using System;
using BrainIn.DevTools.Editor.Deployment.Build;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BrainIn.DevTools.Editor.Deployment.Manifest.UI
{
    /// <summary>
    /// Editor window for generating BrainIn deployment manifests and running build deployment pipeline.
    /// </summary>
    public sealed class BrainInDeploymentManifestWindow : EditorWindow
    {
        private static readonly string[] ProgramCategories =
        {
            "Memory",
            "Concentration",
            "Speech functions",
            "Logical thinking",
            "Spatial orientation",
            "Light motor skills"
        };

        private readonly BrainInDeploymentManifestGenerator _generator = new BrainInDeploymentManifestGenerator();
        private readonly BrainInBuildDeploymentRunner _buildDeploymentRunner = new BrainInBuildDeploymentRunner();

        private BrainInBuildPreset _buildPreset = BrainInBuildPreset.ReleaseSize;
        private string _programName;
        private string _programDescriptionCs = "";
        private string _programDescriptionEn = "";
        private string _programDescriptionDe = "";
        private int _selectedProgramCategoryIndex = 1;
        private string _programVersion = "1.0.0";
        private string _programAuthor = "";
        private string _programNotes = "";
        private string _programId = "";

        private string _buildOutputPath = "Builds/WebGL";
        private string _deploymentPackagePath = "Builds/Deployment";

        private bool _stopOnValidationErrors = true;
        private bool _cleanBuildOutputBeforeBuild = true;
        private bool _cleanDeploymentPackageBeforeCopy = true;

        private BrainInDeploymentManifest _lastManifest;
        private BrainInBuildDeploymentResult _lastBuildDeploymentResult;
        private Vector2 _scroll;

        /// <summary>
        /// Opens the BrainIn deployment manifest window.
        /// </summary>
        [MenuItem("BrainIn/DevTools/Deployment Manifest")]
        public static void Open()
        {
            var window = GetWindow<BrainInDeploymentManifestWindow>("Deployment Manifest");
            window.minSize = new Vector2(800f, 780f);
            window.Show();
        }

        /// <summary>
        /// Initializes default UI values.
        /// </summary>
        private void OnEnable()
        {
            if (string.IsNullOrWhiteSpace(_programName))
                _programName = PlayerSettings.productName;
        }

        /// <summary>
        /// Draws the editor window GUI.
        /// </summary>
        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawHeader();
            DrawProgramSection();
            DrawBuildSection();
            DrawDeploymentSection();
            DrawWebRequirementsSection();
            DrawActions();
            DrawPreview();
            DrawBuildDeploymentResult();

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// Draws window header.
        /// </summary>
        private static void DrawHeader()
        {
            EditorGUILayout.LabelField("BrainIn Deployment Manifest", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Generates a deployment manifest, runs validation, creates a WebGL build and prepares the final BrainIn deployment package.",
                MessageType.Info
            );
        }

        /// <summary>
        /// Draws program metadata section.
        /// </summary>
        private void DrawProgramSection()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Program Metadata", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _programName = EditorGUILayout.TextField("Name", _programName);
            _programVersion = EditorGUILayout.TextField("Version", _programVersion);

            _selectedProgramCategoryIndex = EditorGUILayout.Popup(
                "Category",
                _selectedProgramCategoryIndex,
                ProgramCategories
            );

            _programAuthor = EditorGUILayout.TextField("Author", _programAuthor);

            EditorGUILayout.LabelField("Description CS");
            _programDescriptionCs = EditorGUILayout.TextArea(_programDescriptionCs, GUILayout.MinHeight(45f));

            EditorGUILayout.LabelField("Description EN");
            _programDescriptionEn = EditorGUILayout.TextArea(_programDescriptionEn, GUILayout.MinHeight(45f));

            EditorGUILayout.LabelField("Description DE");
            _programDescriptionDe = EditorGUILayout.TextArea(_programDescriptionDe, GUILayout.MinHeight(45f));

            EditorGUILayout.LabelField("Notes");
            _programNotes = EditorGUILayout.TextArea(_programNotes, GUILayout.MinHeight(45f));

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Draws build metadata section.
        /// </summary>
        private void DrawBuildSection()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Build Settings", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _buildPreset = (BrainInBuildPreset)EditorGUILayout.EnumPopup("Build preset", _buildPreset);
            _stopOnValidationErrors = EditorGUILayout.Toggle("Stop on validation errors", _stopOnValidationErrors);
            _cleanBuildOutputBeforeBuild = EditorGUILayout.Toggle("Clean build output", _cleanBuildOutputBeforeBuild);

            EditorGUILayout.BeginHorizontal();
            _buildOutputPath = EditorGUILayout.TextField("Build output path", _buildOutputPath);

            if (GUILayout.Button("Select", GUILayout.Width(80f)))
            {
                var selectedPath = EditorUtility.OpenFolderPanel(
                    "Select Build Output Folder",
                    string.IsNullOrWhiteSpace(_buildOutputPath) ? Application.dataPath : _buildOutputPath,
                    ""
                );

                if (!string.IsNullOrWhiteSpace(selectedPath))
                    _buildOutputPath = selectedPath;
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox(
                "FastBuild uses disabled compression. ReleaseSize uses Brotli compression with decompression fallback.",
                MessageType.None
            );

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Draws deployment metadata section.
        /// </summary>
        private void DrawDeploymentSection()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Deployment Metadata", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _programId = EditorGUILayout.TextField("BrainIn Program ID", _programId);
            EditorGUILayout.HelpBox(
                "Program ID is optional. When empty, the manifest keeps target path as Files/<ProgramId>/.",
                MessageType.None
            );

            _cleanDeploymentPackageBeforeCopy = EditorGUILayout.Toggle(
                "Clean deployment package",
                _cleanDeploymentPackageBeforeCopy
            );

            EditorGUILayout.BeginHorizontal();
            _deploymentPackagePath = EditorGUILayout.TextField("Deployment package path", _deploymentPackagePath);

            if (GUILayout.Button("Select", GUILayout.Width(80f)))
            {
                var selectedPath = EditorUtility.OpenFolderPanel(
                    "Select Deployment Package Folder",
                    string.IsNullOrWhiteSpace(_deploymentPackagePath) ? Application.dataPath : _deploymentPackagePath,
                    ""
                );

                if (!string.IsNullOrWhiteSpace(selectedPath))
                    _deploymentPackagePath = selectedPath;
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Draws Web requirements status.
        /// </summary>
        private static void DrawWebRequirementsSection()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Web Requirements", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (BrainInBuildDeploymentRunner.IsLocalWebRequirementsFolderDetected())
            {
                EditorGUILayout.HelpBox(
                    "Web folder detected - copying content to build.",
                    MessageType.Info
                );

                DrawRow(
                    "Detected Web folder",
                    BrainInBuildDeploymentRunner.GetLocalWebRequirementsFolderPath()
                );
            }
            else if (BrainInBuildDeploymentRunner.IsWebRequirementsRepositoryConfigured())
            {
                EditorGUILayout.HelpBox(
                    "Web requirements files are not detected, cloning them from git repo.",
                    MessageType.Warning
                );
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Web requirements files are not detected and Git repository URL is not configured in BrainInBuildDeploymentRunner.",
                    MessageType.Error
                );
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Draws action buttons.
        /// </summary>
        private void DrawActions()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Generate Preview", GUILayout.Height(30f)))
                _lastManifest = GenerateManifest();

            if (GUILayout.Button("Export JSON Manifest", GUILayout.Height(30f)))
                ExportManifest();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4f);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Run Build & Deployment", GUILayout.Height(34f)))
                RunBuildDeployment();

            if (GUILayout.Button("Clear Results", GUILayout.Width(120f), GUILayout.Height(34f)))
            {
                _lastManifest = null;
                _lastBuildDeploymentResult = null;
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Draws manifest preview summary.
        /// </summary>
        private void DrawPreview()
        {
            if (_lastManifest == null)
                return;

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Manifest Preview", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            DrawRow("Project", _lastManifest.Project.UnityProjectName);
            DrawRow("Product", _lastManifest.Project.ProductName);
            DrawRow("Build target", _lastManifest.Project.BuildTarget);
            DrawRow("Program category", _lastManifest.Program.Category);
            DrawRow("BrainIn template detected", _lastManifest.BrainInTemplate.Detected ? "Yes" : "No");
            DrawRow("Input parameters", _lastManifest.InputContract.Parameters.Count.ToString());
            DrawRow("Output customData keys", _lastManifest.OutputContract.RoundCustomData.Count.ToString());
            DrawRow("Localization keys", _lastManifest.Localization.Keys.Count.ToString());
            DrawRow("Warnings", _lastManifest.GenerationWarnings.Count.ToString());

            if (_lastManifest.GenerationWarnings.Count > 0)
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("Generation warnings", EditorStyles.boldLabel);

                foreach (var warning in _lastManifest.GenerationWarnings)
                    EditorGUILayout.HelpBox(warning, MessageType.Warning);
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Draws the last build deployment result.
        /// </summary>
        private void DrawBuildDeploymentResult()
        {
            if (_lastBuildDeploymentResult == null)
                return;

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Build & Deployment Result", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.HelpBox(
                _lastBuildDeploymentResult.Message ?? "No message.",
                _lastBuildDeploymentResult.Succeeded ? MessageType.Info : MessageType.Error
            );

            DrawRow("Succeeded", _lastBuildDeploymentResult.Succeeded ? "Yes" : "No");
            DrawRow("Build result", _lastBuildDeploymentResult.BuildResult ?? "-");
            DrawRow("Validation errors", _lastBuildDeploymentResult.ValidationErrors.ToString());
            DrawRow("Validation warnings", _lastBuildDeploymentResult.ValidationWarnings.ToString());
            DrawRow("Validation infos", _lastBuildDeploymentResult.ValidationInfos.ToString());
            DrawRow("Build output", _lastBuildDeploymentResult.BuildOutputPath);
            DrawRow("Deployment package", _lastBuildDeploymentResult.DeploymentPackagePath);
            DrawRow("Validation report", _lastBuildDeploymentResult.ValidationReportPath);
            DrawRow("Deployment manifest", _lastBuildDeploymentResult.DeploymentManifestPath);
            DrawRow("Web requirements mode", _lastBuildDeploymentResult.WebRequirementsMode);
            DrawRow("Web requirements source", _lastBuildDeploymentResult.WebRequirementsResolvedSourcePath);
            DrawRow("Web files copied", _lastBuildDeploymentResult.WebFilesCopied.ToString());
            DrawRow("StreamingAssets files copied", _lastBuildDeploymentResult.StreamingAssetsFilesCopied.ToString());
            DrawRow("Renamed Web script", _lastBuildDeploymentResult.RenamedWebScriptPath);

            if (_lastBuildDeploymentResult.DeploymentWarnings.Count > 0)
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("Deployment warnings", EditorStyles.boldLabel);

                foreach (var warning in _lastBuildDeploymentResult.DeploymentWarnings)
                    EditorGUILayout.HelpBox(warning, MessageType.Warning);
            }

            DrawRevealButtons();

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Draws reveal buttons for generated folders and files.
        /// </summary>
        private void DrawRevealButtons()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.BeginHorizontal();

            GUI.enabled = !string.IsNullOrWhiteSpace(_lastBuildDeploymentResult.BuildOutputPath);
            if (GUILayout.Button("Reveal Build Output"))
                RevealPath(_lastBuildDeploymentResult.BuildOutputPath);

            GUI.enabled = !string.IsNullOrWhiteSpace(_lastBuildDeploymentResult.DeploymentPackagePath);
            if (GUILayout.Button("Reveal Deployment Package"))
                RevealPath(_lastBuildDeploymentResult.DeploymentPackagePath);

            GUI.enabled = !string.IsNullOrWhiteSpace(_lastBuildDeploymentResult.DeploymentManifestPath);
            if (GUILayout.Button("Reveal Manifest"))
                RevealPath(_lastBuildDeploymentResult.DeploymentManifestPath);

            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// Draws one preview row.
        /// </summary>
        /// <param name="label">Row label.</param>
        /// <param name="value">Row value.</param>
        private static void DrawRow(string label, string value)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(200f));
            EditorGUILayout.SelectableLabel(value ?? "-", GUILayout.Height(EditorGUIUtility.singleLineHeight));
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// Generates a manifest from current UI values.
        /// </summary>
        /// <returns>Generated manifest.</returns>
        private BrainInDeploymentManifest GenerateManifest()
        {
            return _generator.Generate(CreateManifestOptions());
        }

        /// <summary>
        /// Exports a generated manifest to a JSON file.
        /// </summary>
        private void ExportManifest()
        {
            var manifest = GenerateManifest();
            _lastManifest = manifest;

            var filePath = EditorUtility.SaveFilePanel(
                "Export BrainIn Deployment Manifest",
                "Assets",
                BrainInDeploymentManifestExporter.CreateDefaultFileName(),
                "json"
            );

            if (string.IsNullOrWhiteSpace(filePath))
                return;

            try
            {
                BrainInDeploymentManifestExporter.Export(manifest, filePath);

                if (filePath.StartsWith(Application.dataPath, StringComparison.Ordinal))
                    AssetDatabase.Refresh();

                EditorUtility.DisplayDialog(
                    "Deployment Manifest Export",
                    "BrainIn deployment manifest was exported successfully.",
                    "OK"
                );
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog(
                    "Deployment Manifest Export Failed",
                    exception.Message,
                    "OK"
                );
            }
        }

        /// <summary>
        /// Runs build deployment pipeline after the current GUI event finishes.
        /// </summary>
        private void RunBuildDeployment()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var options = CreateBuildDeploymentOptions();

            EditorApplication.delayCall += () => RunBuildDeploymentDelayed(options);
        }

        /// <summary>
        /// Runs build deployment pipeline outside the current OnGUI layout event.
        /// </summary>
        /// <param name="options">Build deployment options.</param>
        private void RunBuildDeploymentDelayed(BrainInBuildDeploymentOptions options)
        {
            try
            {
                _lastBuildDeploymentResult = _buildDeploymentRunner.Run(options);
                _lastManifest = _lastBuildDeploymentResult.DeploymentManifest ?? _lastManifest;

                Repaint();

                EditorUtility.DisplayDialog(
                    "BrainIn Build & Deployment",
                    _lastBuildDeploymentResult.Message ?? "Build and deployment finished.",
                    "OK"
                );
            }
            catch (Exception exception)
            {
                _lastBuildDeploymentResult = new BrainInBuildDeploymentResult
                {
                    Succeeded = false,
                    Message = exception.Message,
                    BuildResult = "Exception"
                };

                Repaint();

                EditorUtility.DisplayDialog(
                    "BrainIn Build & Deployment Failed",
                    exception.Message,
                    "OK"
                );
            }
        }

        /// <summary>
        /// Creates manifest generation options from current UI values.
        /// </summary>
        /// <returns>Generation options.</returns>
        private BrainInDeploymentManifestGenerationOptions CreateManifestOptions()
        {
            return new BrainInDeploymentManifestGenerationOptions
            {
                BuildPreset = _buildPreset,
                BuildOutputPath = _buildOutputPath,
                DeploymentPackagePath = _deploymentPackagePath,
                ProgramName = _programName,
                ProgramDescriptionCs = _programDescriptionCs,
                ProgramDescriptionEn = _programDescriptionEn,
                ProgramDescriptionDe = _programDescriptionDe,
                ProgramCategory = ProgramCategories[_selectedProgramCategoryIndex],
                ProgramVersion = _programVersion,
                ProgramAuthor = _programAuthor,
                ProgramNotes = _programNotes,
                ProgramId = TryParseProgramId()
            };
        }

        /// <summary>
        /// Creates build deployment options from current UI values.
        /// </summary>
        /// <returns>Build deployment options.</returns>
        private BrainInBuildDeploymentOptions CreateBuildDeploymentOptions()
        {
            return new BrainInBuildDeploymentOptions
            {
                BuildPreset = _buildPreset,
                BuildOutputPath = _buildOutputPath,
                DeploymentPackagePath = _deploymentPackagePath,
                StopOnValidationErrors = _stopOnValidationErrors,
                CleanBuildOutputBeforeBuild = _cleanBuildOutputBeforeBuild,
                CleanDeploymentPackageBeforeCopy = _cleanDeploymentPackageBeforeCopy,
                ManifestOptions = CreateManifestOptions()
            };
        }

        /// <summary>
        /// Tries to parse BrainIn Program ID from UI.
        /// </summary>
        /// <returns>Parsed Program ID or null.</returns>
        private int? TryParseProgramId()
        {
            if (string.IsNullOrWhiteSpace(_programId))
                return null;

            return int.TryParse(_programId, out var parsedProgramId)
                ? parsedProgramId
                : null;
        }

        /// <summary>
        /// Reveals a generated file or folder in the operating system file browser.
        /// </summary>
        /// <param name="path">Path to reveal.</param>
        private static void RevealPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            EditorUtility.RevealInFinder(path);
        }
    }
}