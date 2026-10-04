using System.Collections.Generic;
using Nexus.Services;

namespace Nexus.Settings
{
    /// <summary>
    /// Typed settings storage, persisted through <see cref="Persistence.IPersistenceService"/> —
    /// this service implements no persistence mechanism of its own. Every setting must be
    /// registered (with a default and, optionally, a validator) before it can be read or written.
    /// Changes are observed via <see cref="SettingChangedEvent"/> on <see cref="Events.IEventService"/>,
    /// not a bespoke callback — subscribe to that instead of polling.
    /// </summary>
    public interface ISettingsService : IGameService
    {
        void Register<TValue>(SettingDefinition<TValue> definition);

        TValue Get<TValue>(string key);

        bool TryGet<TValue>(string key, out TValue value);

        /// <summary>
        /// Throws <see cref="System.ArgumentException"/> if the value fails the
        /// setting's validator. A value equal to the current one is a no-op (no change event).
        /// </summary>
        void Set<TValue>(string key, TValue value);

        void ResetToDefault(string key);

        void ResetAllToDefaults();

        IEnumerable<string> GetKeysInCategory(string category);

        /// <summary>
        /// Persists every registered setting's current value. Also called automatically
        /// from Shutdown if any setting changed since the last Save/Load.
        /// </summary>
        void Save();

        /// <summary>
        /// Reloads every registered setting's value from storage. Keys with no stored
        /// value keep their default; a stored value that fails validation is discarded in favor
        /// of the current value; an unrecognized stored key (removed since the save was written)
        /// is ignored.
        /// </summary>
        void Load();
    }
}
