using System;

namespace MS2026.ShaderFX
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class EffectModuleInfoAttribute : Attribute
    {
        public string DisplayName { get; }
        public string Category { get; }

        // Japanese explanation of what the effect does, shown in the Editor when the module
        // is expanded. Every module in this package sets this; it's optional only so
        // third-party modules aren't forced to.
        public string Description { get; set; }

        public EffectModuleInfoAttribute(string displayName, string category = "General")
        {
            DisplayName = displayName;
            Category = category;
        }
    }
}
