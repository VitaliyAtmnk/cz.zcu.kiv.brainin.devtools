using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using BrainIn.DevTools.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using JsonPropertyAttribute = Newtonsoft.Json.JsonPropertyAttribute;
using PackageManagerPackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace BrainIn.DevTools.Editor.Deployment.Manifest
{
    /// <summary>
    /// Build preset used by the deployment manifest.
    /// </summary>
    public enum BrainInBuildPreset
    {
        /// <summary>
        /// Fast WebGL build intended for development.
        /// </summary>
        FastBuild,

        /// <summary>
        /// Release WebGL build optimized for smaller disk size.
        /// </summary>
        ReleaseSize
    }

    /// <summary>
    /// Options used during deployment manifest generation.
    /// </summary>
    public sealed class BrainInDeploymentManifestGenerationOptions
    {
        /// <summary>
        /// Gets or sets the selected build preset.
        /// </summary>
        public BrainInBuildPreset BuildPreset { get; set; } = BrainInBuildPreset.ReleaseSize;

        /// <summary>
        /// Gets or sets the WebGL build output path.
        /// </summary>
        public string BuildOutputPath { get; set; } = "Builds/WebGL";

        /// <summary>
        /// Gets or sets the prepared deployment package path.
        /// </summary>
        public string DeploymentPackagePath { get; set; } = "Builds/Deployment";

        /// <summary>
        /// Gets or sets BrainIn program name.
        /// </summary>
        public string ProgramName { get; set; }

        /// <summary>
        /// Gets or sets Czech program description.
        /// </summary>
        public string ProgramDescriptionCs { get; set; }

        /// <summary>
        /// Gets or sets English program description.
        /// </summary>
        public string ProgramDescriptionEn { get; set; }

        /// <summary>
        /// Gets or sets German program description.
        /// </summary>
        public string ProgramDescriptionDe { get; set; }

        /// <summary>
        /// Gets or sets program category.
        /// </summary>
        public string ProgramCategory { get; set; }

        /// <summary>
        /// Gets or sets program version.
        /// </summary>
        public string ProgramVersion { get; set; } = "1.0.0";

        /// <summary>
        /// Gets or sets program author.
        /// </summary>
        public string ProgramAuthor { get; set; }

        /// <summary>
        /// Gets or sets optional notes.
        /// </summary>
        public string ProgramNotes { get; set; }

        /// <summary>
        /// Gets or sets optional BrainIn program ID.
        /// </summary>
        public int? ProgramId { get; set; }
    }

    /// <summary>
    /// Generates BrainIn deployment manifests from the currently open Unity project and scene.
    /// </summary>
    public sealed class BrainInDeploymentManifestGenerator
    {
        private const string BrainInTemplateRuntimeAssemblyName = "BrainInTemplate.Runtime";
        private const string BrainInTemplatePackageName = "cz.zcu.kiv.fav";
        private const string BrainInGameSettingsTypeName = "BrainInGameSettings";
        private const string CustomInputParametersFactoryPropertyName = "customInputParametersFactory";
        private const string LocaleAssetPath = "Assets/StreamingAssets/ProgramData/Locale/locale.xml";

        /// <summary>
        /// Generates a deployment manifest.
        /// </summary>
        /// <param name="options">Manifest generation options.</param>
        /// <returns>Generated manifest.</returns>
        public BrainInDeploymentManifest Generate(BrainInDeploymentManifestGenerationOptions options)
        {
            options ??= new BrainInDeploymentManifestGenerationOptions();

            var manifest = new BrainInDeploymentManifest();

            PopulateGenerator(manifest);
            PopulateProject(manifest);
            PopulateBrainInTemplate(manifest);
            PopulateProgram(manifest, options);
            PopulateBuild(manifest, options);
            PopulateDeployment(manifest, options);
            PopulateLocalization(manifest);
            PopulateInputContract(manifest);
            PopulateOutputContract(manifest);

            return manifest;
        }

        /// <summary>
        /// Populates generator metadata.
        /// </summary>
        /// <param name="manifest">Manifest to populate.</param>
        private static void PopulateGenerator(BrainInDeploymentManifest manifest)
        {
            manifest.Generator.Name = "BrainIn DevTools";
            manifest.Generator.PackageName = "cz.zcu.kiv.brainin.devtools";
            manifest.Generator.PackageVersion = GetPackageVersion("cz.zcu.kiv.brainin.devtools");
        }

        /// <summary>
        /// Populates Unity project metadata.
        /// </summary>
        /// <param name="manifest">Manifest to populate.</param>
        private static void PopulateProject(BrainInDeploymentManifest manifest)
        {
            manifest.Project.UnityProjectName = GetUnityProjectName();
            manifest.Project.UnityVersion = Application.unityVersion;
            manifest.Project.ProductName = PlayerSettings.productName;
            manifest.Project.CompanyName = PlayerSettings.companyName;
            manifest.Project.BuildTarget = EditorUserBuildSettings.activeBuildTarget.ToString();
        }

        /// <summary>
        /// Populates BrainIn template metadata.
        /// </summary>
        /// <param name="manifest">Manifest to populate.</param>
        private static void PopulateBrainInTemplate(BrainInDeploymentManifest manifest)
        {
            var runtimeAssembly = AppDomain.CurrentDomain
                .GetAssemblies()
                .FirstOrDefault(assembly => assembly.GetName().Name == BrainInTemplateRuntimeAssemblyName);

            manifest.BrainInTemplate.PackageName = BrainInTemplatePackageName;
            manifest.BrainInTemplate.RuntimeAssembly = BrainInTemplateRuntimeAssemblyName;
            manifest.BrainInTemplate.Detected = runtimeAssembly != null;
            manifest.BrainInTemplate.PackageVersion = runtimeAssembly == null
                ? ""
                : GetPackageVersion(runtimeAssembly);

            if (!manifest.BrainInTemplate.Detected)
            {
                manifest.GenerationWarnings.Add(
                    $"BrainIn template runtime assembly '{BrainInTemplateRuntimeAssemblyName}' was not detected."
                );
            }
        }

        /// <summary>
        /// Populates program metadata.
        /// </summary>
        /// <param name="manifest">Manifest to populate.</param>
        /// <param name="options">Generation options.</param>
        private static void PopulateProgram(
            BrainInDeploymentManifest manifest,
            BrainInDeploymentManifestGenerationOptions options)
        {
            manifest.Program.Name = string.IsNullOrWhiteSpace(options.ProgramName)
                ? PlayerSettings.productName
                : options.ProgramName.Trim();

            manifest.Program.Description.Cs = options.ProgramDescriptionCs ?? "";
            manifest.Program.Description.En = options.ProgramDescriptionEn ?? "";
            manifest.Program.Description.De = options.ProgramDescriptionDe ?? "";
            manifest.Program.Category = options.ProgramCategory ?? "";
            manifest.Program.Version = string.IsNullOrWhiteSpace(options.ProgramVersion)
                ? "1.0.0"
                : options.ProgramVersion.Trim();
            manifest.Program.Author = options.ProgramAuthor ?? "";
            manifest.Program.Notes = options.ProgramNotes ?? "";
        }

        /// <summary>
        /// Populates build metadata.
        /// </summary>
        /// <param name="manifest">Manifest to populate.</param>
        /// <param name="options">Generation options.</param>
        private static void PopulateBuild(
            BrainInDeploymentManifest manifest,
            BrainInDeploymentManifestGenerationOptions options)
        {
            manifest.Build.Preset = options.BuildPreset.ToString();
            manifest.Build.OutputPath = options.BuildOutputPath ?? "";
            manifest.Build.DeploymentPackagePath = options.DeploymentPackagePath ?? "";

            switch (options.BuildPreset)
            {
                case BrainInBuildPreset.FastBuild:
                    manifest.Build.DevelopmentBuild = true;
                    manifest.Build.CompressionFormat = "Disabled";
                    manifest.Build.DecompressionFallback = false;
                    manifest.Build.Optimization = "ShorterBuildTime";
                    break;

                case BrainInBuildPreset.ReleaseSize:
                    manifest.Build.DevelopmentBuild = false;
                    manifest.Build.CompressionFormat = "Brotli";
                    manifest.Build.DecompressionFallback = true;
                    manifest.Build.Optimization = "DiskSize";
                    break;

                default:
                    manifest.Build.DevelopmentBuild = false;
                    manifest.Build.CompressionFormat = "Brotli";
                    manifest.Build.DecompressionFallback = true;
                    manifest.Build.Optimization = "DiskSize";
                    break;
            }

            var outputPath = manifest.Build.OutputPath;

            if (!string.IsNullOrWhiteSpace(outputPath))
            {
                manifest.Build.Files.HasBuildFolder = Directory.Exists(Path.Combine(outputPath, "Build"));
                manifest.Build.Files.HasTemplateDataFolder = Directory.Exists(Path.Combine(outputPath, "TemplateData"));
                manifest.Build.Files.HasStreamingAssetsFolder = Directory.Exists(Path.Combine(outputPath, "StreamingAssets"));
            }
        }

        /// <summary>
        /// Populates deployment metadata.
        /// </summary>
        /// <param name="manifest">Manifest to populate.</param>
        /// <param name="options">Generation options.</param>
        private static void PopulateDeployment(
            BrainInDeploymentManifest manifest,
            BrainInDeploymentManifestGenerationOptions options)
        {
            manifest.Deployment.ProgramId = options.ProgramId;
            manifest.Deployment.TargetRelativePath = options.ProgramId.HasValue
                ? $"Files/{options.ProgramId.Value}/"
                : "Files/<ProgramId>/";
            manifest.Deployment.CopyToBrainInFilesFolder = false;
            manifest.Deployment.Mode = "PreparePackage";
        }

        /// <summary>
        /// Populates localization metadata from locale.xml.
        /// </summary>
        /// <param name="manifest">Manifest to populate.</param>
        private static void PopulateLocalization(BrainInDeploymentManifest manifest)
        {
            manifest.Localization.Path = LocaleAssetPath;

            if (!File.Exists(LocaleAssetPath))
            {
                manifest.GenerationWarnings.Add($"Localization file was not found at '{LocaleAssetPath}'.");
                return;
            }

            try
            {
                var document = XDocument.Load(LocaleAssetPath);
                var languageNames = document
                    .Descendants()
                    .Where(element => element.Name.LocalName is "cs" or "en" or "de")
                    .Select(element => element.Name.LocalName)
                    .Distinct()
                    .OrderBy(language => language)
                    .ToList();

                foreach (var languageName in languageNames)
                    manifest.Localization.Languages.Add(languageName);

                var localizationKeys = document
                    .Descendants()
                    .Where(element => element.Elements().Any(child =>
                        child.Name.LocalName is "cs" or "en" or "de"))
                    .Select(element => element.Name.LocalName)
                    .Where(key => !string.IsNullOrWhiteSpace(key))
                    .Distinct()
                    .OrderBy(key => key)
                    .ToList();

                foreach (var key in localizationKeys)
                    manifest.Localization.Keys.Add(key);
            }
            catch (Exception exception)
            {
                manifest.GenerationWarnings.Add($"Localization file could not be read: {exception.Message}");
            }
        }

        /// <summary>
        /// Populates input contract metadata from JsonProperty attributes.
        /// </summary>
        /// <param name="manifest">Manifest to populate.</param>
        private static void PopulateInputContract(BrainInDeploymentManifest manifest)
        {
            var customInputParametersType = TryGetCustomInputParametersType(manifest);

            if (customInputParametersType == null)
                return;

            manifest.InputContract.BaseInputParametersIncluded = false;
            manifest.InputContract.CustomInputParametersType = customInputParametersType.FullName;

            var instance = TryCreateInstance(customInputParametersType);

            foreach (var parameter in GetInputParameters(customInputParametersType, instance))
            {
                if (manifest.InputContract.Parameters.Any(existing => existing.Key == parameter.Key))
                {
                    manifest.GenerationWarnings.Add(
                        $"Duplicate input parameter key '{parameter.Key}' was detected in '{customInputParametersType.FullName}'."
                    );

                    continue;
                }

                manifest.InputContract.Parameters.Add(parameter);
            }
        }

        /// <summary>
        /// Populates output contract metadata from ExpectedCustomDataKey attributes.
        /// </summary>
        /// <param name="manifest">Manifest to populate.</param>
        private static void PopulateOutputContract(BrainInDeploymentManifest manifest)
        {
            var components = GetComponentsInOpenEditorContexts()
                .Where(component => component != null)
                .ToList();

            var outputParameters = components
                .SelectMany(component => GetExpectedOutputParameters(component.GetType()))
                .GroupBy(parameter => parameter.Key)
                .Select(group => group.First())
                .OrderBy(parameter => parameter.Key)
                .ToList();

            foreach (var outputParameter in outputParameters)
                manifest.OutputContract.RoundCustomData.Add(outputParameter);

            if (outputParameters.Count == 0)
            {
                manifest.GenerationWarnings.Add(
                    "No ExpectedCustomDataKey attributes were found in currently open editor contexts."
                );
            }
        }

        /// <summary>
        /// Gets input parameters from a custom input parameter type.
        /// </summary>
        /// <param name="type">Input parameter type.</param>
        /// <param name="instance">Optional input parameter instance used for default values.</param>
        /// <returns>Input parameter manifest data.</returns>
        private static IEnumerable<InputParameterManifestData> GetInputParameters(Type type, object instance)
        {
            const BindingFlags flags =
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic;

            foreach (var field in type.GetFields(flags))
            {
                var attribute = field.GetCustomAttribute<JsonPropertyAttribute>();

                if (attribute == null)
                    continue;

                var key = ResolveJsonPropertyKey(attribute, field.Name);
                var dataType = MapInputBrainInDataType(field.FieldType);
                var defaultValue = instance == null ? "" : FormatValue(field.GetValue(instance));

                yield return new InputParameterManifestData
                {
                    Key = key,
                    BrainInDataType = dataType.Name,
                    BrainInDataTypeId = dataType.Id,
                    DefaultValue = defaultValue,
                    Source = "JsonProperty",
                    SourceMember = field.Name,
                    DotNetType = GetReadableTypeName(field.FieldType)
                };
            }

            foreach (var property in type.GetProperties(flags))
            {
                if (property.GetIndexParameters().Length != 0)
                    continue;

                var attribute = property.GetCustomAttribute<JsonPropertyAttribute>();

                if (attribute == null)
                    continue;

                var key = ResolveJsonPropertyKey(attribute, property.Name);
                var dataType = MapInputBrainInDataType(property.PropertyType);
                var defaultValue = "";

                if (instance != null && property.GetMethod != null)
                {
                    try
                    {
                        defaultValue = FormatValue(property.GetValue(instance));
                    }
                    catch
                    {
                        defaultValue = "";
                    }
                }

                yield return new InputParameterManifestData
                {
                    Key = key,
                    BrainInDataType = dataType.Name,
                    BrainInDataTypeId = dataType.Id,
                    DefaultValue = defaultValue,
                    Source = "JsonProperty",
                    SourceMember = property.Name,
                    DotNetType = GetReadableTypeName(property.PropertyType)
                };
            }
        }

        /// <summary>
        /// Gets output parameters from ExpectedCustomDataKey attributes.
        /// </summary>
        /// <param name="type">Component type.</param>
        /// <returns>Output parameter manifest data.</returns>
        private static IEnumerable<OutputParameterManifestData> GetExpectedOutputParameters(Type type)
        {
            foreach (var field in GetFieldsInHierarchy(type))
            {
                foreach (var attribute in field
                             .GetCustomAttributes(typeof(ExpectedCustomDataKeyAttribute), true)
                             .Cast<ExpectedCustomDataKeyAttribute>())
                {
                    var key = ResolveExpectedCustomDataKey(attribute, field.Name);

                    if (string.IsNullOrWhiteSpace(key))
                        continue;

                    var dataType = MapOutputBrainInDataType(field.FieldType);

                    yield return new OutputParameterManifestData
                    {
                        Key = key,
                        BrainInDataType = dataType.Name,
                        BrainInDataTypeId = dataType.Id,
                        Source = "ExpectedCustomDataKeyAttribute",
                        SourceMember = field.Name,
                        DotNetType = GetReadableTypeName(field.FieldType)
                    };
                }
            }

            foreach (var property in GetPropertiesInHierarchy(type))
            {
                foreach (var attribute in property
                             .GetCustomAttributes(typeof(ExpectedCustomDataKeyAttribute), true)
                             .Cast<ExpectedCustomDataKeyAttribute>())
                {
                    var key = ResolveExpectedCustomDataKey(attribute, property.Name);

                    if (string.IsNullOrWhiteSpace(key))
                        continue;

                    var dataType = MapOutputBrainInDataType(property.PropertyType);

                    yield return new OutputParameterManifestData
                    {
                        Key = key,
                        BrainInDataType = dataType.Name,
                        BrainInDataTypeId = dataType.Id,
                        Source = "ExpectedCustomDataKeyAttribute",
                        SourceMember = property.Name,
                        DotNetType = GetReadableTypeName(property.PropertyType)
                    };
                }
            }
        }

        /// <summary>
        /// Tries to get the custom input parameter type from BrainInGameSettings.
        /// </summary>
        /// <param name="manifest">Manifest used for warning collection.</param>
        /// <returns>Custom input parameter type or null.</returns>
        private static Type TryGetCustomInputParametersType(BrainInDeploymentManifest manifest)
        {
            var components = GetComponentsInOpenEditorContexts()
                .Where(component => component != null)
                .ToList();

            foreach (var gameSettings in components.Where(component =>
                         component.GetType().Name == BrainInGameSettingsTypeName))
            {
                var serializedGameSettings = new SerializedObject(gameSettings);
                var factoryProperty = serializedGameSettings.FindProperty(CustomInputParametersFactoryPropertyName);

                if (factoryProperty == null ||
                    factoryProperty.objectReferenceValue == null)
                {
                    continue;
                }

                var factory = factoryProperty.objectReferenceValue;
                var type = TryInvokeGetCreatedType(factory);

                if (type != null)
                    return type;

                manifest.GenerationWarnings.Add(
                    $"Custom input parameters factory '{factory.GetType().Name}' did not return a valid type from GetCreatedType()."
                );
            }

            manifest.GenerationWarnings.Add("Custom input parameters factory was not found in open editor contexts.");
            return null;
        }

        /// <summary>
        /// Tries to invoke GetCreatedType on a factory object.
        /// </summary>
        /// <param name="factory">Factory object.</param>
        /// <returns>Created type or null.</returns>
        private static Type TryInvokeGetCreatedType(UnityEngine.Object factory)
        {
            if (factory == null)
                return null;

            var method = factory.GetType().GetMethod(
                "GetCreatedType",
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic
            );

            if (method == null)
                return null;

            try
            {
                return method.Invoke(factory, Array.Empty<object>()) as Type;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Gets all components from loaded scenes, the currently open prefab stage,
        /// and loaded editor objects as a robust fallback.
        /// </summary>
        /// <returns>Components in open editor contexts.</returns>
        private static IEnumerable<Component> GetComponentsInOpenEditorContexts()
        {
            var components = new List<Component>();

            for (var sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                var scene = SceneManager.GetSceneAt(sceneIndex);

                if (!scene.IsValid() || !scene.isLoaded)
                    continue;

                foreach (var rootGameObject in scene.GetRootGameObjects())
                {
                    rootGameObject.GetComponentsInChildren(true, components);
                }
            }

            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();

            if (prefabStage?.prefabContentsRoot != null)
            {
                prefabStage.prefabContentsRoot.GetComponentsInChildren(true, components);
            }

            foreach (var component in Resources.FindObjectsOfTypeAll<Component>())
            {
                if (component == null)
                    continue;

                if (EditorUtility.IsPersistent(component))
                    continue;

                if (component.gameObject == null)
                    continue;

                if (component.hideFlags == HideFlags.NotEditable ||
                    component.hideFlags == HideFlags.HideAndDontSave)
                {
                    continue;
                }

                var scene = component.gameObject.scene;

                if (scene.IsValid() && scene.isLoaded)
                {
                    components.Add(component);
                    continue;
                }

                if (prefabStage?.prefabContentsRoot != null &&
                    component.transform.IsChildOf(prefabStage.prefabContentsRoot.transform))
                {
                    components.Add(component);
                }
            }

            return DistinctComponents(components);
        }

        /// <summary>
        /// Gets fields declared in a type hierarchy.
        /// </summary>
        /// <param name="type">Starting type.</param>
        /// <returns>Fields in the type hierarchy.</returns>
        private static IEnumerable<FieldInfo> GetFieldsInHierarchy(Type type)
        {
            while (type != null && type != typeof(MonoBehaviour))
            {
                foreach (var field in type.GetFields(
                             BindingFlags.Instance |
                             BindingFlags.Public |
                             BindingFlags.NonPublic |
                             BindingFlags.DeclaredOnly))
                {
                    yield return field;
                }

                type = type.BaseType;
            }
        }

        /// <summary>
        /// Gets properties declared in a type hierarchy.
        /// </summary>
        /// <param name="type">Starting type.</param>
        /// <returns>Properties in the type hierarchy.</returns>
        private static IEnumerable<PropertyInfo> GetPropertiesInHierarchy(Type type)
        {
            while (type != null && type != typeof(MonoBehaviour))
            {
                foreach (var property in type.GetProperties(
                             BindingFlags.Instance |
                             BindingFlags.Public |
                             BindingFlags.NonPublic |
                             BindingFlags.DeclaredOnly))
                {
                    if (property.GetIndexParameters().Length == 0)
                        yield return property;
                }

                type = type.BaseType;
            }
        }

        /// <summary>
        /// Resolves JsonProperty key.
        /// </summary>
        /// <param name="attribute">JsonProperty attribute.</param>
        /// <param name="memberName">Member name.</param>
        /// <returns>Resolved JSON key.</returns>
        private static string ResolveJsonPropertyKey(JsonPropertyAttribute attribute, string memberName)
        {
            return string.IsNullOrWhiteSpace(attribute.PropertyName)
                ? NormalizeMemberName(memberName)
                : attribute.PropertyName.Trim();
        }

        /// <summary>
        /// Resolves ExpectedCustomDataKey key.
        /// </summary>
        /// <param name="attribute">Expected customData key attribute.</param>
        /// <param name="memberName">Member name.</param>
        /// <returns>Resolved customData key.</returns>
        private static string ResolveExpectedCustomDataKey(ExpectedCustomDataKeyAttribute attribute, string memberName)
        {
            return string.IsNullOrWhiteSpace(attribute.Key)
                ? NormalizeMemberName(memberName)
                : attribute.Key.Trim();
        }

        /// <summary>
        /// Normalizes a member name into a contract key.
        /// </summary>
        /// <param name="memberName">Field or property name.</param>
        /// <returns>Normalized key.</returns>
        private static string NormalizeMemberName(string memberName)
        {
            if (string.IsNullOrWhiteSpace(memberName))
                return string.Empty;

            var normalized = memberName.Trim();

            if (normalized.StartsWith("m_", StringComparison.Ordinal))
                normalized = normalized.Substring(2);

            while (normalized.StartsWith("_", StringComparison.Ordinal))
                normalized = normalized.Substring(1);

            if (string.IsNullOrWhiteSpace(normalized))
                return string.Empty;

            return char.ToLowerInvariant(normalized[0]) + normalized.Substring(1);
        }

        /// <summary>
        /// Maps a .NET type to a BrainIn input data type.
        /// </summary>
        /// <param name="type">.NET type.</param>
        /// <returns>BrainIn data type.</returns>
        private static BrainInDataTypeMapping MapInputBrainInDataType(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;

            if (type == typeof(bool))
                return new BrainInDataTypeMapping("Boolean", 3);

            if (IsNumericType(type))
                return new BrainInDataTypeMapping("Number", 2);

            if (typeof(AudioClip).IsAssignableFrom(type))
                return new BrainInDataTypeMapping("Sound", 7);

            if (typeof(Texture).IsAssignableFrom(type) ||
                typeof(Sprite).IsAssignableFrom(type))
            {
                return new BrainInDataTypeMapping("Image", 6);
            }

            return new BrainInDataTypeMapping("Text", 1);
        }

        /// <summary>
        /// Maps a .NET type to a BrainIn output data type.
        /// </summary>
        /// <param name="type">.NET type.</param>
        /// <returns>BrainIn data type.</returns>
        private static BrainInDataTypeMapping MapOutputBrainInDataType(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;

            if (type == typeof(bool))
                return new BrainInDataTypeMapping("Boolean", 3);

            if (IsNumericType(type))
                return new BrainInDataTypeMapping("Number", 2);

            if (typeof(Texture).IsAssignableFrom(type) ||
                typeof(Sprite).IsAssignableFrom(type))
            {
                return new BrainInDataTypeMapping("Image", 5);
            }

            return new BrainInDataTypeMapping("Text", 1);
        }

        /// <summary>
        /// Determines whether a type is numeric.
        /// </summary>
        /// <param name="type">Type to inspect.</param>
        /// <returns>True if the type is numeric; otherwise false.</returns>
        private static bool IsNumericType(Type type)
        {
            return type == typeof(byte) ||
                   type == typeof(sbyte) ||
                   type == typeof(short) ||
                   type == typeof(ushort) ||
                   type == typeof(int) ||
                   type == typeof(uint) ||
                   type == typeof(long) ||
                   type == typeof(ulong) ||
                   type == typeof(float) ||
                   type == typeof(double) ||
                   type == typeof(decimal);
        }

        /// <summary>
        /// Tries to create a parameterless instance of a type.
        /// </summary>
        /// <param name="type">Type to instantiate.</param>
        /// <returns>Created instance or null.</returns>
        private static object TryCreateInstance(Type type)
        {
            try
            {
                return Activator.CreateInstance(type);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Formats a value for manifest output.
        /// </summary>
        /// <param name="value">Value to format.</param>
        /// <returns>Formatted value.</returns>
        private static string FormatValue(object value)
        {
            if (value == null)
                return "";

            switch (value)
            {
                case bool boolValue:
                    return boolValue ? "true" : "false";

                case float floatValue:
                    return floatValue.ToString("0.###", CultureInfo.InvariantCulture);

                case double doubleValue:
                    return doubleValue.ToString("0.###", CultureInfo.InvariantCulture);

                case decimal decimalValue:
                    return decimalValue.ToString(CultureInfo.InvariantCulture);

                case IFormattable formattable:
                    return formattable.ToString(null, CultureInfo.InvariantCulture);

                case string stringValue:
                    return stringValue;

                default:
                    return JsonConvert.SerializeObject(value);
            }
        }

        /// <summary>
        /// Gets a readable type name.
        /// </summary>
        /// <param name="type">Type.</param>
        /// <returns>Readable type name.</returns>
        private static string GetReadableTypeName(Type type)
        {
            return type == null ? "" : type.FullName ?? type.Name;
        }

        /// <summary>
        /// Gets Unity project folder name.
        /// </summary>
        /// <returns>Project folder name.</returns>
        private static string GetUnityProjectName()
        {
            var projectPath = Path.GetDirectoryName(Application.dataPath);

            return string.IsNullOrWhiteSpace(projectPath)
                ? ""
                : Path.GetFileName(projectPath);
        }

        /// <summary>
        /// Gets package version by package name.
        /// </summary>
        /// <param name="packageName">Package name.</param>
        /// <returns>Package version or empty string.</returns>
        private static string GetPackageVersion(string packageName)
        {
            var packageJsonPaths = Directory
                .GetFiles("Packages", "package.json", SearchOption.AllDirectories)
                .Where(path => path.Replace('\\', '/').Contains($"/{packageName}/") ||
                               path.Replace('\\', '/').EndsWith($"{packageName}/package.json"))
                .ToList();

            foreach (var path in packageJsonPaths)
            {
                try
                {
                    var json = JObject.Parse(File.ReadAllText(path));
                    return json["version"]?.ToString() ?? "";
                }
                catch
                {
                    // Ignore malformed package.json files.
                }
            }

            return "";
        }

        /// <summary>
        /// Gets package version for an assembly.
        /// </summary>
        /// <param name="assembly">Assembly.</param>
        /// <returns>Package version or empty string.</returns>
        private static string GetPackageVersion(Assembly assembly)
        {
            try
            {
                var packageInfo = PackageManagerPackageInfo.FindForAssembly(assembly);
                return packageInfo?.version ?? "";
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// Returns distinct components by Unity instance ID.
        /// </summary>
        /// <param name="components">Components to deduplicate.</param>
        /// <returns>Distinct components.</returns>
        private static IEnumerable<Component> DistinctComponents(IEnumerable<Component> components)
        {
            var seenInstanceIds = new HashSet<int>();

            foreach (var component in components)
            {
                if (component == null)
                    continue;

                if (seenInstanceIds.Add(component.GetInstanceID()))
                    yield return component;
            }
        }

        /// <summary>
        /// Represents a BrainIn data type mapping.
        /// </summary>
        private readonly struct BrainInDataTypeMapping
        {
            /// <summary>
            /// Creates a BrainIn data type mapping.
            /// </summary>
            /// <param name="name">Readable data type name.</param>
            /// <param name="id">BrainIn form data type ID.</param>
            public BrainInDataTypeMapping(string name, int id)
            {
                Name = name;
                Id = id;
            }

            /// <summary>
            /// Gets readable data type name.
            /// </summary>
            public string Name { get; }

            /// <summary>
            /// Gets BrainIn form data type ID.
            /// </summary>
            public int Id { get; }
        }
    }
}