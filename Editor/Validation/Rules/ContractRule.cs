using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using BrainIn.DevTools.Editor.Validation.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BrainIn.DevTools.Editor.Validation.Rules
{
    /// <summary>
    /// Validates the game-specific BrainIn contract declared by the current Unity task.
    /// </summary>
    public sealed class ContractRule : IValidationRule
    {
        private const string BrainInComponentFullName = "BrainInTemplate.Runtime.Code.BrainIn";
        private const string BrainInRuntimeAssemblyName = "BrainInTemplate.Runtime";

        private const string BrainInGameSettingsName = "BrainInGameSettings";

        private const string InputParametersFactoryFieldName = "customInputParametersFactory";
        private const string LocalizationFactoryFieldName = "localizationFactory";
        private const string CustomValidatorFactoryFieldName = "customParametersValidatorServiceFactory";
        private const string RoundDataGeneratorFactoryFieldName = "roundDataGeneratorFactory";
        private const string CustomGameControllerFieldName = "customGameController";
        private const string LocalesTablePathFieldName = "LocalesTablePath";

        private const string JsonPropertyAttributeFullName = "Newtonsoft.Json.JsonPropertyAttribute";

        private const string CustomGameControllerFullName =
            "BrainInTemplate.Runtime.Code.Unity.Controller.Game.CustomGameController";

        private static readonly string[] RequiredBaseInputParameterNames =
        {
            "lang",
            "webState",
            "maxTime",
            "rounds",
            "skip",
            "skipAll",
            "trafficLightTime",
            "showRoundSolution",
            "solutionTime",
            "resultsTime",
            "seed",
            "debug"
        };

        /// <summary>
        /// Gets the display name of the validation rule.
        /// </summary>
        public string Name => "BrainIn contract validator";

        /// <summary>
        /// Validates the BrainIn game contract in enabled build scenes.
        /// </summary>
        /// <param name="context">Validation context containing project paths and shared validation data.</param>
        /// <returns>Validation results describing detected contract issues.</returns>
        public IEnumerable<ValidationResult> Validate(ValidationContext context)
        {
            var results = new List<ValidationResult>();

            if (!IsRuntimeAssemblyLoaded())
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Error,
                    Name,
                    $"BrainIn runtime assembly '{BrainInRuntimeAssemblyName}' is not loaded. Contract validation cannot be performed."
                ));

                return results;
            }

            var enabledScenePaths = GetEnabledBuildScenePaths();

            if (enabledScenePaths.Count == 0)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Error,
                    Name,
                    "No enabled build scenes were found. BrainIn contract can only be validated in enabled build scenes."
                ));

                return results;
            }

            var validatedContracts = 0;

            foreach (var scenePath in enabledScenePaths)
            {
                validatedContracts += ValidateScene(scenePath, context, results);
            }

            if (validatedContracts == 0)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Error,
                    Name,
                    "No BrainIn contract was found in enabled build scenes. Add and configure the BrainIn root object first."
                ));
            }

            if (results.Count == 0)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Info,
                    Name,
                    "BrainIn game contract looks valid."
                ));
            }

            return results;
        }

        /// <summary>
        /// Validates one enabled build scene.
        /// </summary>
        /// <param name="scenePath">Unity asset path of the scene to validate.</param>
        /// <param name="context">Validation context containing project paths.</param>
        /// <param name="results">Validation results collection to append detected issues to.</param>
        /// <returns>Number of validated BrainIn contracts in the scene.</returns>
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
                    $"Could not open scene for BrainIn contract validation: {exception.Message}",
                    scenePath
                ));

                return 0;
            }

            try
            {
                var brainInComponents = FindBrainInComponents(scene).ToList();

                foreach (var brainInComponent in brainInComponents)
                {
                    ValidateBrainInContract(brainInComponent, context, results, scenePath);
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
        /// Validates the contract declared by one BrainIn root object.
        /// </summary>
        /// <param name="brainInComponent">BrainIn root component to validate.</param>
        /// <param name="context">Validation context containing project paths.</param>
        /// <param name="results">Validation results collection to append detected issues to.</param>
        /// <param name="scenePath">Unity asset path of the scene containing the BrainIn component.</param>
        private void ValidateBrainInContract(
            Component brainInComponent,
            ValidationContext context,
            List<ValidationResult> results,
            string scenePath)
        {
            var gameSettings = GetComponentByTypeName(
                brainInComponent.gameObject,
                BrainInGameSettingsName
            );

            if (gameSettings == null)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Error,
                    Name,
                    $"BrainIn root object '{GetGameObjectPath(brainInComponent.gameObject)}' is missing '{BrainInGameSettingsName}'. Contract cannot be validated.",
                    scenePath
                ));

                return;
            }

            var localeKeys = LoadLocaleKeys(brainInComponent, context, results);

            ValidateInputParametersContract(gameSettings, results, scenePath);
            ValidateLocalizationContract(gameSettings, localeKeys, results, scenePath);
            ValidateCustomValidatorContract(gameSettings, results, scenePath);
            ValidateRoundGeneratorContract(gameSettings, results, scenePath);
            ValidateCustomGameControllerContract(gameSettings, results, scenePath);
        }

        /// <summary>
        /// Validates the custom input parameters factory and its produced type.
        /// </summary>
        /// <param name="gameSettings">BrainInGameSettings component.</param>
        /// <param name="results">Validation results collection to append detected issues to.</param>
        /// <param name="scenePath">Unity asset path of the scene containing the component.</param>
        private void ValidateInputParametersContract(
            Component gameSettings,
            List<ValidationResult> results,
            string scenePath)
        {
            var factory = GetRequiredObjectReference(
                gameSettings,
                InputParametersFactoryFieldName,
                "custom input parameters factory",
                results,
                scenePath
            );

            if (factory == null)
                return;

            if (IsStubReference(factory))
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Warning,
                    Name,
                    $"Input parameters contract still uses stub factory '{factory.GetType().Name}'. Replace it before final deployment.",
                    scenePath
                ));
            }

            var inputType = TryGetCreatedType(factory, "custom input parameters factory", results, scenePath);

            if (inputType == null)
                return;

            var jsonProperties = GetJsonContractMembers(inputType).ToList();

            if (jsonProperties.Count == 0)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Warning,
                    Name,
                    $"Input parameters type '{inputType.FullName}' does not declare any JsonProperty fields or properties. This is valid only if the task does not require custom input parameters.",
                    scenePath
                ));

                return;
            }

            ValidateDuplicateJsonProperties(inputType, jsonProperties, results, scenePath);
            ValidateJsonPropertyTypes(inputType, jsonProperties, results, scenePath);
            ValidateBaseInputParametersAvailability(results, scenePath);
        }

        /// <summary>
        /// Validates the custom localization factory and localization keys declared by the produced type.
        /// </summary>
        /// <param name="gameSettings">BrainInGameSettings component.</param>
        /// <param name="localeKeys">Localization keys loaded from locale.xml.</param>
        /// <param name="results">Validation results collection to append detected issues to.</param>
        /// <param name="scenePath">Unity asset path of the scene containing the component.</param>
        private void ValidateLocalizationContract(
            Component gameSettings,
            IReadOnlyCollection<string> localeKeys,
            List<ValidationResult> results,
            string scenePath)
        {
            var factory = GetRequiredObjectReference(
                gameSettings,
                LocalizationFactoryFieldName,
                "localization factory",
                results,
                scenePath
            );

            if (factory == null)
                return;

            if (IsStubReference(factory))
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Warning,
                    Name,
                    $"Localization contract still uses stub factory '{factory.GetType().Name}'. Replace it before final deployment.",
                    scenePath
                ));

                return;
            }

            var localizationType = TryGetCreatedType(factory, "localization factory", results, scenePath);

            if (localizationType == null)
                return;

            var localizationInstance = TryCreateInstance(factory, localizationType, results, scenePath);

            if (localizationInstance == null)
                return;

            var localizationKeys = GetLocalizationKeyValues(localizationType, localizationInstance).ToList();

            if (localizationKeys.Count == 0)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Warning,
                    Name,
                    $"Localization type '{localizationType.FullName}' does not declare any public string localization keys.",
                    scenePath
                ));

                return;
            }

            ValidateDuplicateLocalizationKeyValues(localizationType, localizationKeys, results, scenePath);

            if (localeKeys.Count == 0)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Warning,
                    Name,
                    "Localization keys from locale.xml could not be loaded. Cross-check between localization type and locale.xml was skipped.",
                    scenePath
                ));

                return;
            }

            foreach (var localizationKey in localizationKeys)
            {
                if (localeKeys.Contains(localizationKey.Value))
                    continue;

                results.Add(new ValidationResult(
                    ValidationSeverity.Error,
                    Name,
                    $"Localization key '{localizationKey.Value}' declared by '{localizationType.Name}.{localizationKey.MemberName}' was not found in locale.xml.",
                    scenePath
                ));
            }
        }

        /// <summary>
        /// Validates the custom input parameters validator factory.
        /// </summary>
        /// <param name="gameSettings">BrainInGameSettings component.</param>
        /// <param name="results">Validation results collection to append detected issues to.</param>
        /// <param name="scenePath">Unity asset path of the scene containing the component.</param>
        private void ValidateCustomValidatorContract(
            Component gameSettings,
            List<ValidationResult> results,
            string scenePath)
        {
            var factory = GetRequiredObjectReference(
                gameSettings,
                CustomValidatorFactoryFieldName,
                "custom parameters validator factory",
                results,
                scenePath
            );

            if (factory == null)
                return;

            if (!IsStubReference(factory))
                return;

            results.Add(new ValidationResult(
                ValidationSeverity.Warning,
                Name,
                $"Input validation contract still uses stub factory '{factory.GetType().Name}'. Replace it when the task has custom input validation rules.",
                scenePath
            ));
        }

        /// <summary>
        /// Validates the round data generator factory.
        /// </summary>
        /// <param name="gameSettings">BrainInGameSettings component.</param>
        /// <param name="results">Validation results collection to append detected issues to.</param>
        /// <param name="scenePath">Unity asset path of the scene containing the component.</param>
        private void ValidateRoundGeneratorContract(
            Component gameSettings,
            List<ValidationResult> results,
            string scenePath)
        {
            var factory = GetRequiredObjectReference(
                gameSettings,
                RoundDataGeneratorFactoryFieldName,
                "round data generator factory",
                results,
                scenePath
            );

            if (factory == null)
                return;

            if (!IsStubReference(factory))
                return;

            results.Add(new ValidationResult(
                ValidationSeverity.Warning,
                Name,
                $"Round generation contract still uses stub factory '{factory.GetType().Name}'. Replace it before final deployment.",
                scenePath
            ));
        }

        /// <summary>
        /// Validates the custom game controller reference and inheritance.
        /// </summary>
        /// <param name="gameSettings">BrainInGameSettings component.</param>
        /// <param name="results">Validation results collection to append detected issues to.</param>
        /// <param name="scenePath">Unity asset path of the scene containing the component.</param>
        private void ValidateCustomGameControllerContract(
            Component gameSettings,
            List<ValidationResult> results,
            string scenePath)
        {
            var controller = GetRequiredObjectReference(
                gameSettings,
                CustomGameControllerFieldName,
                "custom game controller",
                results,
                scenePath
            );

            if (controller == null)
                return;

            if (IsStubReference(controller))
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Warning,
                    Name,
                    $"Game controller contract still uses stub controller '{controller.GetType().Name}'. Replace it before final deployment.",
                    scenePath
                ));

                return;
            }

            if (HasBaseType(controller.GetType(), CustomGameControllerFullName))
                return;

            results.Add(new ValidationResult(
                ValidationSeverity.Error,
                Name,
                $"Custom game controller '{controller.GetType().FullName}' does not inherit from BrainIn CustomGameController.",
                scenePath
            ));
        }

        /// <summary>
        /// Validates duplicate JsonProperty names.
        /// </summary>
        /// <param name="inputType">Input parameters type.</param>
        /// <param name="jsonProperties">JSON contract members to validate.</param>
        /// <param name="results">Validation results collection to append detected issues to.</param>
        /// <param name="scenePath">Unity asset path of the scene containing the component.</param>
        private void ValidateDuplicateJsonProperties(
            Type inputType,
            IReadOnlyList<JsonContractMember> jsonProperties,
            List<ValidationResult> results,
            string scenePath)
        {
            var duplicateProperties = jsonProperties
                .GroupBy(member => member.JsonName)
                .Where(group => group.Count() > 1);

            foreach (var duplicateProperty in duplicateProperties)
            {
                var memberNames = string.Join(
                    ", ",
                    duplicateProperty.Select(member => member.MemberName)
                );

                results.Add(new ValidationResult(
                    ValidationSeverity.Error,
                    Name,
                    $"Input parameters type '{inputType.FullName}' declares duplicate JsonProperty name '{duplicateProperty.Key}' on members: {memberNames}.",
                    scenePath
                ));
            }
        }

        /// <summary>
        /// Validates whether JsonProperty members use types suitable for a web contract.
        /// </summary>
        /// <param name="inputType">Input parameters type.</param>
        /// <param name="jsonProperties">JSON contract members to validate.</param>
        /// <param name="results">Validation results collection to append detected issues to.</param>
        /// <param name="scenePath">Unity asset path of the scene containing the component.</param>
        private void ValidateJsonPropertyTypes(
            Type inputType,
            IReadOnlyList<JsonContractMember> jsonProperties,
            List<ValidationResult> results,
            string scenePath)
        {
            foreach (var jsonProperty in jsonProperties)
            {
                if (IsSupportedInputContractType(jsonProperty.MemberType))
                    continue;

                results.Add(new ValidationResult(
                    ValidationSeverity.Warning,
                    Name,
                    $"Input parameter '{jsonProperty.JsonName}' on type '{inputType.FullName}' uses type '{jsonProperty.MemberType.Name}'. Prefer simple JSON-compatible types such as string, int, float, double, bool, enum, arrays, or lists.",
                    scenePath
                ));
            }
        }

        /// <summary>
        /// Reports the expected BrainIn base input parameters.
        /// </summary>
        /// <param name="results">Validation results collection to append detected issues to.</param>
        /// <param name="scenePath">Unity asset path of the scene containing the component.</param>
        private void ValidateBaseInputParametersAvailability(
            List<ValidationResult> results,
            string scenePath)
        {
            var baseInputType = FindTypeByFullName(
                "BrainInTemplate.Runtime.Code.Core.Service.IO.Input.InputParametersBase"
            );

            if (baseInputType == null)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Warning,
                    Name,
                    "Could not find BrainIn InputParametersBase type. Base input parameter contract could not be inspected.",
                    scenePath
                ));

                return;
            }

            var baseJsonNames = GetJsonContractMembers(baseInputType)
                .Select(member => member.JsonName)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var requiredName in RequiredBaseInputParameterNames)
            {
                if (baseJsonNames.Contains(requiredName))
                    continue;

                results.Add(new ValidationResult(
                    ValidationSeverity.Error,
                    Name,
                    $"BrainIn base input parameter '{requiredName}' was not found in InputParametersBase. The BrainIn template package may have changed.",
                    scenePath
                ));
            }
        }

        /// <summary>
        /// Validates duplicate localization key values declared by the localization type.
        /// </summary>
        /// <param name="localizationType">Localization type.</param>
        /// <param name="localizationKeys">Localization key values to validate.</param>
        /// <param name="results">Validation results collection to append detected issues to.</param>
        /// <param name="scenePath">Unity asset path of the scene containing the component.</param>
        private void ValidateDuplicateLocalizationKeyValues(
            Type localizationType,
            IReadOnlyList<LocalizationKeyValue> localizationKeys,
            List<ValidationResult> results,
            string scenePath)
        {
            var duplicates = localizationKeys
                .GroupBy(key => key.Value)
                .Where(group => group.Count() > 1);

            foreach (var duplicate in duplicates)
            {
                var members = string.Join(", ", duplicate.Select(key => key.MemberName));

                results.Add(new ValidationResult(
                    ValidationSeverity.Warning,
                    Name,
                    $"Localization type '{localizationType.FullName}' declares duplicate localization key value '{duplicate.Key}' on members: {members}.",
                    scenePath
                ));
            }
        }

        /// <summary>
        /// Tries to get the created type from a BrainIn factory.
        /// </summary>
        /// <param name="factory">Factory object to inspect.</param>
        /// <param name="factoryDisplayName">Human-readable factory name.</param>
        /// <param name="results">Validation results collection to append detected issues to.</param>
        /// <param name="assetPath">Unity asset path used in the validation result.</param>
        /// <returns>Created type if available; otherwise null.</returns>
        private Type TryGetCreatedType(
            UnityEngine.Object factory,
            string factoryDisplayName,
            List<ValidationResult> results,
            string assetPath)
        {
            var method = factory
                .GetType()
                .GetMethod("GetCreatedType", BindingFlags.Public | BindingFlags.Instance);

            if (method == null)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Error,
                    Name,
                    $"The {factoryDisplayName} '{factory.GetType().Name}' does not expose GetCreatedType().",
                    assetPath
                ));

                return null;
            }

            try
            {
                return method.Invoke(factory, Array.Empty<object>()) as Type;
            }
            catch (Exception exception)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Error,
                    Name,
                    $"Calling GetCreatedType() on {factoryDisplayName} '{factory.GetType().Name}' failed: {GetExceptionMessage(exception)}",
                    assetPath
                ));

                return null;
            }
        }

        /// <summary>
        /// Tries to create an instance from a BrainIn factory.
        /// </summary>
        /// <param name="factory">Factory object to inspect.</param>
        /// <param name="createdType">Expected created type.</param>
        /// <param name="results">Validation results collection to append detected issues to.</param>
        /// <param name="assetPath">Unity asset path used in the validation result.</param>
        /// <returns>Created instance if available; otherwise null.</returns>
        private object TryCreateInstance(
            UnityEngine.Object factory,
            Type createdType,
            List<ValidationResult> results,
            string assetPath)
        {
            try
            {
                return Activator.CreateInstance(createdType);
            }
            catch
            {
                // Some game classes may not expose a parameterless constructor.
                // In that case, fall back to the factory because this is the same mechanism used by the runtime.
            }

            var method = factory
                .GetType()
                .GetMethod("Create", BindingFlags.Public | BindingFlags.Instance);

            if (method == null)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Error,
                    Name,
                    $"Factory '{factory.GetType().Name}' does not expose Create().",
                    assetPath
                ));

                return null;
            }

            try
            {
                return method.Invoke(factory, Array.Empty<object>());
            }
            catch (Exception exception)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Error,
                    Name,
                    $"Calling Create() on factory '{factory.GetType().Name}' failed: {GetExceptionMessage(exception)}",
                    assetPath
                ));

                return null;
            }
        }

        /// <summary>
        /// Gets a required serialized object reference from a component.
        /// </summary>
        /// <param name="component">Component containing the serialized field.</param>
        /// <param name="fieldName">Serialized field name.</param>
        /// <param name="displayName">Human-readable field name.</param>
        /// <param name="results">Validation results collection to append detected issues to.</param>
        /// <param name="assetPath">Unity asset path used in the validation result.</param>
        /// <returns>Referenced object if assigned; otherwise null.</returns>
        private UnityEngine.Object GetRequiredObjectReference(
            Component component,
            string fieldName,
            string displayName,
            List<ValidationResult> results,
            string assetPath)
        {
            var property = GetSerializedProperty(component, fieldName);

            if (property == null)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Warning,
                    Name,
                    $"Could not find serialized field '{fieldName}' on component '{component.GetType().Name}'. The BrainIn template package may have changed.",
                    assetPath
                ));

                return null;
            }

            if (property.propertyType != SerializedPropertyType.ObjectReference)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Warning,
                    Name,
                    $"Serialized field '{fieldName}' on component '{component.GetType().Name}' is not an object reference.",
                    assetPath
                ));

                return null;
            }

            if (property.objectReferenceValue != null)
                return property.objectReferenceValue;

            results.Add(new ValidationResult(
                ValidationSeverity.Error,
                Name,
                $"Component '{component.GetType().Name}' has missing required contract reference: {displayName}.",
                assetPath
            ));

            return null;
        }

        /// <summary>
        /// Gets all JsonProperty members from a type.
        /// </summary>
        /// <param name="type">Type to inspect.</param>
        /// <returns>JSON contract members.</returns>
        private static IEnumerable<JsonContractMember> GetJsonContractMembers(Type type)
        {
            var fields = type.GetFields(
                BindingFlags.Public |
                BindingFlags.Instance |
                BindingFlags.FlattenHierarchy
            );

            foreach (var field in fields)
            {
                var jsonName = GetJsonPropertyName(field);

                if (string.IsNullOrWhiteSpace(jsonName))
                    continue;

                yield return new JsonContractMember(
                    field.Name,
                    jsonName,
                    field.FieldType
                );
            }

            var properties = type.GetProperties(
                BindingFlags.Public |
                BindingFlags.Instance |
                BindingFlags.FlattenHierarchy
            );

            foreach (var property in properties)
            {
                var jsonName = GetJsonPropertyName(property);

                if (string.IsNullOrWhiteSpace(jsonName))
                    continue;

                yield return new JsonContractMember(
                    property.Name,
                    jsonName,
                    property.PropertyType
                );
            }
        }

        /// <summary>
        /// Gets the JsonProperty name from a reflected member.
        /// </summary>
        /// <param name="memberInfo">Member to inspect.</param>
        /// <returns>JsonProperty name if found; otherwise null.</returns>
        private static string GetJsonPropertyName(MemberInfo memberInfo)
        {
            var attribute = memberInfo
                .GetCustomAttributes(false)
                .FirstOrDefault(customAttribute =>
                    customAttribute.GetType().FullName == JsonPropertyAttributeFullName);

            if (attribute == null)
                return null;

            var propertyNameProperty = attribute
                .GetType()
                .GetProperty("PropertyName", BindingFlags.Public | BindingFlags.Instance);

            return propertyNameProperty?.GetValue(attribute) as string;
        }

        /// <summary>
        /// Gets localization key values from public string fields and properties declared directly on the custom localization type.
        /// Inherited members from LocalizationBase are ignored because they belong to the BrainIn template contract.
        /// </summary>
        /// <param name="type">Localization type.</param>
        /// <param name="instance">Localization instance.</param>
        /// <returns>Localization key values.</returns>
        private static IEnumerable<LocalizationKeyValue> GetLocalizationKeyValues(Type type, object instance)
        {
            const BindingFlags flags =
                BindingFlags.Public |
                BindingFlags.Instance |
                BindingFlags.DeclaredOnly;

            var fields = type.GetFields(flags);

            foreach (var field in fields.Where(field => field.FieldType == typeof(string)))
            {
                var value = field.GetValue(instance) as string;

                if (string.IsNullOrWhiteSpace(value))
                    continue;

                yield return new LocalizationKeyValue(field.Name, value);
            }

            var properties = type.GetProperties(flags);

            foreach (var property in properties.Where(property =>
                         property.PropertyType == typeof(string) &&
                         property.GetMethod != null &&
                         property.GetIndexParameters().Length == 0))
            {
                string value;

                try
                {
                    value = property.GetValue(instance) as string;
                }
                catch
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(value))
                    continue;

                yield return new LocalizationKeyValue(property.Name, value);
            }
        }

        /// <summary>
        /// Loads localization keys from the locale.xml file configured on the BrainIn component.
        /// </summary>
        /// <param name="brainInComponent">BrainIn component containing the localization path.</param>
        /// <param name="context">Validation context containing project paths.</param>
        /// <param name="results">Validation results collection to append detected issues to.</param>
        /// <returns>Localization keys from locale.xml.</returns>
        private IReadOnlyCollection<string> LoadLocaleKeys(
            Component brainInComponent,
            ValidationContext context,
            List<ValidationResult> results)
        {
            var localesTablePath = GetStringProperty(brainInComponent, LocalesTablePathFieldName);

            if (string.IsNullOrWhiteSpace(localesTablePath))
                return Array.Empty<string>();

            var localeAssetPath = ResolveStreamingAssetsAssetPath(localesTablePath);
            var localeFullPath = Path.Combine(
                context.ProjectPath,
                localeAssetPath.Replace("/", Path.DirectorySeparatorChar.ToString())
            );

            if (!File.Exists(localeFullPath))
                return Array.Empty<string>();

            try
            {
                var document = XDocument.Load(localeFullPath);
                return document
                    .Descendants()
                    .Where(IsLocalizationEntry)
                    .SelectMany(CreateLocalizationKeyCandidates)
                    .ToHashSet(StringComparer.Ordinal);
            }
            catch (Exception exception)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Warning,
                    Name,
                    $"Could not load locale.xml for contract cross-check: {exception.Message}",
                    localeAssetPath
                ));

                return Array.Empty<string>();
            }
        }

        /// <summary>
        /// Creates possible localization key candidates for an XML localization entry.
        /// </summary>
        /// <param name="entry">Localization entry XML element.</param>
        /// <returns>Localization key candidates.</returns>
        private static IEnumerable<string> CreateLocalizationKeyCandidates(XElement entry)
        {
            yield return entry.Name.LocalName;

            var fullPath = string.Join(
                "/",
                entry
                    .AncestorsAndSelf()
                    .Reverse()
                    .Where(element => element.Parent != null)
                    .Select(element => element.Name.LocalName)
            );

            if (!string.IsNullOrWhiteSpace(fullPath))
                yield return fullPath;
        }

        /// <summary>
        /// Determines whether an XML element represents a localization entry.
        /// </summary>
        /// <param name="element">XML element to inspect.</param>
        /// <returns>True if the element contains language child elements; otherwise false.</returns>
        private static bool IsLocalizationEntry(XElement element)
        {
            return element
                .Elements()
                .Any(child =>
                    child.Name.LocalName == "cs" ||
                    child.Name.LocalName == "en" ||
                    child.Name.LocalName == "de");
        }

        /// <summary>
        /// Determines whether an input parameter type is suitable for a simple web contract.
        /// </summary>
        /// <param name="type">Type to inspect.</param>
        /// <returns>True if the type is supported; otherwise false.</returns>
        private static bool IsSupportedInputContractType(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;

            if (type == typeof(string) ||
                type == typeof(int) ||
                type == typeof(long) ||
                type == typeof(float) ||
                type == typeof(double) ||
                type == typeof(decimal) ||
                type == typeof(bool))
            {
                return true;
            }

            if (type.IsEnum)
                return true;

            if (type.IsArray)
                return IsSupportedInputContractType(type.GetElementType());

            if (type.IsGenericType &&
                type.GetGenericTypeDefinition() == typeof(List<>))
            {
                return IsSupportedInputContractType(type.GetGenericArguments()[0]);
            }

            return false;
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
                    if (component == null)
                        continue;

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
                    component != null &&
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
                .Where(scene => scene is { enabled: true } && !string.IsNullOrWhiteSpace(scene.path))
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
                    return true;
            }

            return false;
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
        /// Finds a type by its full name in the current editor domain.
        /// </summary>
        /// <param name="fullName">Full name of the type to find.</param>
        /// <returns>Type if found; otherwise null.</returns>
        private static Type FindTypeByFullName(string fullName)
        {
            return AppDomain
                .CurrentDomain
                .GetAssemblies()
                .Select(assembly => assembly.GetType(fullName))
                .FirstOrDefault(type => type != null);
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
        /// Gets a string stored in a serialized field.
        /// </summary>
        /// <param name="component">Component containing the serialized field.</param>
        /// <param name="fieldName">Serialized field name.</param>
        /// <returns>String value if found; otherwise null.</returns>
        private static string GetStringProperty(Component component, string fieldName)
        {
            var property = GetSerializedProperty(component, fieldName);

            if (property == null || property.propertyType != SerializedPropertyType.String)
                return null;

            return property.stringValue;
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
        /// Determines whether a type inherits from a type with the given full name.
        /// </summary>
        /// <param name="type">Type to inspect.</param>
        /// <param name="baseTypeFullName">Expected base type full name.</param>
        /// <returns>True if the base type exists in the inheritance chain; otherwise false.</returns>
        private static bool HasBaseType(Type type, string baseTypeFullName)
        {
            var current = type;

            while (current != null)
            {
                if (current.FullName == baseTypeFullName)
                    return true;

                current = current.BaseType;
            }

            return false;
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

            if (normalizedPath.StartsWith("StreamingAssets/", StringComparison.OrdinalIgnoreCase))
                return $"Assets/{normalizedPath}";

            return $"Assets/StreamingAssets/{normalizedPath}";
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

            while (current != null)
            {
                names.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", names);
        }

        /// <summary>
        /// Gets the most useful message from a reflection exception.
        /// </summary>
        /// <param name="exception">Exception to format.</param>
        /// <returns>Readable exception message.</returns>
        private static string GetExceptionMessage(Exception exception)
        {
            if (exception is TargetInvocationException { InnerException: not null } targetInvocationException)
            {
                return targetInvocationException.InnerException.Message;
            }

            return exception.Message;
        }

        /// <summary>
        /// Represents a JSON contract member.
        /// </summary>
        private sealed class JsonContractMember
        {
            /// <summary>
            /// Creates a new JSON contract member.
            /// </summary>
            /// <param name="memberName">CLR member name.</param>
            /// <param name="jsonName">JSON property name.</param>
            /// <param name="memberType">CLR member type.</param>
            public JsonContractMember(string memberName, string jsonName, Type memberType)
            {
                MemberName = memberName;
                JsonName = jsonName;
                MemberType = memberType;
            }

            /// <summary>
            /// Gets the CLR member name.
            /// </summary>
            public string MemberName { get; }

            /// <summary>
            /// Gets the JSON property name.
            /// </summary>
            public string JsonName { get; }

            /// <summary>
            /// Gets the CLR member type.
            /// </summary>
            public Type MemberType { get; }
        }

        /// <summary>
        /// Represents a localization key declared by a localization type.
        /// </summary>
        private sealed class LocalizationKeyValue
        {
            /// <summary>
            /// Creates a new localization key value.
            /// </summary>
            /// <param name="memberName">CLR member name.</param>
            /// <param name="value">Localization key value.</param>
            public LocalizationKeyValue(string memberName, string value)
            {
                MemberName = memberName;
                Value = value;
            }

            /// <summary>
            /// Gets the CLR member name.
            /// </summary>
            public string MemberName { get; }

            /// <summary>
            /// Gets the localization key value.
            /// </summary>
            public string Value { get; }
        }
    }
}