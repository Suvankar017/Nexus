namespace Nexus.Persistence
{
    /// <summary>
    /// One step in a save-data version chain, registered via
    /// <see cref="IPersistenceService.RegisterMigration"/>. Deliberately type-erased at this level
    /// (it operates on the raw serialized payload text, not a specific data type) so
    /// <see cref="IPersistenceService"/> doesn't need to know every version's shape — a concrete
    /// migration typically deserializes <paramref name="payload"/> with a small "shape" type
    /// representing the old version and re-serializes the result in the new version's shape.
    /// Must be a pure, deterministic function of its input for migrations to be testable in
    /// isolation and safely re-run.
    /// </summary>
    public interface ISaveMigration
    {
        int FromVersion { get; }
        int ToVersion { get; }

        string Migrate(string payload);
    }
}
