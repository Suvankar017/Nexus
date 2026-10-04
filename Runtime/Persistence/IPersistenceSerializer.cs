namespace Nexus.Persistence
{
    /// <summary>
    /// Converts between a data object and its stored text form. Separate from
    /// <see cref="IPersistenceStorage"/> so either can change independently.
    /// </summary>
    public interface IPersistenceSerializer
    {
        string Serialize<TData>(TData data);

        TData Deserialize<TData>(string serialized);
    }
}
