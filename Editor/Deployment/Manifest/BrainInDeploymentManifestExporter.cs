using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace BrainIn.DevTools.Editor.Deployment.Manifest
{
    /// <summary>
    /// Exports BrainIn deployment manifests to JSON files.
    /// </summary>
    public static class BrainInDeploymentManifestExporter
    {
        /// <summary>
        /// Creates a default deployment manifest file name.
        /// </summary>
        /// <returns>Default JSON file name.</returns>
        public static string CreateDefaultFileName()
        {
            return $"BrainInDeploymentManifest_{DateTime.Now:yyyyMMdd_HHmmss}.json";
        }

        /// <summary>
        /// Exports the deployment manifest to a JSON file.
        /// </summary>
        /// <param name="manifest">Deployment manifest.</param>
        /// <param name="filePath">Target file path.</param>
        public static void Export(BrainInDeploymentManifest manifest, string filePath)
        {
            if (manifest == null)
                throw new ArgumentNullException(nameof(manifest));

            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Export file path cannot be empty.", nameof(filePath));

            var directory = Path.GetDirectoryName(filePath);

            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                NullValueHandling = NullValueHandling.Include
            };

            var json = JsonConvert.SerializeObject(manifest, settings);
            File.WriteAllText(filePath, json);
        }
    }
}