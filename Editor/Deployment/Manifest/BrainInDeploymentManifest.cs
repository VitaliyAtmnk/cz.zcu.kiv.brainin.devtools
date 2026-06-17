using System;
using System.Collections.Generic;

namespace BrainIn.DevTools.Editor.Deployment.Manifest
{
    /// <summary>
    /// Describes a BrainIn deployment manifest generated from a Unity task project.
    /// </summary>
    public sealed class BrainInDeploymentManifest
    {
        /// <summary>
        /// Gets or sets the manifest format version.
        /// </summary>
        public string ManifestVersion { get; set; } = "1.0";

        /// <summary>
        /// Gets or sets manifest generation time in UTC.
        /// </summary>
        public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets information about the generator.
        /// </summary>
        public GeneratorManifestData Generator { get; set; } = new GeneratorManifestData();

        /// <summary>
        /// Gets or sets Unity project metadata.
        /// </summary>
        public ProjectManifestData Project { get; set; } = new ProjectManifestData();

        /// <summary>
        /// Gets or sets detected BrainIn template metadata.
        /// </summary>
        public BrainInTemplateManifestData BrainInTemplate { get; set; } = new BrainInTemplateManifestData();

        /// <summary>
        /// Gets or sets BrainIn program metadata intended for the web form.
        /// </summary>
        public ProgramManifestData Program { get; set; } = new ProgramManifestData();

        /// <summary>
        /// Gets or sets build metadata.
        /// </summary>
        public BuildManifestData Build { get; set; } = new BuildManifestData();

        /// <summary>
        /// Gets or sets validation metadata.
        /// </summary>
        public ValidationManifestData Validation { get; set; } = new ValidationManifestData();

        /// <summary>
        /// Gets or sets localization metadata.
        /// </summary>
        public LocalizationManifestData Localization { get; set; } = new LocalizationManifestData();

        /// <summary>
        /// Gets or sets input contract metadata.
        /// </summary>
        public InputContractManifestData InputContract { get; set; } = new InputContractManifestData();

        /// <summary>
        /// Gets or sets output contract metadata.
        /// </summary>
        public OutputContractManifestData OutputContract { get; set; } = new OutputContractManifestData();

        /// <summary>
        /// Gets or sets BrainIn deployment metadata.
        /// </summary>
        public DeploymentManifestData Deployment { get; set; } = new DeploymentManifestData();

        /// <summary>
        /// Gets generation warnings.
        /// </summary>
        public List<string> GenerationWarnings { get; } = new List<string>();
    }

    /// <summary>
    /// Describes the tool that generated the manifest.
    /// </summary>
    public sealed class GeneratorManifestData
    {
        public string Name { get; set; } = "BrainIn DevTools";
        public string PackageName { get; set; } = "cz.zcu.kiv.brainin.devtools";
        public string PackageVersion { get; set; } = "";
    }

    /// <summary>
    /// Describes Unity project metadata.
    /// </summary>
    public sealed class ProjectManifestData
    {
        public string UnityProjectName { get; set; }
        public string UnityVersion { get; set; }
        public string ProductName { get; set; }
        public string CompanyName { get; set; }
        public string BuildTarget { get; set; }
    }

    /// <summary>
    /// Describes detected BrainIn template metadata.
    /// </summary>
    public sealed class BrainInTemplateManifestData
    {
        public string PackageName { get; set; } = "cz.zcu.kiv.fav";
        public string RuntimeAssembly { get; set; } = "BrainInTemplate.Runtime";
        public string PackageVersion { get; set; } = "";
        public bool Detected { get; set; }
    }

    /// <summary>
    /// Describes BrainIn program metadata intended for the web form.
    /// </summary>
    public sealed class ProgramManifestData
    {
        public string Name { get; set; }
        public LocalizedTextManifestData Description { get; set; } = new LocalizedTextManifestData();
        public string Category { get; set; }
        public string Version { get; set; } = "1.0.0";
        public string Author { get; set; }
        public string Notes { get; set; }
    }

    /// <summary>
    /// Stores localized text in supported BrainIn languages.
    /// </summary>
    public sealed class LocalizedTextManifestData
    {
        public string Cs { get; set; } = "";
        public string En { get; set; } = "";
        public string De { get; set; } = "";
    }

    /// <summary>
    /// Describes build metadata.
    /// </summary>
    public sealed class BuildManifestData
    {
        public string Preset { get; set; }
        public bool DevelopmentBuild { get; set; }
        public string CompressionFormat { get; set; }
        public string Optimization { get; set; }
        public string OutputPath { get; set; }
        public string DeploymentPackagePath { get; set; }
        public bool DecompressionFallback { get; set; }
        public BuildFilesManifestData Files { get; set; } = new BuildFilesManifestData();
    }

    /// <summary>
    /// Describes expected build output folders and files.
    /// </summary>
    public sealed class BuildFilesManifestData
    {
        public bool HasBuildFolder { get; set; }
        public bool HasTemplateDataFolder { get; set; }
        public bool HasStreamingAssetsFolder { get; set; }
        public string LocalePath { get; set; } = "StreamingAssets/ProgramData/Locale/locale.xml";
    }

    /// <summary>
    /// Describes validation metadata related to the build.
    /// </summary>
    public sealed class ValidationManifestData
    {
        public bool Passed { get; set; }
        public int Errors { get; set; }
        public int Warnings { get; set; }
        public int Infos { get; set; }
        public string ReportPath { get; set; }
    }

    /// <summary>
    /// Describes localization metadata.
    /// </summary>
    public sealed class LocalizationManifestData
    {
        public string Path { get; set; }
        public List<string> Languages { get; } = new List<string>();
        public List<string> Keys { get; } = new List<string>();
    }

    /// <summary>
    /// Describes input contract metadata.
    /// </summary>
    public sealed class InputContractManifestData
    {
        public bool BaseInputParametersIncluded { get; set; }
        public string CustomInputParametersType { get; set; }
        public List<InputParameterManifestData> Parameters { get; } = new List<InputParameterManifestData>();
    }

    /// <summary>
    /// Describes one input parameter intended for the BrainIn web form.
    /// </summary>
    public sealed class InputParameterManifestData
    {
        public string Key { get; set; }
        public LocalizedTextManifestData DisplayName { get; set; } = new LocalizedTextManifestData();
        public LocalizedTextManifestData Description { get; set; } = new LocalizedTextManifestData();
        public string BrainInDataType { get; set; }
        public int BrainInDataTypeId { get; set; }
        public string DefaultValue { get; set; }
        public bool Required { get; set; } = true;
        public string Source { get; set; }
        public string SourceMember { get; set; }
        public string DotNetType { get; set; }
    }

    /// <summary>
    /// Describes output contract metadata.
    /// </summary>
    public sealed class OutputContractManifestData
    {
        public string CustomDataDeclarationSource { get; set; } = "ExpectedCustomDataKeyAttribute";
        public List<OutputParameterManifestData> RoundCustomData { get; } = new List<OutputParameterManifestData>();
        public List<OutputParameterManifestData> GlobalOutputs { get; } = new List<OutputParameterManifestData>();
    }

    /// <summary>
    /// Describes one output parameter intended for the BrainIn web form.
    /// </summary>
    public sealed class OutputParameterManifestData
    {
        public string Key { get; set; }
        public string BrainInDataType { get; set; }
        public int BrainInDataTypeId { get; set; }
        public LocalizedTextManifestData Description { get; set; } = new LocalizedTextManifestData();
        public string Scope { get; set; } = "RoundCustomData";
        public string Source { get; set; }
        public string SourceMember { get; set; }
        public string DotNetType { get; set; }
    }

    /// <summary>
    /// Describes deployment metadata.
    /// </summary>
    public sealed class DeploymentManifestData
    {
        public string Mode { get; set; } = "PreparePackage";
        public int? ProgramId { get; set; }
        public string TargetRelativePath { get; set; } = "Files/<ProgramId>/";
        public bool CopyToBrainInFilesFolder { get; set; }

        public List<string> PreparedFolders { get; } = new List<string>
        {
            "Build",
            "TemplateData",
            "StreamingAssets"
        };
    }
}