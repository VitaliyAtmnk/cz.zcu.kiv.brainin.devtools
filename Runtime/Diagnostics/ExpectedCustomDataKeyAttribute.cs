using System;

namespace BrainIn.DevTools.Diagnostics
{
    /// <summary>
    /// Marks a field or property as an expected BrainIn customData output key.
    /// When no key is provided, the key is derived from the field or property name.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true, Inherited = true)]
    public sealed class ExpectedCustomDataKeyAttribute : Attribute
    {
        /// <summary>
        /// Creates an expected customData key declaration.
        /// The key is derived from the field or property name.
        /// </summary>
        public ExpectedCustomDataKeyAttribute()
        {
        }

        /// <summary>
        /// Creates an expected customData key declaration with an explicit key name.
        /// </summary>
        /// <param name="key">Expected customData key name.</param>
        public ExpectedCustomDataKeyAttribute(string key)
        {
            Key = key;
        }

        /// <summary>
        /// Gets the explicit expected customData key name.
        /// When empty, the key is derived from the field or property name.
        /// </summary>
        public string Key { get; }
    }
}