using System;
using UnityEngine;

// Mm-StatSystem uses a small subset of Odin's runtime-facing API for
// ScriptableObject base types and Inspector decoration. The FPS project does
// not depend on Odin, so these no-op types preserve the upstream data model
// without importing Odin's commercial editor/runtime binaries.
namespace Sirenix.OdinInspector
{
    public abstract class SerializedScriptableObject : ScriptableObject
    {
    }

    [AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = true)]
    public sealed class LabelTextAttribute : Attribute
    {
        public LabelTextAttribute(string text)
        {
        }
    }

    [AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = true)]
    public sealed class TitleGroupAttribute : Attribute
    {
        public TitleGroupAttribute(string groupName)
        {
        }
    }

    [AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = true)]
    public sealed class ShowIfAttribute : Attribute
    {
        public ShowIfAttribute(string condition)
        {
        }
    }

    [AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = true)]
    public sealed class InfoBoxAttribute : Attribute
    {
        public InfoBoxAttribute(string message)
        {
        }
    }

    [AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = true)]
    public sealed class MinValueAttribute : Attribute
    {
        public MinValueAttribute(double minimum)
        {
        }
    }
}
