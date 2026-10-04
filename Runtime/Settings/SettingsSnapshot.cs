using System;
using System.Collections.Generic;

namespace Nexus.Settings
{
    /// <summary>
    /// The persisted shape of all settings. Parallel lists rather than a Dictionary because
    /// JsonUtility (see <see cref="Persistence.JSONPersistenceSerializer"/>) cannot serialize
    /// Dictionary; each value is stored as text via its <see cref="SettingsService"/> entry's own
    /// to/from-string conversion, keeping this type itself trivially serializable regardless of
    /// how many different setting value types are registered.
    /// </summary>
    [Serializable]
    internal sealed class SettingsSnapshot
    {
        public List<string> Keys = new();
        public List<string> Values = new();
    }
}
