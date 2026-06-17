using System;
using System.Collections.Generic;
using BrainIn.DevTools.Editor.Validation.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BrainIn.DevTools.Editor.Validation.Rules
{
    /// <summary>
    /// Validates Unity scenes and prefabs for missing scripts and broken serialized object references.
    /// </summary>
    public sealed class MissingReferencesRule : IValidationRule
    {
        private const string SearchRoot = "Assets";

        /// <summary>
        /// Asset path prefixes that should be ignored by the missing references validator.
        /// These paths usually contain third-party sample content that is not part of a BrainIn task.
        /// </summary>
        private static readonly string[] IgnoredAssetPathPrefixes =
        {
            "Assets/TextMesh Pro/Examples & Extras/"
        };

        /// <summary>
        /// Gets the display name of the validation rule.
        /// </summary>
        public string Name => "Missing references validator";

        /// <summary>
        /// Runs the missing references validation over project scenes and prefabs.
        /// </summary>
        /// <param name="context">Validation context containing project paths and shared validation data.</param>
        /// <returns>Collection of validation results found by this rule.</returns>
        public IEnumerable<ValidationResult> Validate(ValidationContext context)
        {
            var results = new List<ValidationResult>();

            ValidatePrefabs(results);
            ValidateScenes(results);

            if (results.Count == 0)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Info,
                    Name,
                    "No missing scripts or broken serialized references were found in project scenes and prefabs."
                ));
            }

            return results;
        }

        /// <summary>
        /// Validates all prefabs under the Assets folder.
        /// </summary>
        /// <param name="results">Validation results collection to append detected issues to.</param>
        private void ValidatePrefabs(List<ValidationResult> results)
        {
            var prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { SearchRoot });

            foreach (var guid in prefabGuids)
            {
                var prefabPath = AssetDatabase.GUIDToAssetPath(guid);

                if (string.IsNullOrWhiteSpace(prefabPath))
                    continue;

                if (IsIgnoredAssetPath(prefabPath))
                    continue;

                GameObject prefabRoot = null;

                try
                {
                    prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

                    if (prefabRoot == null)
                    {
                        results.Add(new ValidationResult(
                            ValidationSeverity.Warning,
                            Name,
                            $"Could not load prefab contents: {prefabPath}",
                            prefabPath
                        ));

                        continue;
                    }

                    ScanGameObjectHierarchy(prefabRoot, prefabPath, results);
                }
                catch (Exception exception)
                {
                    results.Add(new ValidationResult(
                        ValidationSeverity.Warning,
                        Name,
                        $"Failed to validate prefab '{prefabPath}'. Reason: {exception.Message}",
                        prefabPath
                    ));
                }
                finally
                {
                    if (prefabRoot != null)
                        PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }
        }

        /// <summary>
        /// Validates all scenes under the Assets folder.
        /// </summary>
        /// <param name="results">Validation results collection to append detected issues to.</param>
        private void ValidateScenes(List<ValidationResult> results)
        {
            var sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { SearchRoot });

            foreach (var guid in sceneGuids)
            {
                var scenePath = AssetDatabase.GUIDToAssetPath(guid);

                if (string.IsNullOrWhiteSpace(scenePath))
                    continue;

                if (IsIgnoredAssetPath(scenePath))
                    continue;

                ValidateScene(scenePath, results);
            }
        }

        /// <summary>
        /// Validates a single scene. If the scene is not already loaded, it is opened additively and closed afterwards.
        /// </summary>
        /// <param name="scenePath">Asset path of the scene to validate.</param>
        /// <param name="results">Validation results collection to append detected issues to.</param>
        private void ValidateScene(string scenePath, List<ValidationResult> results)
        {
            var openedByValidator = false;
            Scene scene = default;

            try
            {
                if (!TryGetLoadedScene(scenePath, out scene))
                {
                    scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                    openedByValidator = true;
                }

                if (!scene.IsValid() || !scene.isLoaded)
                {
                    results.Add(new ValidationResult(
                        ValidationSeverity.Warning,
                        Name,
                        $"Could not load scene for validation: {scenePath}",
                        scenePath
                    ));

                    return;
                }

                foreach (var rootGameObject in scene.GetRootGameObjects())
                    ScanGameObjectHierarchy(rootGameObject, scenePath, results);
            }
            catch (Exception exception)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Warning,
                    Name,
                    $"Failed to validate scene '{scenePath}'. Reason: {exception.Message}",
                    scenePath
                ));
            }
            finally
            {
                if (openedByValidator && scene.IsValid())
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        /// <summary>
        /// Scans a GameObject hierarchy for missing scripts and broken serialized references.
        /// </summary>
        /// <param name="root">Root GameObject of the hierarchy to scan.</param>
        /// <param name="assetPath">Asset path of the scene or prefab being scanned.</param>
        /// <param name="results">Validation results collection to append detected issues to.</param>
        private void ScanGameObjectHierarchy(GameObject root, string assetPath, List<ValidationResult> results)
        {
            if (root == null)
                return;

            var stack = new Stack<Transform>();
            stack.Push(root.transform);

            while (stack.Count > 0)
            {
                var current = stack.Pop();
                var currentGameObject = current.gameObject;
                var gameObjectPath = GetGameObjectPath(currentGameObject);

                ValidateMissingScripts(currentGameObject, gameObjectPath, assetPath, results);
                ValidateBrokenSerializedReferences(currentGameObject, gameObjectPath, assetPath, results);

                for (var i = 0; i < current.childCount; i++)
                    stack.Push(current.GetChild(i));
            }
        }

        /// <summary>
        /// Validates whether the given GameObject contains missing MonoBehaviour scripts.
        /// </summary>
        /// <param name="gameObject">GameObject to validate.</param>
        /// <param name="gameObjectPath">Readable hierarchy path of the GameObject.</param>
        /// <param name="assetPath">Asset path of the scene or prefab being scanned.</param>
        /// <param name="results">Validation results collection to append detected issues to.</param>
        private void ValidateMissingScripts(
            GameObject gameObject,
            string gameObjectPath,
            string assetPath,
            List<ValidationResult> results)
        {
            var missingScriptCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject);

            if (missingScriptCount <= 0)
                return;

            results.Add(new ValidationResult(
                ValidationSeverity.Error,
                Name,
                $"GameObject '{gameObjectPath}' contains {missingScriptCount} missing script component(s).",
                assetPath
            ));
        }

        /// <summary>
        /// Validates serialized object reference properties on all non-missing components of the given GameObject.
        /// </summary>
        /// <param name="gameObject">GameObject whose components should be validated.</param>
        /// <param name="gameObjectPath">Readable hierarchy path of the GameObject.</param>
        /// <param name="assetPath">Asset path of the scene or prefab being scanned.</param>
        /// <param name="results">Validation results collection to append detected issues to.</param>
        private void ValidateBrokenSerializedReferences(
            GameObject gameObject,
            string gameObjectPath,
            string assetPath,
            List<ValidationResult> results)
        {
            var components = gameObject.GetComponents<Component>();

            foreach (var component in components)
            {
                if (component == null)
                    continue;

                ValidateSerializedObject(component, gameObjectPath, assetPath, results);
            }
        }

        /// <summary>
        /// Validates one serialized Unity component for broken object references.
        /// </summary>
        /// <param name="component">Component to validate.</param>
        /// <param name="gameObjectPath">Readable hierarchy path of the component owner.</param>
        /// <param name="assetPath">Asset path of the scene or prefab being scanned.</param>
        /// <param name="results">Validation results collection to append detected issues to.</param>
        private void ValidateSerializedObject(
            Component component,
            string gameObjectPath,
            string assetPath,
            List<ValidationResult> results)
        {
            SerializedObject serializedObject;

            try
            {
                serializedObject = new SerializedObject(component);
            }
            catch (Exception exception)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Warning,
                    Name,
                    $"Could not inspect component '{component.GetType().Name}' on GameObject '{gameObjectPath}'. Reason: {exception.Message}",
                    assetPath
                ));

                return;
            }

            var property = serializedObject.GetIterator();
            var enterChildren = true;

            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;

                if (!IsBrokenObjectReference(property))
                    continue;

                results.Add(new ValidationResult(
                    ValidationSeverity.Error,
                    Name,
                    $"Broken reference found on GameObject '{gameObjectPath}', component '{component.GetType().Name}', property '{property.propertyPath}'.",
                    assetPath
                ));
            }
        }

        /// <summary>
        /// Determines whether a serialized property represents a broken Unity object reference.
        /// </summary>
        /// <param name="property">Serialized property to inspect.</param>
        /// <returns>True if the property is a broken object reference; otherwise false.</returns>
        private static bool IsBrokenObjectReference(SerializedProperty property)
        {
            if (property.propertyType != SerializedPropertyType.ObjectReference)
                return false;

            if (property.objectReferenceValue != null)
                return false;

            return property.objectReferenceInstanceIDValue != 0;
        }

        /// <summary>
        /// Determines whether the specified asset path should be ignored by this validation rule.
        /// </summary>
        /// <param name="assetPath">Unity asset path to check.</param>
        /// <returns>True if the asset path should be ignored; otherwise false.</returns>
        private static bool IsIgnoredAssetPath(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                return false;

            var normalizedPath = assetPath.Replace('\\', '/');

            foreach (var ignoredPrefix in IgnoredAssetPathPrefixes)
            {
                if (normalizedPath.StartsWith(ignoredPrefix, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Tries to find an already loaded scene by its asset path.
        /// </summary>
        /// <param name="scenePath">Asset path of the scene.</param>
        /// <param name="scene">Loaded scene if found.</param>
        /// <returns>True if the scene is already loaded; otherwise false.</returns>
        private static bool TryGetLoadedScene(string scenePath, out Scene scene)
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var loadedScene = SceneManager.GetSceneAt(i);

                if (loadedScene.path == scenePath)
                {
                    scene = loadedScene;
                    return true;
                }
            }

            scene = default;
            return false;
        }

        /// <summary>
        /// Builds a readable hierarchy path for the given GameObject.
        /// </summary>
        /// <param name="gameObject">GameObject whose path should be created.</param>
        /// <returns>Readable GameObject hierarchy path.</returns>
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
    }
}