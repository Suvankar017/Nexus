using System;

namespace Nexus.Settings
{
    /// <summary>
    /// Describes a setting — its key, default value, and optional validity check — separately
    /// from whatever its current value is at runtime. Supported <typeparamref name="TValue"/>
    /// types: <see cref="bool"/>, <see cref="int"/>, <see cref="float"/>, <see cref="string"/>,
    /// and enums.
    /// </summary>
    public readonly struct SettingDefinition<TValue>
    {
        public readonly string Key;
        public readonly TValue DefaultValue;
        public readonly string Category;
        public readonly Func<TValue, bool> Validator;

        /// <param name="validator">
        /// Returns true if a value is acceptable; null means any value of
        /// <typeparamref name="TValue"/> is acceptable. The default value must satisfy it.
        /// </param>
        /// <param name="category">
        /// Free-form grouping label (e.g. "Audio") for a settings UI to key
        /// off of — the framework doesn't enforce or predefine any category names.
        /// </param>
        public SettingDefinition(string key, TValue defaultValue, Func<TValue, bool> validator = null, string category = null)
        {
            Key = key;
            DefaultValue = defaultValue;
            Validator = validator;
            Category = category;
        }
    }
}
