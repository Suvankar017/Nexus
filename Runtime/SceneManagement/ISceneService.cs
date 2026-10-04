using System;
using UnityEngine;
using Nexus.Services;

namespace Nexus.SceneManagement
{
    /// <summary>
    /// Centralized scene loading. Wraps <see cref="UnityEngine.SceneManagement.SceneManager"/> so
    /// game code doesn't call it directly — this is where duplicate-load prevention, Build
    /// Settings validation, and load/unload notifications live.<br/><br/>
    ///
    /// There is intentionally no synchronous unload and no operation cancellation: Unity does not
    /// support either for scene unloading/loading (unload is always asynchronous under the hood,
    /// and an in-flight <see cref="AsyncOperation"/> scene load cannot be aborted), so this
    /// interface doesn't pretend otherwise.
    /// </summary>
    public interface ISceneService : IGameService
    {
        string ActiveSceneName { get; }

        bool IsLoaded(string sceneName);

        /// <summary>
        /// True if a load for this exact scene name was started through this service and
        /// has not finished yet.
        /// </summary>
        bool IsLoadInProgress(string sceneName);

        /// <summary>
        /// Synchronous single-mode load. Blocks until the scene is loaded and active.
        /// </summary>
        void Load(string sceneName);

        /// <summary>
        /// Starts loading <paramref name="sceneName"/> and returns Unity's own
        /// <see cref="AsyncOperation"/> (progress, isDone, allowSceneActivation, completed all work
        /// as normal). Calling this again for a scene that is already loading through this service
        /// returns the same operation instead of starting a second load.
        /// </summary>
        AsyncOperation LoadAsync(string sceneName, SceneLoadMode mode = SceneLoadMode.Single, bool activateOnLoad = true);

        /// <summary>
        /// Starts unloading an additively-loaded scene. Throws if the scene is not
        /// currently loaded.
        /// </summary>
        AsyncOperation UnloadAsync(string sceneName);

        /// <summary>
        /// Raised whenever any scene finishes loading, including ones loaded outside this
        /// service.
        /// </summary>
        event Action<string> SceneLoaded;

        /// <summary>
        /// Raised whenever any scene finishes unloading, including ones unloaded outside
        /// this service.
        /// </summary>
        event Action<string> SceneUnloaded;
    }
}
