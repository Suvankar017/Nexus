using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Nexus.Services;
using Nexus.Core.Validation;

namespace Nexus.SceneManagement
{
    public sealed class SceneService : ISceneService
    {
        private readonly Dictionary<string, AsyncOperation> _inFlightLoads =
            new(StringComparer.Ordinal);

        public string ActiveSceneName => SceneManager.GetActiveScene().name;

        public event Action<string> SceneLoaded;
        public event Action<string> SceneUnloaded;

        public void Initialize(IServiceRegistry registry)
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        public void Shutdown()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            _inFlightLoads.Clear();
        }

        public bool IsLoaded(string sceneName)
        {
            Guard.NotNullOrEmpty(sceneName, nameof(sceneName));
            Scene scene = SceneManager.GetSceneByName(sceneName);
            return scene.IsValid() && scene.isLoaded;
        }

        public bool IsLoadInProgress(string sceneName)
        {
            Guard.NotNullOrEmpty(sceneName, nameof(sceneName));
            return _inFlightLoads.ContainsKey(sceneName);
        }

        public void Load(string sceneName)
        {
            Guard.NotNullOrEmpty(sceneName, nameof(sceneName));
            EnsureSceneExists(sceneName);
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }

        public AsyncOperation LoadAsync(
            string sceneName,
            SceneLoadMode mode = SceneLoadMode.Single,
            bool activateOnLoad = true)
        {
            Guard.NotNullOrEmpty(sceneName, nameof(sceneName));

            if (_inFlightLoads.TryGetValue(sceneName, out AsyncOperation existing))
            {
                return existing;
            }

            EnsureSceneExists(sceneName);

            LoadSceneMode unityMode = mode == SceneLoadMode.Additive ? LoadSceneMode.Additive : LoadSceneMode.Single;
            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, unityMode);
            operation.allowSceneActivation = activateOnLoad;

            _inFlightLoads[sceneName] = operation;
            return operation;
        }

        public AsyncOperation UnloadAsync(string sceneName)
        {
            Guard.NotNullOrEmpty(sceneName, nameof(sceneName));

            Scene scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                throw new InvalidOperationException($"Scene '{sceneName}' is not currently loaded.");
            }

            return SceneManager.UnloadSceneAsync(sceneName);
        }

        private static void EnsureSceneExists(string sceneName)
        {
            int count = SceneManager.sceneCountInBuildSettings;
            for (int i = 0; i < count; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                string name = Path.GetFileNameWithoutExtension(path);
                if (string.Equals(name, sceneName, StringComparison.Ordinal))
                {
                    return;
                }
            }

            throw new SceneNotFoundException(sceneName);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _inFlightLoads.Remove(scene.name);
            SceneLoaded?.Invoke(scene.name);
        }

        private void OnSceneUnloaded(Scene scene)
        {
            SceneUnloaded?.Invoke(scene.name);
        }
    }
}
