using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BrainIn.DevTools.Editor.Validation.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BrainIn.DevTools.Editor.Validation.Rules
{
    /// <summary>
    /// Validates whether the current Unity project is correctly integrated with the BrainIn runtime plugin.
    /// BrainIn runtime plugin was created by M. Jakubašek 
    /// </summary>
    public sealed class BrainInIntegrationRule : IValidationRule
    {
        private const string BrainInPackageName = "cz.zcu.kiv.fav";
        private const string BrainInRuntimeAssemblyName = "BrainInTemplate.Runtime";
        private const string BrainInComponentFullName = "BrainInTemplate.Runtime.Code.BrainIn";

        private const string BrainInLoggerSettingsName = "BrainInLoggerSettings";
        private const string BrainInServicesSettingsName = "BrainInServicesSettings";
        private const string BrainInGameSettingsName = "BrainInGameSettings";
        private const string BrainInControllersSettingsName = "BrainInControllersSettings";
        private const string BrainInBackgroundSettingsName = "BrainInBackgroundSettings";
        private const string BrainInCursorSettingsName = "BrainInCursorSettings";

        /// <summary>
        /// Gets the display name of the validation rule.
        /// </summary>
        public string Name => "BrainIn plugin integration validator";

        /// <summary>
        /// Validates BrainIn runtime plugin integration in enabled build scenes.
        /// </summary>
        /// <param name="context">Validation context containing project paths and shared validation data.</param>
        /// <returns>Validation results describing detected BrainIn integration issues.</returns>
        public IEnumerable<ValidationResult> Validate(ValidationContext context)
        {
            var results = new List<ValidationResult>();

            ValidatePackageInstallation(context, results);

            var enabledScenePaths = GetEnabledBuildScenePaths();

            if (enabledScenePaths.Count == 0)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Error,
                    Name,
                    "No enabled build scenes were found. BrainIn integration can only be validated in enabled build scenes."
                ));

                return results;
            }

            var brainInInstanceCount = 0;

            foreach (var scenePath in enabledScenePaths)
            {
                brainInInstanceCount += ValidateScene(scenePath, context, results);
            }

            if (brainInInstanceCount == 0)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Error,
                    Name,
                    "No BrainIn root object was found in enabled build scenes. Add the BrainIn root prefab through the BrainIn menu before building."
                ));
            }

            if (brainInInstanceCount > 1)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Error,
                    Name,
                    $"Expected exactly one BrainIn root object in enabled build scenes, but found {brainInInstanceCount}. Multiple BrainIn runtimes can break initialization."
                ));
            }

            if (results.Count == 0)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Info,
                    Name,
                    "BrainIn runtime plugin integration looks valid."
                ));
            }

            return results;
        }

        /// <summary>
        /// Validates whether the BrainIn package and runtime assembly are available.
        /// </summary>
        /// <param name="context">Validation context containing the Unity project path.</param>
        /// <param name="results">Validation results collection to append detected issues to.</param>
        private void ValidatePackageInstallation(ValidationContext context, List<ValidationResult> results)
        {
            var packageDeclared = IsPackageDeclaredInManifest(context.ProjectPath);
            var assemblyLoaded = IsRuntimeAssemblyLoaded();

            switch (packageDeclared)
            {
                case false when !assemblyLoaded:
                    results.Add(new ValidationResult(
                        ValidationSeverity.Error,
                        Name,
                        $"BrainIn runtime package '{BrainInPackageName}' was not found and assembly '{BrainInRuntimeAssemblyName}' is not loaded. Install the BrainIn template plugin first."
                    ));

                    return;
                case false when assemblyLoaded:
                    results.Add(new ValidationResult(
                        ValidationSeverity.Warning,
                        Name,
                        $"BrainIn runtime assembly '{BrainInRuntimeAssemblyName}' is loaded, but package '{BrainInPackageName}' was not found in Packages/manifest.json. The plugin may be embedded or referenced in a non-standard way."
                    ));
                    break;
                case true when !assemblyLoaded:
                    results.Add(new ValidationResult(
                        ValidationSeverity.Error,
                        Name,
                        $"BrainIn package '{BrainInPackageName}' is declared in Packages/manifest.json, but runtime assembly '{BrainInRuntimeAssemblyName}' is not loaded. Check Unity compile errors and package installation."
                    ));
                    break;
            }
        }

        /// <summary>
        /// Validates one enabled build scene.
        /// </summary>
        /// <param name="scenePath">Unity asset path of the scene to validate.</param>
        /// <param name="context">Validation context containing project paths.</param>
        /// <param name="results">Validation results collection to append detected issues to.</param>
        /// <returns>Number of BrainIn root objects found in the scene.</returns>
        private int ValidateScene(
            string scenePath,
            ValidationContext context,
            List<ValidationResult> results)
        {
            var sceneWasLoaded = IsSceneLoaded(scenePath);
            Scene scene;

            try
            {
                scene = sceneWasLoaded
                    ? SceneManager.GetSceneByPath(scenePath)
                    : EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            }
            catch (Exception exception)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Error,
                    Name,
                    $"Could not open scene for BrainIn integration validation: {exception.Message}",
                    scenePath
                ));

                return 0;
            }

            try
            {
                var brainInComponents = FindBrainInComponents(scene).ToList();

                foreach (var brainInComponent in brainInComponents)
                {
                    ValidateBrainInRoot(brainInComponent, context, results, scenePath);
                }

                return brainInComponents.Count;
            }
            finally
            {
                if (!sceneWasLoaded && scene.IsValid())
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        /// <summary>
        /// Validates one BrainIn root component and its required settings.
        /// </summary>
        /// <param name="brainInComponent">BrainIn root component to validate.</param>
        /// <param name="context">Validation context containing project paths.</param>
        /// <param name="results">Validation results collection to append detected issues to.</param>
        /// <param name="scenePath">Unity asset path of the scene containing the BrainIn component.</param>
        private void ValidateBrainInRoot(
            Component brainInComponent,
            ValidationContext context,
            List<ValidationResult> results,
            string scenePath)
        {
            if (!brainInComponent.gameObject.activeInHierarchy)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Error,
                    Name,
                    $"BrainIn root object '{GetGameObjectPath(brainInComponent.gameObject)}' is inactive. The BrainIn runtime will not initialize.",
                    scenePath
                ));
            }

            if (brainInComponent is Behaviour behaviour && !behaviour.enabled)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Error,
                    Name,
                    $"BrainIn component on '{GetGameObjectPath(brainInComponent.gameObject)}' is disabled. The BrainIn runtime will not initialize.",
                    scenePath
                ));
            }

            ValidateRequiredSettingsComponents(brainInComponent, results, scenePath);
            ValidateBrainInSerializedReferences(brainInComponent, context, results, scenePath);
        }

        /// <summary>
        /// Validates whether the BrainIn root object contains all required settings components.
        /// </summary>
        /// <param name="brainInComponent">BrainIn root component to validate.</param>
        /// <param name="results">Validation results collection to append detected issues to.</param>
        /// <param name="scenePath">Unity asset path of the scene containing the BrainIn component.</param>
        private void ValidateRequiredSettingsComponents(
            Component brainInComponent,
            List<ValidationResult> results,
            string scenePath)
        {
            var gameObject = brainInComponent.gameObject;

            ValidateRequiredComponent(gameObject, BrainInLoggerSettingsName, results, scenePath);
            ValidateRequiredComponent(gameObject, BrainInServicesSettingsName, results, scenePath);
            ValidateRequiredComponent(gameObject, BrainInGameSettingsName, results, scenePath);
            ValidateRequiredComponent(gameObject, BrainInControllersSettingsName, results, scenePath);
            ValidateRequiredComponent(gameObject, BrainInBackgroundSettingsName, results, scenePath);
            ValidateRequiredComponent(gameObject, BrainInCursorSettingsName, results, scenePath);
        }

        /// <summary>
        /// Validates serialized references stored directly on the BrainIn root component.
        /// </summary>
        /// <param name="brainInComponent">BrainIn root component to validate.</param>
        /// <param name="context">Validation context containing project paths.</param>
        /// <param name="results">Validation results collection to append detected issues to.</param>
        /// <param name="scenePath">Unity asset path of the scene containing the BrainIn component.</param>
        private void ValidateBrainInSerializedReferences(
            Component brainInComponent,
            ValidationContext context,
            List<ValidationResult> results,
            string scenePath)
        {
            ValidateRequiredObjectReference(brainInComponent, "loggerSettings", "logger settings", results, scenePath);
            ValidateRequiredObjectReference(brainInComponent, "servicesSettings", "services settings", results,
                scenePath);
            ValidateRequiredObjectReference(brainInComponent, "gameSettings", "game settings", results, scenePath);
            ValidateRequiredObjectReference(brainInComponent, "controllersSettings", "controllers settings", results,
                scenePath);
            ValidateRequiredObjectReference(brainInComponent, "backgroundSettings", "background settings", results,
                scenePath);
            ValidateRequiredObjectReference(brainInComponent, "cursorSettings", "cursor settings", results, scenePath);

            ValidateLocalesTablePath(brainInComponent, context, results, scenePath);

            var gameObject = brainInComponent.gameObject;

            var loggerSettings = GetComponentByTypeName(gameObject, BrainInLoggerSettingsName);
            var gameSettings = GetComponentByTypeName(gameObject, BrainInGameSettingsName);
            var servicesSettings = GetComponentByTypeName(gameObject, BrainInServicesSettingsName);
            var controllersSettings = GetComponentByTypeName(gameObject, BrainInControllersSettingsName);
            var backgroundSettings = GetComponentByTypeName(gameObject, BrainInBackgroundSettingsName);

            if (loggerSettings)
            {
                ValidateLoggerSettings(loggerSettings, results, scenePath);
            }

            if (gameSettings)
            {
                ValidateGameSettings(gameSettings, results, scenePath);
            }

            if (servicesSettings)
            {
                ValidateServicesSettings(servicesSettings, results, scenePath);
            }

            if (controllersSettings)
            {
                ValidateControllersSettings(controllersSettings, results, scenePath);
            }

            if (backgroundSettings)
            {
                ValidateBackgroundSettings(backgroundSettings, results, scenePath);
            }
        }

        /// <summary>
        /// Validates the BrainIn logger settings component.
        /// </summary>
        /// <param name="component">Logger settings component to validate.</param>
        /// <param name="results">Validation results collection to append detected issues to.</param>
        /// <param name="scenePath">Unity asset path of the scene containing the component.</param>
        private void ValidateLoggerSettings(Component component, List<ValidationResult> results, string scenePath)
        {
            ValidateRequiredObjectReference(component, "loggerFactory", "logger factory", results, scenePath);
        }

        /// <summary>
        /// Validates the BrainIn game settings component.
        /// </summary>
        /// <param name="component">Game settings component to validate.</param>
        /// <param name="results">Validation results collection to append detected issues to.</param>
        /// <param name="scenePath">Unity asset path of the scene containing the component.</param>
        private void ValidateGameSettings(Component component, List<ValidationResult> results, string scenePath)
        {
            ValidateRequiredObjectReference(component, "customInputParametersFactory",
                "custom input parameters factory", results, scenePath);
            ValidateRequiredObjectReference(component, "localizationFactory", "localization factory", results,
                scenePath);
            ValidateRequiredObjectReference(component, "customParametersValidatorServiceFactory",
                "custom parameters validator factory", results, scenePath);
            ValidateRequiredObjectReference(component, "roundDataGeneratorFactory", "round data generator factory",
                results, scenePath);
            ValidateRequiredObjectReference(component, "customGameController", "custom game controller", results,
                scenePath);

            WarnIfReferenceUsesStub(component, "customInputParametersFactory", "custom input parameters factory",
                results, scenePath);
            WarnIfReferenceUsesStub(component, "localizationFactory", "localization factory", results, scenePath);
            WarnIfReferenceUsesStub(component, "customParametersValidatorServiceFactory",
                "custom parameters validator factory", results, scenePath);
            WarnIfReferenceUsesStub(component, "roundDataGeneratorFactory", "round data generator factory", results,
                scenePath);
            WarnIfReferenceUsesStub(component, "customGameController", "custom game controller", results, scenePath);
        }

        /// <summary>
        /// Validates the BrainIn services settings component.
        /// </summary>
        /// <param name="component">Services settings component to validate.</param>
        /// <param name="results">Validation results collection to append detected issues to.</param>
        /// <param name="scenePath">Unity asset path of the scene containing the component.</param>
        private void ValidateServicesSettings(Component component, List<ValidationResult> results, string scenePath)
        {
            ValidateRequiredObjectReference(component, "gameSettings", "game settings", results, scenePath);
            ValidateRequiredObjectReference(component, "serviceLocatorFactory", "service locator factory", results,
                scenePath);
            ValidateRequiredObjectReference(component, "eventAggregatorServiceFactory",
                "event aggregator service factory", results, scenePath);
            ValidateRequiredObjectReference(component, "webBridgeServiceFactory", "web bridge service factory", results,
                scenePath);
            ValidateRequiredObjectReference(component, "timeServiceFactory", "time service factory", results,
                scenePath);
            ValidateRequiredObjectReference(component, "localizationServiceFactory", "localization service factory",
                results, scenePath);
            ValidateRequiredObjectReference(component, "outputServiceFactory", "output service factory", results,
                scenePath);
            ValidateRequiredObjectReference(component, "inputParametersValidatorFactory",
                "input parameters validator factory", results, scenePath);
            ValidateRequiredObjectReference(component, "inputParametersParserFactory",
                "input parameters parser factory", results, scenePath);
            ValidateRequiredObjectReference(component, "inputServiceFactory", "input service factory", results,
                scenePath);

            ValidateObjectReferenceList(component, "additionalServiceFactories", "additional service factories",
                results, scenePath);
        }

        /// <summary>
        /// Validates the BrainIn controllers settings component.
        /// </summary>
        /// <param name="component">Controllers settings component to validate.</param>
        /// <param name="results">Validation results collection to append detected issues to.</param>
        /// <param name="scenePath">Unity asset path of the scene containing the component.</param>
        private void ValidateControllersSettings(Component component, List<ValidationResult> results, string scenePath)
        {
            ValidateRequiredObjectReference(component, "gameSettings", "game settings", results, scenePath);
            ValidateRequiredObjectReference(component, "controllerLocatorFactory", "controller locator factory",
                results, scenePath);
            ValidateRequiredObjectReference(component, "cursorController", "cursor controller", results, scenePath);
            ValidateRequiredObjectReference(component, "webBridgeController", "web bridge controller", results,
                scenePath);
            ValidateRequiredObjectReference(component, "pauseController", "pause controller", results, scenePath);
            ValidateRequiredObjectReference(component, "modalController", "modal controller", results, scenePath);
            ValidateRequiredObjectReference(component, "audioPlayerController", "audio player controller", results,
                scenePath);
            ValidateRequiredObjectReference(component, "trafficLightController", "traffic light controller", results,
                scenePath);
            ValidateRequiredObjectReference(component, "preRoundController", "pre-round controller", results,
                scenePath);
            ValidateRequiredObjectReference(component, "gameController", "game controller", results, scenePath);
            ValidateRequiredObjectReference(component, "tooltipController", "tooltip controller", results, scenePath);
            ValidateRequiredObjectReference(component, "clickController", "click controller", results, scenePath);
            ValidateRequiredObjectReference(component, "keystrokeController", "keystroke controller", results,
                scenePath);
            ValidateRequiredObjectReference(component, "debugController", "debug controller", results, scenePath);
            ValidateRequiredObjectReference(component, "gameDirector", "game director", results, scenePath);

            ValidateObjectReferenceList(component, "additionalControllerBases", "additional controller bases", results,
                scenePath);
        }

        /// <summary>
        /// Validates the BrainIn background settings component.
        /// </summary>
        /// <param name="component">Background settings component to validate.</param>
        /// <param name="results">Validation results collection to append detected issues to.</param>
        /// <param name="scenePath">Unity asset path of the scene containing the component.</param>
        private void ValidateBackgroundSettings(Component component, List<ValidationResult> results, string scenePath)
        {
            var backgroundEnabled = GetBoolProperty(component, "backgroundEnabled");

            if (backgroundEnabled)
            {
                ValidateRequiredObjectReference(component, "backgroundCanvas", "background canvas", results, scenePath);
            }
        }

        /// <summary>
        /// Validates the localization table path configured on the BrainIn component.
        /// </summary>
        /// <param name="brainInComponent">BrainIn component to validate.</param>
        /// <param name="context">Validation context containing project paths.</param>
        /// <param name="results">Validation results collection to append detected issues to.</param>
        /// <param name="scenePath">Unity asset path of the scene containing the BrainIn component.</param>
        private void ValidateLocalesTablePath(
            Component brainInComponent,
            ValidationContext context,
            List<ValidationResult> results,
            string scenePath)
        {
            var localesTablePath = GetStringProperty(brainInComponent, "LocalesTablePath");

            if (localesTablePath == null)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Warning,
                    Name,
                    "Could not find serialized field 'LocalesTablePath' on BrainIn component. The BrainIn plugin version may have changed.",
                    scenePath
                ));

                return;
            }

            if (string.IsNullOrWhiteSpace(localesTablePath))
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Error,
                    Name,
                    "BrainIn LocalesTablePath is empty. Expected value is usually 'ProgramData/Locale/locale.xml'.",
                    scenePath
                ));

                return;
            }

            var localeAssetPath = ResolveStreamingAssetsAssetPath(localesTablePath);

            if (AssetFileExists(context.ProjectPath, localeAssetPath))
                return;

            results.Add(new ValidationResult(
                ValidationSeverity.Error,
                Name,
                $"BrainIn localization file configured by LocalesTablePath was not found. Expected file: {localeAssetPath}",
                localeAssetPath
            ));
        }

        /// <summary>
        /// Validates whether a GameObject contains a required component by type name.
        /// </summary>
        /// <param name="gameObject">GameObject to inspect.</param>
        /// <param name="componentTypeName">Required component type name.</param>
        /// <param name="results">Validation results collection to append detected issues to.</param>
        /// <param name="scenePath">Unity asset path of the scene containing the GameObject.</param>
        private void ValidateRequiredComponent(
            GameObject gameObject,
            string componentTypeName,
            List<ValidationResult> results,
            string scenePath)
        {
            if (GetComponentByTypeName(gameObject, componentTypeName) != null)
                return;

            results.Add(new ValidationResult(
                ValidationSeverity.Error,
                Name,
                $"BrainIn root object '{GetGameObjectPath(gameObject)}' is missing required component '{componentTypeName}'.",
                scenePath
            ));
        }

        /// <summary>
        /// Validates whether a required serialized object reference is assigned.
        /// </summary>
        /// <param name="component">Component containing the serialized field.</param>
        /// <param name="fieldName">Serialized field name.</param>
        /// <param name="displayName">Human-readable field name.</param>
        /// <param name="results">Validation results collection to append detected issues to.</param>
        /// <param name="scenePath">Unity asset path of the scene containing the component.</param>
        private void ValidateRequiredObjectReference(
            Component component,
            string fieldName,
            string displayName,
            List<ValidationResult> results,
            string scenePath)
        {
            var property = GetSerializedProperty(component, fieldName);

            if (property == null)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Warning,
                    Name,
                    $"Could not find serialized field '{fieldName}' on component '{component.GetType().Name}'. The BrainIn plugin version may have changed.",
                    scenePath
                ));

                return;
            }

            if (property.propertyType != SerializedPropertyType.ObjectReference)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Warning,
                    Name,
                    $"Serialized field '{fieldName}' on component '{component.GetType().Name}' is not an object reference.",
                    scenePath
                ));

                return;
            }

            if (property.objectReferenceValue != null)
                return;

            results.Add(new ValidationResult(
                ValidationSeverity.Error,
                Name,
                $"Component '{component.GetType().Name}' on '{GetGameObjectPath(component.gameObject)}' has missing required reference: {displayName}.",
                scenePath
            ));
        }

        /// <summary>
        /// Reports a warning when a serialized object reference points to a stub implementation.
        /// </summary>
        /// <param name="component">Component containing the serialized field.</param>
        /// <param name="fieldName">Serialized field name.</param>
        /// <param name="displayName">Human-readable field name.</param>
        /// <param name="results">Validation results collection to append detected issues to.</param>
        /// <param name="scenePath">Unity asset path of the scene containing the component.</param>
        private void WarnIfReferenceUsesStub(
            Component component,
            string fieldName,
            string displayName,
            List<ValidationResult> results,
            string scenePath)
        {
            var reference = GetObjectReference(component, fieldName);

            if (!reference || !IsStubReference(reference))
                return;

            results.Add(new ValidationResult(
                ValidationSeverity.Warning,
                Name,
                $"Component '{component.GetType().Name}' on '{GetGameObjectPath(component.gameObject)}' still uses stub implementation for {displayName}: {reference.GetType().Name}. Replace it before final deployment.",
                scenePath
            ));
        }

        /// <summary>
        /// Validates whether an optional serialized object reference list does not contain null entries.
        /// </summary>
        /// <param name="component">Component containing the serialized list.</param>
        /// <param name="fieldName">Serialized field name.</param>
        /// <param name="displayName">Human-readable field name.</param>
        /// <param name="results">Validation results collection to append detected issues to.</param>
        /// <param name="scenePath">Unity asset path of the scene containing the component.</param>
        private void ValidateObjectReferenceList(
            Component component,
            string fieldName,
            string displayName,
            List<ValidationResult> results,
            string scenePath)
        {
            var property = GetSerializedProperty(component, fieldName);

            if (property == null)
            {
                return;
            }

            if (!property.isArray)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Warning,
                    Name,
                    $"Serialized field '{fieldName}' on component '{component.GetType().Name}' is expected to be a list.",
                    scenePath
                ));

                return;
            }

            for (var index = 0; index < property.arraySize; index++)
            {
                var element = property.GetArrayElementAtIndex(index);

                if (element.propertyType != SerializedPropertyType.ObjectReference)
                    continue;

                if (element.objectReferenceValue != null)
                    continue;

                results.Add(new ValidationResult(
                    ValidationSeverity.Warning,
                    Name,
                    $"Component '{component.GetType().Name}' on '{GetGameObjectPath(component.gameObject)}' contains an empty item in {displayName} at index {index}.",
                    scenePath
                ));
            }
        }

        /// <summary>
        /// Finds all BrainIn root components in a loaded scene.
        /// </summary>
        /// <param name="scene">Loaded scene to inspect.</param>
        /// <returns>BrainIn root components found in the scene.</returns>
        private static IEnumerable<Component> FindBrainInComponents(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                yield break;

            foreach (var rootGameObject in scene.GetRootGameObjects())
            {
                foreach (var component in rootGameObject.GetComponentsInChildren<Component>(true))
                {
                    if (!component)
                    {
                        continue;
                    }

                    if (component.GetType().FullName == BrainInComponentFullName)
                        yield return component;
                }
            }
        }

        /// <summary>
        /// Gets a component from a GameObject by its type name.
        /// </summary>
        /// <param name="gameObject">GameObject to inspect.</param>
        /// <param name="componentTypeName">Component type name to find.</param>
        /// <returns>Component if found; otherwise null.</returns>
        private static Component GetComponentByTypeName(GameObject gameObject, string componentTypeName)
        {
            return gameObject
                .GetComponents<Component>()
                .FirstOrDefault(component =>
                    component &&
                    string.Equals(component.GetType().Name, componentTypeName, StringComparison.Ordinal));
        }

        /// <summary>
        /// Gets all enabled scene paths from Unity Build Settings.
        /// </summary>
        /// <returns>Enabled build scene paths.</returns>
        private static IReadOnlyList<string> GetEnabledBuildScenePaths()
        {
            return EditorBuildSettings
                .scenes
                .Where(scene => scene != null && scene.enabled && !string.IsNullOrWhiteSpace(scene.path))
                .Select(scene => scene.path)
                .ToList();
        }

        /// <summary>
        /// Determines whether a scene is already loaded in the editor.
        /// </summary>
        /// <param name="scenePath">Unity asset path of the scene.</param>
        /// <returns>True if the scene is loaded; otherwise false.</returns>
        private static bool IsSceneLoaded(string scenePath)
        {
            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                var scene = SceneManager.GetSceneAt(index);

                if (string.Equals(scene.path, scenePath, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether the BrainIn package is declared in Packages/manifest.json.
        /// </summary>
        /// <param name="projectPath">Absolute path to the Unity project root directory.</param>
        /// <returns>True if the package is declared; otherwise false.</returns>
        private static bool IsPackageDeclaredInManifest(string projectPath)
        {
            var manifestPath = Path.Combine(projectPath, "Packages", "manifest.json");

            if (!File.Exists(manifestPath))
            {
                return false;
            }

            var manifestContent = File.ReadAllText(manifestPath);
            return manifestContent.Contains($"\"{BrainInPackageName}\"");
        }

        /// <summary>
        /// Determines whether the BrainIn runtime assembly is loaded in the current editor domain.
        /// </summary>
        /// <returns>True if the runtime assembly is loaded; otherwise false.</returns>
        private static bool IsRuntimeAssemblyLoaded()
        {
            return AppDomain
                .CurrentDomain
                .GetAssemblies()
                .Any(assembly => assembly.GetName().Name == BrainInRuntimeAssemblyName);
        }

        /// <summary>
        /// Gets a serialized property from a component by field name.
        /// </summary>
        /// <param name="component">Component containing the serialized field.</param>
        /// <param name="fieldName">Serialized field name.</param>
        /// <returns>Serialized property if found; otherwise null.</returns>
        private static SerializedProperty GetSerializedProperty(Component component, string fieldName)
        {
            var serializedObject = new SerializedObject(component);
            return serializedObject.FindProperty(fieldName);
        }

        /// <summary>
        /// Gets an object reference stored in a serialized field.
        /// </summary>
        /// <param name="component">Component containing the serialized field.</param>
        /// <param name="fieldName">Serialized field name.</param>
        /// <returns>Referenced Unity object if found; otherwise null.</returns>
        private static UnityEngine.Object GetObjectReference(Component component, string fieldName)
        {
            var property = GetSerializedProperty(component, fieldName);

            return property is not { propertyType: SerializedPropertyType.ObjectReference }
                ? null
                : property.objectReferenceValue;
        }

        /// <summary>
        /// Gets a string stored in a serialized field.
        /// </summary>
        /// <param name="component">Component containing the serialized field.</param>
        /// <param name="fieldName">Serialized field name.</param>
        /// <returns>String value if found; otherwise null.</returns>
        private static string GetStringProperty(Component component, string fieldName)
        {
            var property = GetSerializedProperty(component, fieldName);

            return property is not { propertyType: SerializedPropertyType.String } ? null : property.stringValue;
        }

        /// <summary>
        /// Gets a boolean stored in a serialized field.
        /// </summary>
        /// <param name="component">Component containing the serialized field.</param>
        /// <param name="fieldName">Serialized field name.</param>
        /// <returns>Boolean value if found; otherwise false.</returns>
        private static bool GetBoolProperty(Component component, string fieldName)
        {
            var property = GetSerializedProperty(component, fieldName);

            return property is { propertyType: SerializedPropertyType.Boolean, boolValue: true };
        }

        /// <summary>
        /// Determines whether a Unity object reference points to a BrainIn stub implementation.
        /// </summary>
        /// <param name="reference">Unity object reference to inspect.</param>
        /// <returns>True if the reference appears to be a stub implementation; otherwise false.</returns>
        private static bool IsStubReference(UnityEngine.Object reference)
        {
            var type = reference.GetType();
            var fullName = type.FullName ?? string.Empty;

            return type.Name.StartsWith("Stub", StringComparison.Ordinal) ||
                   fullName.Contains(".Stub.", StringComparison.Ordinal);
        }

        /// <summary>
        /// Resolves a BrainIn localization path to a Unity asset path under StreamingAssets.
        /// </summary>
        /// <param name="localesTablePath">BrainIn LocalesTablePath value.</param>
        /// <returns>Unity asset path to the localization file.</returns>
        private static string ResolveStreamingAssetsAssetPath(string localesTablePath)
        {
            var normalizedPath = localesTablePath.Replace("\\", "/").TrimStart('/');

            if (normalizedPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                return normalizedPath;

            return normalizedPath.StartsWith("StreamingAssets/", StringComparison.OrdinalIgnoreCase)
                ? $"Assets/{normalizedPath}"
                : $"Assets/StreamingAssets/{normalizedPath}";
        }

        /// <summary>
        /// Determines whether a Unity asset path exists on disk.
        /// </summary>
        /// <param name="projectPath">Absolute path to the Unity project root directory.</param>
        /// <param name="assetPath">Unity asset path to inspect.</param>
        /// <returns>True if the asset exists on disk; otherwise false.</returns>
        private static bool AssetFileExists(string projectPath, string assetPath)
        {
            var fullPath = Path.Combine(
                projectPath,
                assetPath.Replace("/", Path.DirectorySeparatorChar.ToString())
            );

            return File.Exists(fullPath);
        }

        /// <summary>
        /// Creates a readable hierarchy path for a GameObject.
        /// </summary>
        /// <param name="gameObject">GameObject to format.</param>
        /// <returns>Readable hierarchy path.</returns>
        private static string GetGameObjectPath(GameObject gameObject)
        {
            var names = new Stack<string>();
            var current = gameObject.transform;

            while (current)
            {
                names.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", names);
        }
    }
}