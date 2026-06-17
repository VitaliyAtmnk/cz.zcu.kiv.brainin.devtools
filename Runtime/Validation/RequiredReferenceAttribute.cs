using System;

namespace BrainIn.DevTools.Validation
{
    /// <summary>
    /// Marks a serialized Unity object reference as required for validation.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class RequiredReferenceAttribute : Attribute
    {
    }
}