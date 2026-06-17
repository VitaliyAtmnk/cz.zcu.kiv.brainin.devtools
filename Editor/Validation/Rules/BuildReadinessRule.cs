using System.Collections.Generic;
using System.IO;
using BrainIn.DevTools.Editor.Validation.Core;
using UnityEditor;
using UnityEngine;

namespace BrainIn.DevTools.Editor.Validation.Rules
{
    public sealed class BuildReadinessRule : IValidationRule
    {
        public string Name => "Build readiness validator";

        public IEnumerable<ValidationResult> Validate(ValidationContext context)
        {
            var results = new List<ValidationResult>();

            ValidateBuildTarget(results);
            ValidateBuildScenes(results);
            ValidateProductName(results);

            if (results.Count == 0)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Info,
                    Name,
                    "Project build settings look ready for a BrainIn WebGL build."
                ));
            }

            return results;
        }

        private void ValidateBuildTarget(List<ValidationResult> results)
        {
            var activeBuildTarget = EditorUserBuildSettings.activeBuildTarget;

            if (activeBuildTarget != BuildTarget.WebGL)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Error,
                    Name,
                    $"Active build target is '{activeBuildTarget}', but BrainIn tasks should be built for WebGL. Switch platform to WebGL before building."
                ));
            }
        }

        private void ValidateBuildScenes(List<ValidationResult> results)
        {
            var scenes = EditorBuildSettings.scenes;

            if (scenes == null || scenes.Length == 0)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Error,
                    Name,
                    "No scenes are configured in Build Settings. Add at least one scene to the build."
                ));

                return;
            }

            var enabledSceneCount = 0;

            foreach (var scene in scenes)
            {
                if (scene == null)
                {
                    results.Add(new ValidationResult(
                        ValidationSeverity.Error,
                        Name,
                        "Build Settings contain a null scene entry."
                    ));

                    continue;
                }

                if (string.IsNullOrWhiteSpace(scene.path))
                {
                    results.Add(new ValidationResult(
                        ValidationSeverity.Error,
                        Name,
                        "Build Settings contain a scene with an empty path."
                    ));

                    continue;
                }

                if (!scene.path.EndsWith(".unity"))
                {
                    results.Add(new ValidationResult(
                        ValidationSeverity.Warning,
                        Name,
                        $"Build Settings contain an unusual scene path: {scene.path}",
                        scene.path
                    ));
                }

                if (scene.enabled)
                    enabledSceneCount++;

                if (!SceneFileExists(scene.path))
                {
                    results.Add(new ValidationResult(
                        ValidationSeverity.Error,
                        Name,
                        $"Scene configured in Build Settings does not exist: {scene.path}",
                        scene.path
                    ));
                }
            }

            if (enabledSceneCount == 0)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Error,
                    Name,
                    "No enabled scenes found in Build Settings. Enable at least one scene before building."
                ));
            }
        }

        private void ValidateProductName(List<ValidationResult> results)
        {
            if (string.IsNullOrWhiteSpace(PlayerSettings.productName))
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Warning,
                    Name,
                    "PlayerSettings.productName is empty. Set a meaningful task/game name before building."
                ));
            }
        }

        private static bool SceneFileExists(string sceneAssetPath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;

            if (string.IsNullOrWhiteSpace(projectRoot))
                return false;

            var fullPath = Path.Combine(projectRoot, sceneAssetPath);
            return File.Exists(fullPath);
        }
    }
}