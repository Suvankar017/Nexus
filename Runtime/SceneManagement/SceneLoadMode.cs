namespace Nexus.SceneManagement
{
    public enum SceneLoadMode
    {
        /// <summary>
        /// Unloads every other loaded scene as this one loads.
        /// </summary>
        Single,

        /// <summary>
        /// Loads alongside whatever scenes are already loaded.
        /// </summary>
        Additive
    }
}
