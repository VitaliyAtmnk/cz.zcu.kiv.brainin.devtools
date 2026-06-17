using System;
using System.Collections.Generic;
using System.Linq;
using BrainIn.DevTools.Editor.Validation.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BrainIn.DevTools.Editor.Validation.Rules
{
    /// <summary>
    /// Validates runtime prerequisites required by BrainIn scenes.
    /// </summary>
    public sealed class SceneRuntimePrerequisitesRule : IValidationRule
    {
        private const string BrainInComponentFullName = "BrainInTemplate.Runtime.Code.BrainIn";

        /// <summary>
        /// Gets the display name of the validation rule.
        /// </summary>
        public string Name => "Scene runtime prerequisites validator";

        /// <summary>
        /// Validates enabled build scenes for required Unity runtime objects.
        /// </summary>
        /// <param name="context">Validation context.</param>
        /// <returns>Validation results describing missing runtime prerequisites.</returns>
        public IEnumerable<ValidationResult> Validate(ValidationContext context)
        {
            var results = new List<ValidationResult>();
            var enabledScenePaths = GetEnabledBuildScenePaths();

            if (enabledScenePaths.Count == 0)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Error,
                    Name,
                    "No enabled build scenes were found. Runtime scene prerequisites cannot be validated."
                ));

                return results;
            }

            var validatedScenes = 0;

            foreach (var scenePath in enabledScenePaths)
            {
                if (ValidateScene(scenePath, results))
                    validatedScenes++;
            }

            if (validatedScenes == 0)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Warning,
                    Name,
                    "No BrainIn scene was found among enabled build scenes. Runtime prerequisite validation was skipped."
                ));
            }

            if (results.Count == 0)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Info,
                    Name,
                    "Scene runtime prerequisites look valid."
                ));
            }

            return results;
        }

        /// <summary>
        /// Validates one enabled build scene.
        /// </summary>
        /// <param name="scenePath">Unity asset path of the scene.</param>
        /// <param name="results">Validation results collection.</param>
        /// <returns>True if the scene contained a BrainIn root object and was validated.</returns>
        private bool ValidateScene(string scenePath, List<ValidationResult> results)
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
                    $"Could not open scene for runtime prerequisite validation: {exception.Message}",
                    scenePath
                ));

                return false;
            }

            try
            {
                if (!ContainsBrainInRoot(scene))
                    return false;

                ValidateEventSystem(scene, results, scenePath);
                ValidateMainCamera(scene, results, scenePath);
                ValidateCanvases(scene, results, scenePath);

                return true;
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
        /// Validates EventSystem and input module setup.
        /// </summary>
        /// <param name="scene">Scene to validate.</param>
        /// <param name="results">Validation results collection.</param>
        /// <param name="scenePath">Unity asset path of the scene.</param>
        private void ValidateEventSystem(Scene scene, List<ValidationResult> results, string scenePath)
        {
            var eventSystems = GetSceneComponents<EventSystem>(scene).ToList();
            var activeEventSystems = eventSystems
                .Where(IsActiveAndEnabled)
                .ToList();

            switch (activeEventSystems.Count)
            {
                case 0:
                    results.Add(new ValidationResult(
                        ValidationSeverity.Error,
                        Name,
                        "BrainIn scene does not contain an active EventSystem. BrainIn pointer and UI click controllers require EventSystem.current.\nAdd it to hierarchy -> + -> UI (Canvas) -> Event System",
                        scenePath
                    ));

                    return;
                case > 1:
                {
                    var paths = string.Join(
                        ", ",
                        activeEventSystems.Select(eventSystem => GetGameObjectPath(eventSystem.gameObject))
                    );

                    results.Add(new ValidationResult(
                        ValidationSeverity.Warning,
                        Name,
                        $"Scene contains multiple active EventSystems. Usually only one EventSystem should be active. Found: {paths}.",
                        scenePath
                    ));
                    break;
                }
            }

            results.AddRange(from eventSystem in activeEventSystems
                let activeInputModules = eventSystem.GetComponents<BaseInputModule>()
                    .Where(IsActiveAndEnabled)
                    .ToList()
                where activeInputModules.Count <= 0
                select new ValidationResult(ValidationSeverity.Error, Name,
                    $"EventSystem '{GetGameObjectPath(eventSystem.gameObject)}' does not have an active input module. Add StandaloneInputModule or InputSystemUIInputModule.",
                    scenePath));
        }

        /// <summary>
        /// Validates whether the scene has an active MainCamera.
        /// </summary>
        /// <param name="scene">Scene to validate.</param>
        /// <param name="results">Validation results collection.</param>
        /// <param name="scenePath">Unity asset path of the scene.</param>
        private void ValidateMainCamera(Scene scene, List<ValidationResult> results, string scenePath)
        {
            var mainCameras = GetSceneComponents<Camera>(scene)
                .Where(IsActiveAndEnabled)
                .Where(camera => camera.CompareTag("MainCamera"))
                .ToList();

            if (mainCameras.Count == 0)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Error,
                    Name,
                    "BrainIn scene does not contain an active Camera tagged 'MainCamera'. BrainIn pointer controllers may rely on Camera.main.",
                    scenePath
                ));

                return;
            }

            if (mainCameras.Count <= 1)
                return;

            var paths = string.Join(
                ", ",
                mainCameras.Select(camera => GetGameObjectPath(camera.gameObject))
            );

            results.Add(new ValidationResult(
                ValidationSeverity.Warning,
                Name,
                $"Scene contains multiple active cameras tagged 'MainCamera'. Camera.main may be ambiguous. Found: {paths}.",
                scenePath
            ));
        }

        /// <summary>
        /// Validates Canvas setup needed for UI raycasting.
        /// </summary>
        /// <param name="scene">Scene to validate.</param>
        /// <param name="results">Validation results collection.</param>
        /// <param name="scenePath">Unity asset path of the scene.</param>
        private void ValidateCanvases(Scene scene, List<ValidationResult> results, string scenePath)
        {
            var canvases = GetSceneComponents<Canvas>(scene)
                .Where(IsActiveAndEnabled)
                .ToList();

            if (canvases.Count == 0)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Warning,
                    Name,
                    "BrainIn scene does not contain an active Canvas. This is valid only for non-UI tasks.",
                    scenePath
                ));

                return;
            }

            foreach (var canvas in canvases)
            {
                ValidateCanvasGraphicRaycaster(canvas, results, scenePath);
                ValidateCanvasCamera(canvas, results, scenePath);
            }
        }

        /// <summary>
        /// Validates GraphicRaycaster presence on canvases that contain interactive UI elements.
        /// Decorative canvases, such as cursor canvases, do not need a GraphicRaycaster.
        /// </summary>
        /// <param name="canvas">Canvas to validate.</param>
        /// <param name="results">Validation results collection.</param>
        /// <param name="scenePath">Unity asset path of the scene.</param>
        private void ValidateCanvasGraphicRaycaster(Canvas canvas, List<ValidationResult> results, string scenePath)
        {
            if (!CanvasContainsInteractiveUi(canvas))
                return;

            var graphicRaycaster = canvas.GetComponent<GraphicRaycaster>();

            if (graphicRaycaster != null && graphicRaycaster.enabled)
                return;

            results.Add(new ValidationResult(
                ValidationSeverity.Warning,
                Name,
                $"Canvas '{GetGameObjectPath(canvas.gameObject)}' contains interactive UI elements but does not have an enabled GraphicRaycaster. UI elements on this Canvas may not be detected by BrainIn click tracking.",
                scenePath
            ));
        }

        /// <summary>
        /// Validates camera assignment for camera-based Canvas render modes.
        /// </summary>
        /// <param name="canvas">Canvas to validate.</param>
        /// <param name="results">Validation results collection.</param>
        /// <param name="scenePath">Unity asset path of the scene.</param>
        private void ValidateCanvasCamera(Canvas canvas, List<ValidationResult> results, string scenePath)
        {
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                return;

            if (canvas.worldCamera != null)
                return;

            results.Add(new ValidationResult(
                ValidationSeverity.Warning,
                Name,
                $"Canvas '{GetGameObjectPath(canvas.gameObject)}' uses render mode '{canvas.renderMode}' but has no world camera assigned.",
                scenePath
            ));
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
                    if (component != null)
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
        /// Determines whether a behaviour is active and enabled.
        /// </summary>
        /// <param name="behaviour">Behaviour to inspect.</param>
        /// <returns>True if active and enabled; otherwise false.</returns>
        private static bool IsActiveAndEnabled(Behaviour behaviour)
        {
            return behaviour != null &&
                   behaviour.enabled &&
                   behaviour.gameObject.activeInHierarchy;
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
        /// Determines whether a canvas contains UI elements that should participate in UI raycasting.
        /// </summary>
        /// <param name="canvas">Canvas to inspect.</param>
        /// <returns>True if the canvas contains interactive UI; otherwise false.</returns>
        private static bool CanvasContainsInteractiveUi(Canvas canvas)
        {
            if (canvas == null)
                return false;

            if (canvas.GetComponentsInChildren<Selectable>(true).Any(IsActiveAndEnabled))
                return true;

            return canvas
                .GetComponentsInChildren<Component>(true)
                .Any(IsActivePointerController);
        }

        /// <summary>
        /// Determines whether a component is an active BrainIn pointer controller.
        /// Reflection by type name is used to avoid a compile-time dependency on the BrainIn runtime assembly.
        /// </summary>
        /// <param name="component">Component to inspect.</param>
        /// <returns>True if the component appears to be an active BrainIn pointer controller; otherwise false.</returns>
        private static bool IsActivePointerController(Component component)
        {
            if (component == null || !component.gameObject.activeInHierarchy)
                return false;

            if (component is Behaviour behaviour && !behaviour.enabled)
                return false;

            var type = component.GetType();

            while (type != null)
            {
                if (type.Name == "PointerController")
                    return true;

                type = type.BaseType;
            }

            return false;
        }
    }
}