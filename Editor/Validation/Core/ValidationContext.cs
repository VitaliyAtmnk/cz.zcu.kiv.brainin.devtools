namespace BrainIn.DevTools.Editor.Validation.Core
{
    /// <summary>
    /// Provides shared project-level information for validation rules.
    /// </summary>
    public sealed class ValidationContext
    {
        /// <summary>
        /// Gets the absolute path to the root directory of the Unity project.
        /// </summary>
        public string ProjectPath { get; private set; }

        /// <summary>
        /// Gets the absolute path to the Unity project's Assets directory.
        /// </summary>
        public string AssetsPath { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationContext"/> class.
        /// </summary>
        /// <param name="projectPath">Absolute path to the Unity project root directory.</param>
        /// <param name="assetsPath">Absolute path to the Unity project's Assets directory.</param>
        public ValidationContext(string projectPath, string assetsPath)
        {
            ProjectPath = projectPath;
            AssetsPath = assetsPath;
        }
    }
}