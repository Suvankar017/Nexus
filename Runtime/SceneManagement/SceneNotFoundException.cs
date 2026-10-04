using System;

namespace Nexus.SceneManagement
{
    /// <summary>
    /// Thrown when a requested scene name is not present in the project's Build Settings scene
    /// list.
    /// </summary>
    public sealed class SceneNotFoundException : Exception
    {
        public SceneNotFoundException(string sceneName)
            : base($"Scene '{sceneName}' is not in the Build Settings scene list.")
        {
        }
    }
}
