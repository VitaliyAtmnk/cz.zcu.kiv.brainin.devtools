using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using BrainIn.DevTools.Editor.Deployment.Manifest;
using BrainIn.DevTools.Editor.Reports;
using BrainIn.DevTools.Editor.Validation.Core;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BrainIn.DevTools.Editor.Deployment.Build
{
    /// <summary>
    /// Options used by the BrainIn build and deployment runner.
    /// </summary>
    public sealed class BrainInBuildDeploymentOptions
    {
        public BrainInBuildPreset BuildPreset { get; set; } = BrainInBuildPreset.ReleaseSize;
        public string BuildOutputPath { get; set; } = "Builds/WebGL";
        public string DeploymentPackagePath { get; set; } = "Builds/Deployment";
        public bool StopOnValidationErrors { get; set; } = true;
        public bool CleanBuildOutputBeforeBuild { get; set; } = true;
        public bool CleanDeploymentPackageBeforeCopy { get; set; } = true;

        public BrainInDeploymentManifestGenerationOptions ManifestOptions { get; set; } =
            new BrainInDeploymentManifestGenerationOptions();
    }

    /// <summary>
    /// Result of the BrainIn build and deployment process.
    /// </summary>
    public sealed class BrainInBuildDeploymentResult
    {
        public bool Succeeded { get; set; }
        public string Message { get; set; }
        public int ValidationErrors { get; set; }
        public int ValidationWarnings { get; set; }
        public int ValidationInfos { get; set; }
        public string BuildResult { get; set; }
        public string BuildOutputPath { get; set; }
        public string DeploymentPackagePath { get; set; }
        public string ValidationReportPath { get; set; }
        public string DeploymentManifestPath { get; set; }

        public string WebRequirementsMode { get; set; }
        public string WebRequirementsResolvedSourcePath { get; set; }
        public int WebFilesCopied { get; set; }
        public int StreamingAssetsFilesCopied { get; set; }
        public string RenamedWebScriptPath { get; set; }
        public List<string> DeploymentWarnings { get; } = new List<string>();

        public BrainInDeploymentManifest DeploymentManifest { get; set; }
    }

    /// <summary>
    /// Runs validation, WebGL build, deployment folder preparation and deployment manifest generation.
    /// </summary>
    public sealed class BrainInBuildDeploymentRunner
    {
        private const string WebGlModuleDocumentationUrl = "https://learn.unity.com/tutorial/install-the-webgl-module";

        private const string WebRequirementsRepositoryUrl =
            "https://gitlab.kiv.zcu.cz/neurorehabilitation/unity-template-web-requirements.git";

        // Leave empty to use the default branch.
        private const string WebRequirementsRepositoryBranch = "";

        private static readonly string[] WebTemplatePlaceholderFileNames =
        {
            "TODO_SET_GAME_NAME.js",
            "TODO_SET_NAME.js"
        };

        private static readonly string[] IgnoredRequirementsDirectories =
        {
            ".git",
            ".idea",
            "node_modules"
        };

        private static readonly string[] IgnoredRequirementsFileNames =
        {
            ".DS_Store",
            "Thumbs.db",
            "desktop.ini"
        };

        private readonly BrainInDeploymentManifestGenerator _manifestGenerator =
            new BrainInDeploymentManifestGenerator();

        /// <summary>
        /// Gets whether a local Web requirements folder exists.
        /// </summary>
        /// <returns>True if local Web folder exists.</returns>
        public static bool IsLocalWebRequirementsFolderDetected()
        {
            return TryFindLocalWebRequirementsSourceRootPath(out _);
        }

        /// <summary>
        /// Gets whether the Git repository URL is configured.
        /// </summary>
        /// <returns>True if repository URL is configured.</returns>
        public static bool IsWebRequirementsRepositoryConfigured()
        {
            return !string.IsNullOrWhiteSpace(WebRequirementsRepositoryUrl);
        }

        /// <summary>
        /// Gets the detected local Web requirements folder path.
        /// </summary>
        /// <returns>Absolute local Web folder path or expected default path.</returns>
        public static string GetLocalWebRequirementsFolderPath()
        {
            if (!TryFindLocalWebRequirementsSourceRootPath(out var sourceRootPath))
                return Path.Combine(GetProjectRootPath(), "Web");

            return ResolveWebFolderPath(sourceRootPath) ?? Path.Combine(sourceRootPath, "Web");
        }

        /// <summary>
        /// Runs the full build and deployment pipeline.
        /// </summary>
        /// <param name="options">Build and deployment options.</param>
        /// <returns>Build and deployment result.</returns>
        public BrainInBuildDeploymentResult Run(BrainInBuildDeploymentOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            EnsureWebBuildSupportInstalled();

            var buildOutputPath = ToAbsoluteProjectPath(options.BuildOutputPath);
            var deploymentPackagePath = ToAbsoluteProjectPath(options.DeploymentPackagePath);
            var webRequirements = ResolveWebRequirementsSource();

            var result = new BrainInBuildDeploymentResult
            {
                BuildOutputPath = buildOutputPath,
                DeploymentPackagePath = deploymentPackagePath,
                WebRequirementsMode = webRequirements.Mode,
                WebRequirementsResolvedSourcePath = webRequirements.SourceRootPath,
                BuildResult = "NotStarted"
            };

            ApplyBuildPreset(options.BuildPreset);

            var validationResults = RunValidation();
            FillValidationSummary(result, validationResults);

            if (result.ValidationErrors > 0 && options.StopOnValidationErrors)
            {
                PrepareDeploymentDirectory(deploymentPackagePath, options.CleanDeploymentPackageBeforeCopy);

                result.ValidationReportPath = ExportValidationReport(validationResults, deploymentPackagePath);
                result.DeploymentManifestPath = ExportDeploymentManifest(
                    options,
                    deploymentPackagePath,
                    result,
                    buildWasCreated: false
                );

                result.Succeeded = false;
                result.Message = "Build was stopped because validation produced errors.";
                AssetDatabase.Refresh();
                return result;
            }

            if (options.CleanBuildOutputBeforeBuild && Directory.Exists(buildOutputPath))
                Directory.Delete(buildOutputPath, true);

            var buildReport = BuildWebGL(buildOutputPath, options.BuildPreset);
            result.BuildResult = buildReport.summary.result.ToString();

            if (buildReport.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                PrepareDeploymentDirectory(deploymentPackagePath, options.CleanDeploymentPackageBeforeCopy);

                result.ValidationReportPath = ExportValidationReport(validationResults, deploymentPackagePath);
                result.DeploymentManifestPath = ExportDeploymentManifest(
                    options,
                    deploymentPackagePath,
                    result,
                    buildWasCreated: false
                );

                result.Succeeded = false;
                result.Message = $"WebGL build failed with result: {buildReport.summary.result}.";
                AssetDatabase.Refresh();
                return result;
            }

            PrepareDeploymentDirectory(deploymentPackagePath, options.CleanDeploymentPackageBeforeCopy);
            CopyDirectoryContents(buildOutputPath, deploymentPackagePath, overwriteExisting: true);

            var gameName = ResolveGameName(options);
            var safeGameName = CreateSafeFileName(gameName);

            NormalizeUnityBuildFileNames(
                Path.Combine(deploymentPackagePath, "Build"),
                safeGameName,
                result
            );

            CopyWebRequirementsContents(
                webRequirements.SourceRootPath,
                deploymentPackagePath,
                safeGameName,
                result
            );

            result.ValidationReportPath = ExportValidationReport(validationResults, deploymentPackagePath);
            result.DeploymentManifestPath = ExportDeploymentManifest(
                options,
                deploymentPackagePath,
                result,
                buildWasCreated: true
            );

            result.Succeeded = true;
            result.Message = "Build and deployment package were created successfully.";

            AssetDatabase.Refresh();

            return result;
        }

        /// <summary>
        /// Checks whether WebGL/Web build support is installed for the current Unity Editor.
        /// </summary>
        private static void EnsureWebBuildSupportInstalled()
        {
            if (BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
                return;

            throw new InvalidOperationException(
                "WebGL/Web Build Support is not installed for this Unity Editor installation. " +
                "Open Unity Hub, find this Unity version in Installs, choose Add modules, and install WebGL Build Support / Web Build Support. " +
                $"Unity instructions: {WebGlModuleDocumentationUrl}"
            );
        }

        /// <summary>
        /// Resolves Web requirements from a local Web folder or from the configured Git repository.
        /// </summary>
        /// <returns>Resolved Web requirements source.</returns>
        private static WebRequirementsSource ResolveWebRequirementsSource()
        {
            if (TryFindLocalWebRequirementsSourceRootPath(out var localRequirementsSourceRootPath))
            {
                return new WebRequirementsSource
                {
                    Mode = "LocalWebFolder",
                    SourceRootPath = localRequirementsSourceRootPath
                };
            }

            if (string.IsNullOrWhiteSpace(WebRequirementsRepositoryUrl))
            {
                throw new InvalidOperationException(
                    "Web requirements files are not detected and the Git repository URL is not configured. " +
                    "Either add a Web/ folder to the project root, add Assets/Web/, or fill WebRequirementsRepositoryUrl in BrainInBuildDeploymentRunner."
                );
            }

            var repositoryPath = CloneOrUpdateWebRequirementsRepository(
                WebRequirementsRepositoryUrl,
                WebRequirementsRepositoryBranch
            );

            if (ResolveWebFolderPath(repositoryPath) == null)
            {
                throw new DirectoryNotFoundException(
                    $"The configured Web requirements repository does not contain a Web/ folder: {repositoryPath}"
                );
            }

            return new WebRequirementsSource
            {
                Mode = "GitRepository",
                SourceRootPath = repositoryPath
            };
        }

        /// <summary>
        /// Tries to find local Web requirements in supported Unity project locations.
        /// Returned path is the requirements source root, not the Web folder itself.
        /// </summary>
        /// <param name="sourceRootPath">Detected source root path.</param>
        /// <returns>True if local Web requirements were found.</returns>
        private static bool TryFindLocalWebRequirementsSourceRootPath(out string sourceRootPath)
        {
            var projectRootPath = GetProjectRootPath();

            var candidateSourceRootPaths = new[]
            {
                projectRootPath,
                Path.Combine(projectRootPath, "Assets")
            };

            foreach (var candidateSourceRootPath in candidateSourceRootPaths)
            {
                if (!Directory.Exists(candidateSourceRootPath))
                    continue;

                if (ResolveWebFolderPath(candidateSourceRootPath) != null)
                {
                    sourceRootPath = candidateSourceRootPath;
                    return true;
                }
            }

            sourceRootPath = null;
            return false;
        }

        /// <summary>
        /// Clones or updates the Web requirements Git repository into the Unity project Library cache.
        /// </summary>
        /// <param name="repositoryUrl">Git repository URL.</param>
        /// <param name="branch">Optional branch name.</param>
        /// <returns>Local repository path.</returns>
        private static string CloneOrUpdateWebRequirementsRepository(string repositoryUrl, string branch)
        {
            var cacheKey = CreateStableHash($"{repositoryUrl}|{branch}");
            var cacheRoot = Path.Combine(
                GetProjectRootPath(),
                "Library",
                "BrainInDevTools",
                "WebRequirements",
                cacheKey
            );

            var gitDirectory = Path.Combine(cacheRoot, ".git");

            if (!Directory.Exists(gitDirectory))
            {
                if (Directory.Exists(cacheRoot))
                    Directory.Delete(cacheRoot, true);

                var cacheParent = Path.GetDirectoryName(cacheRoot);

                if (!string.IsNullOrWhiteSpace(cacheParent))
                    Directory.CreateDirectory(cacheParent);

                var cloneArguments = new StringBuilder();
                cloneArguments.Append("clone --depth 1 ");

                if (!string.IsNullOrWhiteSpace(branch))
                {
                    cloneArguments.Append("--branch ");
                    cloneArguments.Append(QuoteArgument(branch));
                    cloneArguments.Append(' ');
                }

                cloneArguments.Append(QuoteArgument(repositoryUrl));
                cloneArguments.Append(' ');
                cloneArguments.Append(QuoteArgument(cacheRoot));

                RunGitCommand(GetProjectRootPath(), cloneArguments.ToString());
                return cacheRoot;
            }

            if (!string.IsNullOrWhiteSpace(branch))
            {
                RunGitCommand(cacheRoot, "fetch --depth 1 origin " + QuoteArgument(branch));
                RunGitCommand(cacheRoot, "checkout " + QuoteArgument(branch));
                RunGitCommand(cacheRoot, "pull --ff-only origin " + QuoteArgument(branch));
            }
            else
            {
                RunGitCommand(cacheRoot, "pull --ff-only");
            }

            return cacheRoot;
        }

        /// <summary>
        /// Runs a Git command and throws a readable exception when Git fails.
        /// </summary>
        /// <param name="workingDirectory">Working directory.</param>
        /// <param name="arguments">Git arguments.</param>
        private static void RunGitCommand(string workingDirectory, string arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            Process process;

            try
            {
                process = Process.Start(startInfo);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    "Git executable was not found. Install Git or add a Web/ folder to the project root.",
                    exception
                );
            }

            if (process == null)
                throw new InvalidOperationException("Git process could not be started.");

            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();

            process.WaitForExit();

            if (process.ExitCode == 0)
                return;

            throw new InvalidOperationException(
                $"Git command failed: git {arguments}\n{standardOutput}\n{standardError}"
            );
        }

        /// <summary>
        /// Runs all default BrainIn DevTools validation rules.
        /// </summary>
        /// <returns>Validation results.</returns>
        private static IReadOnlyList<ValidationResult> RunValidation()
        {
            var projectRootPath = GetProjectRootPath();

            var context = new ValidationContext(
                projectRootPath,
                Path.Combine(projectRootPath, "Assets")
            );

            var runner = ValidationRuleRegistry.CreateDefaultRunner();
            return runner.Run(context);
        }

        /// <summary>
        /// Applies WebGL build settings based on selected preset.
        /// </summary>
        /// <param name="preset">Selected build preset.</param>
        private static void ApplyBuildPreset(BrainInBuildPreset preset)
        {
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            {
                throw new InvalidOperationException(
                    "Could not switch active build target to WebGL. " +
                    "Check that WebGL/Web Build Support is installed in Unity Hub."
                );
            }

            switch (preset)
            {
                case BrainInBuildPreset.FastBuild:
                    EditorUserBuildSettings.development = true;
                    PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
                    PlayerSettings.WebGL.decompressionFallback = false;
                    break;

                case BrainInBuildPreset.ReleaseSize:
                    EditorUserBuildSettings.development = false;
                    PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
                    PlayerSettings.WebGL.decompressionFallback = true;
                    break;

                default:
                    EditorUserBuildSettings.development = false;
                    PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
                    PlayerSettings.WebGL.decompressionFallback = true;
                    break;
            }
        }

        /// <summary>
        /// Builds the project as WebGL.
        /// </summary>
        /// <param name="buildOutputPath">Absolute build output path.</param>
        /// <param name="preset">Selected build preset.</param>
        /// <returns>Unity build report.</returns>
        private static BuildReport BuildWebGL(string buildOutputPath, BrainInBuildPreset preset)
        {
            var scenes = GetEnabledBuildScenes();

            if (scenes.Length == 0)
                throw new InvalidOperationException("No enabled scenes were found in Build Settings.");

            var buildOptions = BuildOptions.None;

            if (preset == BrainInBuildPreset.FastBuild)
                buildOptions |= BuildOptions.Development;

            var buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = buildOutputPath,
                target = BuildTarget.WebGL,
                options = buildOptions
            };

            return BuildPipeline.BuildPlayer(buildPlayerOptions);
        }

        /// <summary>
        /// Gets enabled scenes from Build Settings.
        /// </summary>
        /// <returns>Enabled scene paths.</returns>
        private static string[] GetEnabledBuildScenes()
        {
            return EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .ToArray();
        }

        /// <summary>
        /// Fills validation summary into the build deployment result.
        /// </summary>
        /// <param name="result">Result to populate.</param>
        /// <param name="validationResults">Validation results.</param>
        private static void FillValidationSummary(
            BrainInBuildDeploymentResult result,
            IReadOnlyList<ValidationResult> validationResults)
        {
            var safeResults = validationResults ?? Array.Empty<ValidationResult>();

            result.ValidationErrors = safeResults.Count(item => item.Severity == ValidationSeverity.Error);
            result.ValidationWarnings = safeResults.Count(item => item.Severity == ValidationSeverity.Warning);
            result.ValidationInfos = safeResults.Count(item => item.Severity == ValidationSeverity.Info);
        }

        /// <summary>
        /// Exports validation report into the deployment package directory.
        /// </summary>
        /// <param name="validationResults">Validation results.</param>
        /// <param name="deploymentPackagePath">Deployment package path.</param>
        /// <returns>Exported report path.</returns>
        private static string ExportValidationReport(
            IReadOnlyList<ValidationResult> validationResults,
            string deploymentPackagePath)
        {
            Directory.CreateDirectory(deploymentPackagePath);

            var validationReportPath = Path.Combine(
                deploymentPackagePath,
                $"BrainInValidationReport_{DateTime.Now:yyyyMMdd_HHmmss}.json"
            );

            ValidationReportExporter.ExportJson(validationResults, validationReportPath);

            return validationReportPath;
        }

        /// <summary>
        /// Exports deployment manifest into the deployment package directory.
        /// </summary>
        /// <param name="options">Build and deployment options.</param>
        /// <param name="deploymentPackagePath">Deployment package path.</param>
        /// <param name="result">Current build and deployment result.</param>
        /// <param name="buildWasCreated">Whether the WebGL build was created successfully.</param>
        /// <returns>Exported manifest path.</returns>
        private string ExportDeploymentManifest(
            BrainInBuildDeploymentOptions options,
            string deploymentPackagePath,
            BrainInBuildDeploymentResult result,
            bool buildWasCreated)
        {
            Directory.CreateDirectory(deploymentPackagePath);

            var manifestOptions = options.ManifestOptions ?? new BrainInDeploymentManifestGenerationOptions();
            manifestOptions.BuildPreset = options.BuildPreset;
            manifestOptions.BuildOutputPath = result.BuildOutputPath;
            manifestOptions.DeploymentPackagePath = deploymentPackagePath;

            var manifest = _manifestGenerator.Generate(manifestOptions);

            manifest.Validation.Passed = result.ValidationErrors == 0;
            manifest.Validation.Errors = result.ValidationErrors;
            manifest.Validation.Warnings = result.ValidationWarnings;
            manifest.Validation.Infos = result.ValidationInfos;
            manifest.Validation.ReportPath = result.ValidationReportPath;

            manifest.Build.OutputPath = result.BuildOutputPath;
            manifest.Build.DeploymentPackagePath = deploymentPackagePath;

            ApplyBuildSettingsToManifest(manifest, options.BuildPreset);

            if (buildWasCreated)
            {
                manifest.Build.Files.HasBuildFolder = Directory.Exists(Path.Combine(deploymentPackagePath, "Build"));
                manifest.Build.Files.HasTemplateDataFolder =
                    Directory.Exists(Path.Combine(deploymentPackagePath, "TemplateData"));
                manifest.Build.Files.HasStreamingAssetsFolder =
                    Directory.Exists(Path.Combine(deploymentPackagePath, "StreamingAssets"));
            }

            foreach (var warning in result.DeploymentWarnings)
                manifest.GenerationWarnings.Add(warning);

            var manifestPath = Path.Combine(
                deploymentPackagePath,
                BrainInDeploymentManifestExporter.CreateDefaultFileName()
            );

            BrainInDeploymentManifestExporter.Export(manifest, manifestPath);

            result.DeploymentManifest = manifest;

            return manifestPath;
        }

        /// <summary>
        /// Applies selected build preset metadata to the deployment manifest.
        /// </summary>
        /// <param name="manifest">Deployment manifest.</param>
        /// <param name="preset">Selected build preset.</param>
        private static void ApplyBuildSettingsToManifest(
            BrainInDeploymentManifest manifest,
            BrainInBuildPreset preset)
        {
            switch (preset)
            {
                case BrainInBuildPreset.FastBuild:
                    manifest.Build.Preset = preset.ToString();
                    manifest.Build.DevelopmentBuild = true;
                    manifest.Build.CompressionFormat = "Disabled";
                    manifest.Build.DecompressionFallback = false;
                    manifest.Build.Optimization = "ShorterBuildTime";
                    break;

                case BrainInBuildPreset.ReleaseSize:
                    manifest.Build.Preset = preset.ToString();
                    manifest.Build.DevelopmentBuild = false;
                    manifest.Build.CompressionFormat = "Gzip";
                    manifest.Build.DecompressionFallback = true;
                    manifest.Build.Optimization = "DiskSize";
                    break;

                default:
                    manifest.Build.Preset = preset.ToString();
                    manifest.Build.DevelopmentBuild = false;
                    manifest.Build.CompressionFormat = "Brotli";
                    manifest.Build.DecompressionFallback = true;
                    manifest.Build.Optimization = "DiskSize";
                    break;
            }
        }

        /// <summary>
        /// Prepares deployment package directory.
        /// </summary>
        /// <param name="deploymentPackagePath">Deployment package path.</param>
        /// <param name="cleanBeforeUse">Whether to delete existing contents first.</param>
        private static void PrepareDeploymentDirectory(string deploymentPackagePath, bool cleanBeforeUse)
        {
            if (cleanBeforeUse && Directory.Exists(deploymentPackagePath))
                Directory.Delete(deploymentPackagePath, true);

            Directory.CreateDirectory(deploymentPackagePath);
        }

        /// <summary>
        /// Copies all contents from one directory into another directory.
        /// </summary>
        /// <param name="sourceDirectory">Source directory.</param>
        /// <param name="targetDirectory">Target directory.</param>
        /// <param name="overwriteExisting">Whether existing files should be overwritten.</param>
        private static int CopyDirectoryContents(
            string sourceDirectory,
            string targetDirectory,
            bool overwriteExisting)
        {
            if (!Directory.Exists(sourceDirectory))
                throw new DirectoryNotFoundException($"Source directory does not exist: {sourceDirectory}");

            Directory.CreateDirectory(targetDirectory);

            var copiedFiles = 0;
            var normalizedSourceDirectory = Path.GetFullPath(sourceDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            foreach (var sourceFilePath in Directory.GetFiles(normalizedSourceDirectory, "*",
                         SearchOption.AllDirectories))
            {
                if (ShouldSkipRequirementsFile(normalizedSourceDirectory, sourceFilePath))
                    continue;

                var relativePath = sourceFilePath.Substring(normalizedSourceDirectory.Length)
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                var targetFilePath = Path.Combine(targetDirectory, relativePath);

                if (!overwriteExisting && File.Exists(targetFilePath))
                    continue;

                var targetFileDirectory = Path.GetDirectoryName(targetFilePath);

                if (!string.IsNullOrWhiteSpace(targetFileDirectory))
                    Directory.CreateDirectory(targetFileDirectory);

                File.Copy(sourceFilePath, targetFilePath, overwriteExisting);
                copiedFiles++;
            }

            return copiedFiles;
        }

        /// <summary>
        /// Renames Unity WebGL build files from the generated Unity prefix to the BrainIn game name prefix.
        /// </summary>
        /// <param name="buildFolderPath">Deployment Build folder path.</param>
        /// <param name="safeGameName">Safe game name used as file prefix.</param>
        /// <param name="result">Build deployment result.</param>
        private static void NormalizeUnityBuildFileNames(
            string buildFolderPath,
            string safeGameName,
            BrainInBuildDeploymentResult result)
        {
            if (!Directory.Exists(buildFolderPath))
                throw new DirectoryNotFoundException($"Build folder was not found: {buildFolderPath}");

            var loaderFilePath = Directory
                .GetFiles(buildFolderPath, "*.loader.js", SearchOption.TopDirectoryOnly)
                .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(loaderFilePath))
            {
                result.DeploymentWarnings.Add(
                    $"No Unity loader file (*.loader.js) was found in build folder: {buildFolderPath}"
                );

                return;
            }

            var currentPrefix = Path.GetFileName(loaderFilePath)
                .Replace(".loader.js", string.Empty);

            if (string.Equals(currentPrefix, safeGameName, StringComparison.Ordinal))
                return;

            foreach (var sourceFilePath in Directory.GetFiles(buildFolderPath, currentPrefix + ".*",
                         SearchOption.TopDirectoryOnly))
            {
                var fileName = Path.GetFileName(sourceFilePath);
                var newFileName = safeGameName + fileName.Substring(currentPrefix.Length);
                var targetFilePath = Path.Combine(buildFolderPath, newFileName);

                if (File.Exists(targetFilePath))
                    File.Delete(targetFilePath);

                File.Move(sourceFilePath, targetFilePath);
            }

            var indexPath = Path.Combine(Path.GetDirectoryName(buildFolderPath) ?? string.Empty, "index.html");

            if (File.Exists(indexPath))
                ReplaceTextInFile(indexPath, currentPrefix, safeGameName);
        }

        /// <summary>
        /// Copies Web and StreamingAssets requirements into the final deployment package.
        /// </summary>
        /// <param name="requirementsSourceRootPath">Resolved requirements source root path.</param>
        /// <param name="deploymentPackagePath">Deployment package path.</param>
        /// <param name="gameName">Game name used for placeholder script rename.</param>
        /// <param name="result">Build deployment result to populate.</param>
        private static void CopyWebRequirementsContents(
            string requirementsSourceRootPath,
            string deploymentPackagePath,
            string gameName,
            BrainInBuildDeploymentResult result)
        {
            var webSourcePath = ResolveWebFolderPath(requirementsSourceRootPath);

            if (webSourcePath == null)
                throw new DirectoryNotFoundException(
                    $"Web/ folder was not found in requirements source: {requirementsSourceRootPath}");

            result.WebFilesCopied = CopyWebFolder(
                webSourcePath,
                deploymentPackagePath,
                gameName,
                result
            );

            var streamingAssetsSourcePath = Path.Combine(requirementsSourceRootPath, "StreamingAssets");

            if (Directory.Exists(streamingAssetsSourcePath))
            {
                result.StreamingAssetsFilesCopied = CopyDirectoryContents(
                    streamingAssetsSourcePath,
                    Path.Combine(deploymentPackagePath, "StreamingAssets"),
                    overwriteExisting: false
                );
            }
        }

        /// <summary>
        /// Copies the Web folder contents into deployment root and renames the placeholder game script.
        /// </summary>
        /// <param name="webSourcePath">Source Web folder.</param>
        /// <param name="webTargetPath">Target deployment root folder.</param>
        /// <param name="gameName">Game name.</param>
        /// <param name="result">Build deployment result.</param>
        /// <returns>Number of copied files.</returns>
        private static int CopyWebFolder(
            string webSourcePath,
            string webTargetPath,
            string gameName,
            BrainInBuildDeploymentResult result)
        {
            Directory.CreateDirectory(webTargetPath);

            var copiedFiles = 0;
            var placeholderFound = false;
            var safeGameName = CreateSafeFileName(gameName);
            var normalizedSourceDirectory = Path.GetFullPath(webSourcePath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            foreach (var sourceFilePath in Directory.GetFiles(normalizedSourceDirectory, "*",
                         SearchOption.AllDirectories))
            {
                if (ShouldSkipRequirementsFile(normalizedSourceDirectory, sourceFilePath))
                    continue;

                var relativePath = sourceFilePath.Substring(normalizedSourceDirectory.Length)
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                var targetRelativePath = relativePath;
                var sourceFileName = Path.GetFileName(sourceFilePath);

                if (IsPlaceholderGameScript(sourceFileName))
                {
                    var relativeDirectory = Path.GetDirectoryName(relativePath);
                    var renamedFileName = $"{safeGameName}.js";

                    targetRelativePath = string.IsNullOrWhiteSpace(relativeDirectory)
                        ? renamedFileName
                        : Path.Combine(relativeDirectory, renamedFileName);

                    placeholderFound = true;
                }

                var targetFilePath = Path.Combine(webTargetPath, targetRelativePath);
                var targetFileDirectory = Path.GetDirectoryName(targetFilePath);

                if (!string.IsNullOrWhiteSpace(targetFileDirectory))
                    Directory.CreateDirectory(targetFileDirectory);

                CopyFileWithGameNameReplacement(sourceFilePath, targetFilePath, safeGameName);
                copiedFiles++;

                if (IsPlaceholderGameScript(sourceFileName))
                    result.RenamedWebScriptPath = targetFilePath;
            }

            if (!placeholderFound)
            {
                result.DeploymentWarnings.Add(
                    "Web requirements were copied, but no TODO_SET_GAME_NAME.js or TODO_SET_NAME.js placeholder script was found. This is valid if the game script has already been renamed."
                );
            }

            return copiedFiles;
        }

        /// <summary>
        /// Copies a file and replaces game name placeholders in text files.
        /// </summary>
        /// <param name="sourceFilePath">Source file path.</param>
        /// <param name="targetFilePath">Target file path.</param>
        /// <param name="safeGameName">Safe game name.</param>
        private static void CopyFileWithGameNameReplacement(
            string sourceFilePath,
            string targetFilePath,
            string safeGameName)
        {
            if (!IsTextFile(sourceFilePath))
            {
                File.Copy(sourceFilePath, targetFilePath, true);
                return;
            }

            var content = File.ReadAllText(sourceFilePath, Encoding.UTF8);

            content = content
                .Replace("TODO_SET_GAME_NAME", safeGameName)
                .Replace("TODO_SET_NAME", safeGameName);

            File.WriteAllText(targetFilePath, content, new UTF8Encoding(false));
        }

        /// <summary>
        /// Replaces text in a file.
        /// </summary>
        /// <param name="filePath">File path.</param>
        /// <param name="oldValue">Old value.</param>
        /// <param name="newValue">New value.</param>
        private static void ReplaceTextInFile(string filePath, string oldValue, string newValue)
        {
            if (!IsTextFile(filePath))
                return;

            var content = File.ReadAllText(filePath, Encoding.UTF8);

            if (!content.Contains(oldValue))
                return;

            content = content.Replace(oldValue, newValue);
            File.WriteAllText(filePath, content, new UTF8Encoding(false));
        }

        /// <summary>
        /// Determines whether a file should be treated as text.
        /// </summary>
        /// <param name="filePath">File path.</param>
        /// <returns>True if file is text-like.</returns>
        private static bool IsTextFile(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();

            return extension is ".js"
                or ".html"
                or ".htm"
                or ".css"
                or ".json"
                or ".xml"
                or ".txt"
                or ".md";
        }

        /// <summary>
        /// Resolves the Web folder path from the requirements source root.
        /// </summary>
        /// <param name="requirementsSourceRootPath">Requirements source root path.</param>
        /// <returns>Web folder path or null.</returns>
        private static string ResolveWebFolderPath(string requirementsSourceRootPath)
        {
            if (string.IsNullOrWhiteSpace(requirementsSourceRootPath))
                return null;

            var nestedWebPath = Path.Combine(requirementsSourceRootPath, "Web");

            if (Directory.Exists(nestedWebPath))
                return nestedWebPath;

            var nestedLowercaseWebPath = Path.Combine(requirementsSourceRootPath, "web");

            if (Directory.Exists(nestedLowercaseWebPath))
                return nestedLowercaseWebPath;

            if (string.Equals(
                    Path.GetFileName(requirementsSourceRootPath),
                    "Web",
                    StringComparison.OrdinalIgnoreCase) &&
                Directory.Exists(requirementsSourceRootPath))
            {
                return requirementsSourceRootPath;
            }

            return null;
        }

        /// <summary>
        /// Determines whether a file should be skipped while copying requirements.
        /// </summary>
        /// <param name="sourceRootPath">Source root path.</param>
        /// <param name="sourceFilePath">Source file path.</param>
        /// <returns>True if the file should be skipped.</returns>
        private static bool ShouldSkipRequirementsFile(string sourceRootPath, string sourceFilePath)
        {
            var fileName = Path.GetFileName(sourceFilePath);

            if (IgnoredRequirementsFileNames.Any(ignored =>
                    string.Equals(fileName, ignored, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            if (string.Equals(Path.GetExtension(sourceFilePath), ".meta", StringComparison.OrdinalIgnoreCase))
                return true;

            var relativePath = sourceFilePath.Substring(sourceRootPath.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            var pathParts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            return pathParts.Any(part =>
                IgnoredRequirementsDirectories.Any(ignored =>
                    string.Equals(part, ignored, StringComparison.OrdinalIgnoreCase)));
        }

        /// <summary>
        /// Determines whether the file is the placeholder game script.
        /// </summary>
        /// <param name="fileName">File name.</param>
        /// <returns>True if the file is a placeholder script.</returns>
        private static bool IsPlaceholderGameScript(string fileName)
        {
            return WebTemplatePlaceholderFileNames.Any(placeholder =>
                string.Equals(fileName, placeholder, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Resolves the game name used for generated Web files.
        /// </summary>
        /// <param name="options">Build deployment options.</param>
        /// <returns>Resolved game name.</returns>
        private static string ResolveGameName(BrainInBuildDeploymentOptions options)
        {
            if (!string.IsNullOrWhiteSpace(options.ManifestOptions?.ProgramName))
                return options.ManifestOptions.ProgramName;

            if (!string.IsNullOrWhiteSpace(PlayerSettings.productName))
                return PlayerSettings.productName;

            return "Game";
        }

        /// <summary>
        /// Creates a safe file name from an arbitrary game name.
        /// </summary>
        /// <param name="name">Input name.</param>
        /// <returns>Safe file name.</returns>
        private static string CreateSafeFileName(string name)
        {
            var safeName = string.IsNullOrWhiteSpace(name) ? "Game" : name.Trim();

            foreach (var invalidCharacter in Path.GetInvalidFileNameChars())
                safeName = safeName.Replace(invalidCharacter, '_');

            safeName = safeName.Replace(' ', '_');

            return string.IsNullOrWhiteSpace(safeName) ? "Game" : safeName;
        }

        /// <summary>
        /// Creates a stable short hash string.
        /// </summary>
        /// <param name="value">Input value.</param>
        /// <returns>Short hash string.</returns>
        private static string CreateStableHash(string value)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(value ?? "");
                var hash = sha256.ComputeHash(bytes);

                return string.Concat(hash.Take(8).Select(item => item.ToString("x2")));
            }
        }

        /// <summary>
        /// Quotes a command-line argument.
        /// </summary>
        /// <param name="value">Argument value.</param>
        /// <returns>Quoted argument.</returns>
        private static string QuoteArgument(string value)
        {
            return "\"" + (value ?? "").Replace("\"", "\\\"") + "\"";
        }

        /// <summary>
        /// Converts a project-relative path to an absolute path.
        /// Absolute paths are returned unchanged.
        /// </summary>
        /// <param name="path">Input path.</param>
        /// <returns>Absolute path.</returns>
        private static string ToAbsoluteProjectPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path cannot be empty.", nameof(path));

            if (Path.IsPathRooted(path))
                return Path.GetFullPath(path);

            return Path.GetFullPath(Path.Combine(GetProjectRootPath(), path));
        }

        /// <summary>
        /// Gets the current Unity project root path.
        /// </summary>
        /// <returns>Unity project root path.</returns>
        private static string GetProjectRootPath()
        {
            var assetsDirectory = new DirectoryInfo(Application.dataPath);
            return assetsDirectory.Parent?.FullName ?? Application.dataPath;
        }

        /// <summary>
        /// Resolved Web requirements source.
        /// </summary>
        private sealed class WebRequirementsSource
        {
            public string Mode { get; set; }
            public string SourceRootPath { get; set; }
        }
    }
}