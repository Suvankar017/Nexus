namespace Nexus.Persistence
{
    /// <summary>
    /// Raw key → text storage, with no knowledge of what the text means. Swappable independently
    /// of <see cref="IPersistenceSerializer"/> — a file-backed implementation and an in-memory one
    /// (for tests) are provided; a future cloud-backed implementation would only need to implement
    /// this interface.
    /// </summary>
    public interface IPersistenceStorage
    {
        bool Exists(string key);

        /// <summary>
        /// Returns the stored text for <paramref name="key"/>, or null if it doesn't exist.
        /// </summary>
        string ReadText(string key);

        void WriteText(string key, string contents);

        void Delete(string key);
    }
}
