using Nexus.Services;

namespace Nexus.Persistence
{
    /// <summary>
    /// Save/load for domain-specific data classes the game defines (e.g. <c>PlayerProgressData</c>,
    /// <c>SettingsData</c>) — never for arbitrary Unity objects or whole scenes; see
    /// <c>JsonPersistenceSerializer</c>'s constraints. Save/Load are synchronous (the underlying
    /// JSON serialization and file I/O are synchronous) — there is no SaveAsync/LoadAsync, since
    /// wrapping a synchronous implementation in a Task/coroutine would only pretend to be
    /// asynchronous.
    /// </summary>
    public interface IPersistenceService : IGameService
    {
        bool Exists(string key);

        /// <summary>
        /// Serializes and writes <paramref name="data"/> under <paramref name="key"/>,
        /// tagged with <paramref name="version"/>.
        /// </summary>
        void Save<TData>(string key, TData data, int version);

        /// <summary>
        /// Loads the data stored under <paramref name="key"/>. Returns <paramref name="defaultValue"/>
        /// if nothing is stored yet. If the stored version is older than
        /// <paramref name="currentVersion"/>, walks the registered <see cref="ISaveMigration"/>
        /// chain for <paramref name="key"/> up to it; if no migration bridges some version in that
        /// chain, or any step (including the final deserialize) throws, the failure is logged and
        /// <paramref name="defaultValue"/> is returned — the corrupt/unmigratable raw data is kept
        /// under <c>"{key}.corrupt"</c> rather than being discarded.
        /// </summary>
        TData Load<TData>(string key, int currentVersion, TData defaultValue);

        /// <summary>
        /// Deletes the data stored under <paramref name="key"/>, if any. Also the
        /// mechanism for "reset to defaults" — the next <see cref="Load{TData}"/> for that key
        /// naturally returns its default.
        /// </summary>
        void Delete(string key);

        /// <summary>
        /// Registers one version-chain step for <paramref name="key"/>. Throws if a
        /// migration from the same <see cref="ISaveMigration.FromVersion"/> is already registered
        /// for that key.
        /// </summary>
        void RegisterMigration(string key, ISaveMigration migration);
    }
}
