using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BrainIn.DevTools.Diagnostics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BrainIn.DevTools.Editor.Diagnostics.Results
{
    /// <summary>
    /// Loads expected BrainIn customData keys from components in open Unity editor contexts.
    /// </summary>
    public static class ExpectedCustomDataContractLoader
    {
        private const string BrainInComponentFullName = "BrainInTemplate.Runtime.Code.BrainIn";
        private const string CustomGameControllerTypeName = "CustomGameController";
        private const string GameSettingsPropertyName = "gameSettings";
        private const string CustomGameControllerPropertyName = "customGameController";

        /// <summary>
        /// Loads expected customData keys from components in open scenes and prefab stage.
        /// Components with ExpectedCustomDataKey attributes are used directly.
        /// BrainIn custom game controllers are also inspected as a fallback.
        /// </summary>
        /// <returns>Loaded expected customData contract.</returns>
        public static ExpectedCustomDataContractLoadResult LoadFromOpenScenes()
        {
            var result = new ExpectedCustomDataContractLoadResult();
            var components = GetComponentsInOpenEditorContexts()
                .Where(component => component)
                .ToList();

            if (components.Count == 0)
            {
                result.Warnings.Add("No components were found in currently open scenes or prefab stage.");
                return result;
            }

            var componentsWithAttributes = components
                .Where(ComponentDeclaresExpectedKeys)
                .ToList();

            var referencedControllers = FindCustomGameControllersReferencedByBrainIn(components);
            var discoveredControllers = components.Where(IsLikelyCustomGameController);

            var contractSources = DistinctComponents(
                    componentsWithAttributes
                        .Concat(referencedControllers)
                        .Concat(discoveredControllers)
                )
                .ToList();

            if (contractSources.Count == 0)
            {
                result.Warnings.Add(
                    $"No component with ExpectedCustomDataKey attributes and no BrainIn custom game controller was found. " +
                    $"Scanned {components.Count} component(s) in open editor contexts."
                );

                return result;
            }

            foreach (var source in contractSources)
            {
                var sourceType = source.GetType();
                var keys = GetExpectedKeys(sourceType).ToList();

                if (keys.Count == 0)
                    continue;

                result.SourceNames.Add($"{sourceType.Name} on {GetGameObjectPath(source.gameObject)}");

                foreach (var key in keys)
                {
                    if (!result.Keys.Contains(key))
                        result.Keys.Add(key);
                }
            }

            if (result.Keys.Count == 0)
            {
                var sourceNames = contractSources
                    .Select(source => $"{source.GetType().Name} on {GetGameObjectPath(source.gameObject)}")
                    .ToList();

                result.Warnings.Add(
                    "Potential custom game controller/component source was found, but it does not declare any " +
                    "ExpectedCustomDataKey attributes."
                );

                foreach (var sourceName in sourceNames)
                {
                    result.Warnings.Add($"Inspected source: {sourceName}");
                }
            }

            return result;
        }

        /// <summary>
        /// Finds custom game controllers referenced from BrainInGameSettings.
        /// </summary>
        /// <param name="components">Components to inspect.</param>
        /// <returns>Referenced custom game controllers.</returns>
        private static IEnumerable<Component> FindCustomGameControllersReferencedByBrainIn(
            IReadOnlyCollection<Component> components)
        {
            foreach (var brainInComponent in components.Where(component =>
                         component.GetType().FullName == BrainInComponentFullName))
            {
                if (TryGetCustomGameControllerFromBrainIn(brainInComponent, out var customGameController))
                    yield return customGameController;
            }

            foreach (var gameSettings in components.Where(component =>
                         component.GetType().Name == "BrainInGameSettings"))
            {
                if (TryGetCustomGameControllerFromGameSettings(gameSettings, out var customGameController))
                    yield return customGameController;
            }
        }

        /// <summary>
        /// Determines whether a component type declares at least one ExpectedCustomDataKey attribute.
        /// </summary>
        /// <param name="component">Component to inspect.</param>
        /// <returns>True if the component declares expected customData keys; otherwise false.</returns>
        private static bool ComponentDeclaresExpectedKeys(Component component)
        {
            return component != null && GetExpectedKeys(component.GetType()).Any();
        }

        /// <summary>
        /// Determines whether a component is likely a BrainIn custom game controller.
        /// This fallback uses type names to avoid depending on exact namespace versions.
        /// </summary>
        /// <param name="component">Component to inspect.</param>
        /// <returns>True if the component appears to derive from CustomGameController; otherwise false.</returns>
        private static bool IsLikelyCustomGameController(Component component)
        {
            if (component == null)
                return false;

            var type = component.GetType();

            while (type != null && type != typeof(MonoBehaviour))
            {
                if (type.Name == CustomGameControllerTypeName)
                    return true;

                type = type.BaseType;
            }

            return false;
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
                    continue;

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
        /// Tries to get custom game controller from a BrainIn root component.
        /// </summary>
        /// <param name="brainInComponent">BrainIn root component.</param>
        /// <param name="customGameController">Resolved custom game controller.</param>
        /// <returns>True if the custom game controller was resolved; otherwise false.</returns>
        private static bool TryGetCustomGameControllerFromBrainIn(
            Component brainInComponent,
            out Component customGameController)
        {
            customGameController = null;

            var serializedBrainIn = new SerializedObject(brainInComponent);
            var gameSettingsProperty = serializedBrainIn.FindProperty(GameSettingsPropertyName);

            if (gameSettingsProperty == null ||
                !(gameSettingsProperty.objectReferenceValue is Component gameSettings))
            {
                return false;
            }

            return TryGetCustomGameControllerFromGameSettings(gameSettings, out customGameController);
        }

        /// <summary>
        /// Tries to get custom game controller from BrainInGameSettings.
        /// </summary>
        /// <param name="gameSettings">BrainIn game settings component.</param>
        /// <param name="customGameController">Resolved custom game controller.</param>
        /// <returns>True if the custom game controller was resolved; otherwise false.</returns>
        private static bool TryGetCustomGameControllerFromGameSettings(
            Component gameSettings,
            out Component customGameController)
        {
            customGameController = null;

            var serializedGameSettings = new SerializedObject(gameSettings);
            var customGameControllerProperty = serializedGameSettings.FindProperty(CustomGameControllerPropertyName);

            if (customGameControllerProperty == null ||
                !(customGameControllerProperty.objectReferenceValue is Component controller))
            {
                return false;
            }

            customGameController = controller;
            return true;
        }

        /// <summary>
        /// Gets expected customData keys declared on fields and properties of a component type.
        /// </summary>
        /// <param name="type">Component type.</param>
        /// <returns>Expected customData keys.</returns>
        private static IEnumerable<string> GetExpectedKeys(Type type)
        {
            foreach (var field in GetFieldsInHierarchy(type))
            {
                foreach (var attribute in field
                             .GetCustomAttributes(typeof(ExpectedCustomDataKeyAttribute), true)
                             .Cast<ExpectedCustomDataKeyAttribute>())
                {
                    var key = ResolveKey(attribute, field.Name);

                    if (!string.IsNullOrWhiteSpace(key))
                        yield return key;
                }
            }

            foreach (var property in GetPropertiesInHierarchy(type))
            {
                foreach (var attribute in property
                             .GetCustomAttributes(typeof(ExpectedCustomDataKeyAttribute), true)
                             .Cast<ExpectedCustomDataKeyAttribute>())
                {
                    var key = ResolveKey(attribute, property.Name);

                    if (!string.IsNullOrWhiteSpace(key))
                        yield return key;
                }
            }
        }

        /// <summary>
        /// Gets all instance fields declared in the type hierarchy.
        /// </summary>
        /// <param name="type">Starting type.</param>
        /// <returns>Fields from the type hierarchy.</returns>
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
        /// Gets all instance properties declared in the type hierarchy.
        /// </summary>
        /// <param name="type">Starting type.</param>
        /// <returns>Properties from the type hierarchy.</returns>
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
        /// Resolves expected customData key from an attribute and member name.
        /// </summary>
        /// <param name="attribute">Expected customData key attribute.</param>
        /// <param name="memberName">Field or property name.</param>
        /// <returns>Resolved customData key.</returns>
        private static string ResolveKey(ExpectedCustomDataKeyAttribute attribute, string memberName)
        {
            if (!string.IsNullOrWhiteSpace(attribute.Key))
                return attribute.Key.Trim();

            return NormalizeMemberName(memberName);
        }

        /// <summary>
        /// Normalizes a field or property name into a likely customData key.
        /// </summary>
        /// <param name="memberName">Field or property name.</param>
        /// <returns>Normalized customData key.</returns>
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
        /// Gets a readable hierarchy path for a GameObject.
        /// </summary>
        /// <param name="gameObject">GameObject.</param>
        /// <returns>Readable hierarchy path.</returns>
        private static string GetGameObjectPath(GameObject gameObject)
        {
            if (gameObject == null)
                return "<missing>";

            var path = gameObject.name;
            var current = gameObject.transform.parent;

            while (current != null)
            {
                path = $"{current.name}/{path}";
                current = current.parent;
            }

            return path;
        }
    }

    /// <summary>
    /// Result of loading expected BrainIn customData contract from open editor contexts.
    /// </summary>
    public sealed class ExpectedCustomDataContractLoadResult
    {
        /// <summary>
        /// Gets loaded expected customData keys.
        /// </summary>
        public List<string> Keys { get; } = new List<string>();

        /// <summary>
        /// Gets names of components that were used as contract sources.
        /// </summary>
        public List<string> SourceNames { get; } = new List<string>();

        /// <summary>
        /// Gets non-fatal loading warnings.
        /// </summary>
        public List<string> Warnings { get; } = new List<string>();
    }
}