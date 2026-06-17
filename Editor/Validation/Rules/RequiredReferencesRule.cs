using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BrainIn.DevTools.Editor.Validation.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BrainIn.DevTools.Editor.Validation.Rules
{
    /// <summary>
    /// Validates Unity object references explicitly marked as required.
    /// </summary>
    public sealed class RequiredReferencesRule : IValidationRule
    {
        private const string BrainInComponentFullName = "BrainInTemplate.Runtime.Code.BrainIn";
        private const string RequiredReferenceAttributeName = "RequiredReferenceAttribute";

        /// <summary>
        /// Gets the display name of the validation rule.
        /// </summary>
        public string Name => "Required references validator";

        /// <summary>
        /// Validates required serialized references in enabled BrainIn build scenes.
        /// </summary>
        /// <param name="context">Validation context.</param>
        /// <returns>Validation results describing missing required references.</returns>
        public IEnumerable<ValidationResult> Validate(ValidationContext context)
        {
            var results = new List<ValidationResult>();
            var enabledScenePaths = GetEnabledBuildScenePaths();

            if (enabledScenePaths.Count == 0)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Error,
                    Name,
                    "No enabled build scenes were found. Required references cannot be validated."
                ));

                return results;
            }

            var validatedScenes = 0;
            var checkedRequiredReferences = 0;

            foreach (var scenePath in enabledScenePaths)
            {
                checkedRequiredReferences += ValidateScene(scenePath, results, out var sceneWasValidated);

                if (sceneWasValidated)
                    validatedScenes++;
            }

            if (validatedScenes == 0)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Warning,
                    Name,
                    "No BrainIn scene was found among enabled build scenes. Required reference validation was skipped."
                ));

                return results;
            }

            if (checkedRequiredReferences == 0)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Info,
                    Name,
                    "No fields marked with RequiredReference were found in enabled BrainIn scenes."
                ));

                return results;
            }

            if (results.Count == 0)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Info,
                    Name,
                    "All required references are assigned."
                ));
            }

            return results;
        }

        /// <summary>
        /// Validates one enabled build scene.
        /// </summary>
        /// <param name="scenePath">Unity asset path of the scene.</param>
        /// <param name="results">Validation results collection.</param>
        /// <param name="sceneWasValidated">True if the scene contained BrainIn root object.</param>
        /// <returns>Number of checked required references.</returns>
        private int ValidateScene(
            string scenePath,
            List<ValidationResult> results,
            out bool sceneWasValidated)
        {
            sceneWasValidated = false;

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
                    $"Could not open scene for required reference validation: {exception.Message}",
                    scenePath
                ));

                return 0;
            }

            try
            {
                if (!ContainsBrainInRoot(scene))
                    return 0;

                sceneWasValidated = true;

                return GetSceneComponents<Component>(scene)
                    .Where(component => component != null)
                    .Sum(component => ValidateComponentRequiredReferences(component, results, scenePath));
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
        /// Validates required references declared on one component.
        /// </summary>
        /// <param name="component">Component to validate.</param>
        /// <param name="results">Validation results collection.</param>
        /// <param name="scenePath">Unity asset path of the scene.</param>
        /// <returns>Number of checked required fields.</returns>
        private int ValidateComponentRequiredReferences(
            Component component,
            List<ValidationResult> results,
            string scenePath)
        {
            var requiredFields = GetRequiredReferenceFields(component.GetType()).ToList();

            if (requiredFields.Count == 0)
                return 0;

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
                    $"Could not inspect serialized fields on component '{component.GetType().Name}': {exception.Message}",
                    scenePath
                ));

                return 0;
            }

            foreach (var field in requiredFields)
            {
                ValidateRequiredField(component, serializedObject, field, results, scenePath);
            }

            return requiredFields.Count;
        }

        /// <summary>
        /// Validates one required field.
        /// </summary>
        /// <param name="component">Component containing the field.</param>
        /// <param name="serializedObject">Serialized representation of the component.</param>
        /// <param name="field">Field marked as required.</param>
        /// <param name="results">Validation results collection.</param>
        /// <param name="scenePath">Unity asset path of the scene.</param>
        private void ValidateRequiredField(
            Component component,
            SerializedObject serializedObject,
            FieldInfo field,
            List<ValidationResult> results,
            string scenePath)
        {
            if (!typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType))
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Warning,
                    Name,
                    $"Field '{component.GetType().Name}.{field.Name}' is marked with RequiredReference but is not a Unity object reference.",
                    scenePath
                ));

                return;
            }

            var property = serializedObject.FindProperty(field.Name);

            if (property == null)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Warning,
                    Name,
                    $"Field '{component.GetType().Name}.{field.Name}' is marked with RequiredReference but is not serialized by Unity. Add SerializeField or make the field public.",
                    scenePath
                ));

                return;
            }

            if (property.propertyType != SerializedPropertyType.ObjectReference)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Warning,
                    Name,
                    $"Field '{component.GetType().Name}.{field.Name}' is marked with RequiredReference but is not serialized as an object reference.",
                    scenePath
                ));

                return;
            }

            if (property.objectReferenceValue != null)
                return;

            results.Add(new ValidationResult(
                ValidationSeverity.Error,
                Name,
                $"Required reference '{component.GetType().Name}.{field.Name}' is not assigned on '{GetGameObjectPath(component.gameObject)}'.",
                scenePath
            ));
        }

        /// <summary>
        /// Gets fields marked with RequiredReferenceAttribute from the whole type hierarchy.
        /// </summary>
        /// <param name="type">Component type to inspect.</param>
        /// <returns>Fields marked as required references.</returns>
        private static IEnumerable<FieldInfo> GetRequiredReferenceFields(Type type)
        {
            var current = type;

            while (current != null && current != typeof(MonoBehaviour))
            {
                var fields = current.GetFields(
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly
                );

                foreach (var field in fields)
                {
                    if (HasRequiredReferenceAttribute(field))
                        yield return field;
                }

                current = current.BaseType;
            }
        }

        /// <summary>
        /// Determines whether a field has a RequiredReferenceAttribute.
        /// The check uses the attribute name to keep the editor rule independent from the runtime assembly.
        /// </summary>
        /// <param name="field">Field to inspect.</param>
        /// <returns>True if the field is marked as required; otherwise false.</returns>
        private static bool HasRequiredReferenceAttribute(FieldInfo field)
        {
            return field
                .GetCustomAttributes(false)
                .Any(attribute => attribute.GetType().Name == RequiredReferenceAttributeName);
        }

        /// <summary>
        /// Determines whether the scene contains a BrainIn root component.
        /// </summary>
        /// <param name="scene">Scene to inspect.</param>
        /// <returns>True if BrainIn root exists; otherwise false.</returns>
        private static bool ContainsBrainInRoot(Scene scene)
        {
            return GetSceneComponents<Component>(scene)
                .Any(component =>
                    component &&
                    component.GetType().FullName == BrainInComponentFullName);
        }

        /// <summary>
        /// Gets components from root objects in a scene, including inactive objects.
        /// </summary>
        /// <param name="scene">Scene to inspect.</param>
        /// <typeparam name="T">Component type.</typeparam>
        /// <returns>Components found in the scene.</returns>
        private static IEnumerable<T> GetSceneComponents<T>(Scene scene)
            where T : Component
        {
            if (!scene.IsValid() || !scene.isLoaded)
                yield break;

            foreach (var rootGameObject in scene.GetRootGameObjects())
            {
                foreach (var component in rootGameObject.GetComponentsInChildren<T>(true))
                {
                    if (component)
                        yield return component;
                }
            }
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