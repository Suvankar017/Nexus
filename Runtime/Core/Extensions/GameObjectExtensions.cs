using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nexus.Core.Extensions
{
    /// <summary>
    /// Production-ready extension methods for <see cref="GameObject"/> covering component
    /// retrieval, activation state, tag/layer queries and child hierarchy management.
    /// <para>
    /// Because <see cref="GameObject"/> inherits directly from <see cref="UnityEngine.Object"/>
    /// (not <see cref="Component"/>), the null-safety and destruction helpers in
    /// <c>ObjectExtensions</c> already apply to any <see cref="GameObject"/> reference
    /// (<see cref="ObjectExtensions.IsAlive(UnityEngine.Object)"/>,
    /// <see cref="ObjectExtensions.DestroySafe(UnityEngine.Object, bool)"/>). This class instead
    /// covers operations unique to a <see cref="GameObject"/> as a container: finding/adding its
    /// components, toggling its active state, and navigating its child hierarchy — many APIs
    /// (<see cref="GameObject.Find(string)"/>, <see cref="Physics.Raycast(Ray, out RaycastHit)"/>
    /// hit results) hand back a <see cref="GameObject"/> rather than a specific
    /// <see cref="Component"/>, making these the first point of contact in gameplay code.
    /// </para>
    /// <para>Design rules:</para>
    /// <list type="bullet">
    ///     <item>Component retrieval never throws on a missing component; it returns null, a fallback, or logs a clear error for hard dependencies, matching Unity's own permissive contract.</item>
    ///     <item>Activation helpers skip redundant <see cref="GameObject.SetActive(bool)"/> calls when the state already matches, avoiding unnecessary internal Unity bookkeeping in per-frame toggles.</item>
    ///     <item>Hierarchy helpers that can create new objects (<c>GetOrCreateChild</c>) are clearly named to distinguish them from pure lookups.</item>
    ///     <item>Where an equivalent already exists on <see cref="Transform"/> or <see cref="Component"/>, this class provides a thin, explicitly documented forwarding wrapper rather than duplicating logic.</item>
    ///     <item>Zero allocation on every member except explicit child-creation and debug-string helpers, which allocate by necessity.</item>
    /// </list>
    /// </summary>
    public static class GameObjectExtensions
    {
        #region Component Retrieval

        /// <summary>
        /// Returns the existing component of type <typeparamref name="T"/> on this GameObject, 
        /// or attaches and returns a new one if it does not exist.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T GetOrAddComponent<T>(this GameObject gameObject) where T : Component
        {
            if (gameObject.TryGetComponent<T>(out var component)) return component;
            return gameObject.AddComponent<T>();
        }

        /// <summary>
        /// Non-generic version of <see cref="GetOrAddComponent{T}(GameObject)"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Component GetOrAddComponent(this GameObject gameObject, Type type)
        {
            if (gameObject.TryGetComponent(type, out var component)) return component;
            return gameObject.AddComponent(type);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T GetOrAddComponent<T>(this GameObject gameObject, Action<T> onAdded) where T : Component
        {
            if (gameObject.TryGetComponent<T>(out var component)) return component;
            component = gameObject.AddComponent<T>();

            try
            {
                onAdded?.Invoke(component);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            return component;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Component GetOrAddComponent(this GameObject gameObject, Type type, Action<Component> onAdded)
        {
            if (gameObject.TryGetComponent(type, out var component))
            {
                return component;
            }

            component = gameObject.AddComponent(type);
            try
            {
                onAdded?.Invoke(component);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            return component;
        }

        /// <summary>
        /// Returns the component of type <typeparamref name="T"/> on <paramref name="go"/>,
        /// logging an error if it is missing instead of allowing a downstream
        /// <see cref="System.NullReferenceException"/>.
        /// <br/>
        /// Intended for validating hard dependencies on objects resolved dynamically (spawned
        /// prefabs, scene queries), so a misconfigured prefab fails loudly at setup time rather
        /// than crashing deep inside an update loop.
        /// </summary>
        /// <typeparam name="T">Required component type.</typeparam>
        /// <param name="go">GameObject to query.</param>
        /// <returns>The found component, or a Unity-null reference if missing.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T RequireComponent<T>(this GameObject go) where T : Component
        {
            T comp = go.GetComponent<T>();
            if (comp.IsUnityNull())
                Debug.LogError($"Required component of type {typeof(T).Name} is missing on \"{go.name}\".");

            return comp;
        }

        /// <summary>
        /// Returns the component of type <typeparamref name="T"/> on <paramref name="go"/>, or
        /// <paramref name="fallback"/> if it is not present.
        /// <br/>
        /// Ideal for optional dependencies where absence is a valid, expected configuration
        /// rather than an error (e.g. an optional <see cref="AudioSource"/> on a decorative prop).
        /// </summary>
        /// <typeparam name="T">Component type to retrieve.</typeparam>
        /// <param name="go">GameObject to query.</param>
        /// <param name="fallback">Value returned when the component is absent.</param>
        /// <returns>The found component, or <paramref name="fallback"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T GetComponentOr<T>(this GameObject go, T fallback) where T : Component
        {
            return go.TryGetComponent(out T comp) && comp.IsAlive() ? comp : fallback;
        }

        /// <summary>
        /// Finds all components of type <typeparamref name="T"/> in children, 
        /// <c>EXCLUDING</c> the calling object's own GameObject.
        /// </summary>
        public static void GetComponentsInChildrenOnly<T>(
            this GameObject gameObject,
            List<T> results,
            bool includeInactive = false) where T : class
        {
            results.Clear();
            Transform transform = gameObject.transform;
            int childCount = transform.childCount;

            for (int i = 0; i < childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (includeInactive || child.gameObject.activeSelf)
                {
                    child.GetComponentsInChildren(includeInactive, results);
                }
            }
        }

        /// <summary>
        /// Finds a component of type <typeparamref name="T"/> in the parent chain, 
        /// <c>EXCLUDING</c> the calling object's own GameObject.
        /// <para>
        /// (Standard Unity <see cref="GameObject.GetComponentInParent{T}()"/> checks self first, 
        /// which often returns unwanted local components).
        /// </para>
        /// </summary>
        public static T GetComponentInParentOnly<T>(this GameObject gameObject, bool includeInactive = false) where T : class
        {
            Transform parent = gameObject.transform.parent;
            return parent == null ? null : parent.GetComponentInParent<T>(includeInactive);
        }

        /// <summary>
        /// Attempts to find a component of type <typeparamref name="T"/> in parents.
        /// </summary>
        public static bool TryGetComponentInParent<T>(this GameObject gameObject, out T result, bool includeInactive = false)
        {
            result = gameObject.GetComponentInParent<T>(includeInactive);
            return result != null;
        }

        /// <summary>
        /// Attempts to find a component of type <typeparamref name="T"/> in children.
        /// </summary>
        public static bool TryGetComponentInChildren<T>(this GameObject gameObject, out T result, bool includeInactive = false)
        {
            result = gameObject.GetComponentInChildren<T>(includeInactive);
            return result != null;
        }

        #endregion

        #region Existence Checks

        /// <summary>
        /// Checks if a component of type <typeparamref name="T"/> exists on this GameObject without throwing or allocating.
        /// </summary>
        public static bool HasComponent<T>(this GameObject gameObject)
        {
            return gameObject.TryGetComponent<T>(out _);
        }

        public static bool HasComponent(this GameObject gameObject, Type type)
        {
            return gameObject.TryGetComponent(type, out _);
        }

        /// <summary>
        /// Returns <c>true</c> if a component of type <typeparamref name="T"/> exists anywhere in
        /// <paramref name="go"/>'s children, including <paramref name="go"/> itself.
        /// <br/>
        /// Useful for structural validation of spawned prefabs, e.g. confirming an equipped
        /// weapon hierarchy contains at least one muzzle-flash emitter before enabling fire VFX.
        /// </summary>
        /// <typeparam name="T">Component type to search for.</typeparam>
        /// <param name="go">Root GameObject to search from.</param>
        /// <returns><c>true</c> if any matching component is found.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasComponentInChildren<T>(this GameObject go) where T : Component
            => go.GetComponentInChildren<T>() != null;

        /// <summary>
        /// Returns <c>true</c> if a component of type <typeparamref name="T"/> exists on
        /// <paramref name="go"/> or any of its ancestors.
        /// <br/>
        /// Common in hit-detection code needing to find an owning <c>Damageable</c> or
        /// <c>Faction</c> component regardless of which child collider's <see cref="GameObject"/>
        /// registered the hit.
        /// </summary>
        /// <typeparam name="T">Component type to search for.</typeparam>
        /// <param name="go">GameObject to search upward from.</param>
        /// <returns><c>true</c> if any matching component is found.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasComponentInParent<T>(this GameObject go) where T : Component
            => go.GetComponentInParent<T>() != null;

        #endregion

        #region Activation

        /// <summary>
        /// Sets <paramref name="go"/>'s active state, guarding against a destroyed reference and
        /// skipping redundant calls when the state already matches.
        /// <br/>
        /// Calling <see cref="GameObject.SetActive(bool)"/> with the same value it already has
        /// still triggers internal Unity bookkeeping (hierarchy change events, callback
        /// dispatch); skipping the redundant call matters when toggled every frame from AI
        /// perception, pooling or UI visibility systems.
        /// </summary>
        /// <param name="go">GameObject to toggle.</param>
        /// <param name="active">Desired active state.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetActiveSafe(this GameObject go, bool active)
        {
            if (go.IsUnityNull()) return;
            if (go.activeSelf != active)
                go.SetActive(active);
        }

        /// <summary>
        /// Activates <paramref name="go"/>, equivalent to <c>SetActiveSafe(true)</c>.
        /// <br/>
        /// Reads more clearly than a boolean literal at call sites that unconditionally show an
        /// object, such as revealing a pooled projectile on spawn.
        /// </summary>
        /// <param name="go">GameObject to activate.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ActivateSafe(this GameObject go)
            => go.SetActiveSafe(true);

        /// <summary>
        /// Deactivates <paramref name="go"/>, equivalent to <c>SetActiveSafe(false)</c>.
        /// <br/>
        /// Reads more clearly than a boolean literal at call sites that unconditionally hide an
        /// object, such as returning a pooled projectile to its pool.
        /// </summary>
        /// <param name="go">GameObject to deactivate.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DeactivateSafe(this GameObject go)
            => go.SetActiveSafe(false);

        /// <summary>
        /// Flips <paramref name="go"/>'s active state and returns the new state.
        /// <br/>
        /// Ideal for single-button visibility toggles (debug overlays, collapsible UI panels)
        /// where the caller does not need to track the previous state itself.
        /// </summary>
        /// <param name="go">GameObject to toggle.</param>
        /// <returns>The resulting active state after the toggle.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ToggleActive(this GameObject go)
        {
            bool newState = !go.activeSelf;
            go.SetActive(newState);
            return newState;
        }

        #endregion

        #region Tag & Layer

        /// <summary>
        /// Safely compares <paramref name="go"/>'s tag against <paramref name="tag"/>, guarding
        /// against a destroyed reference.
        /// <br/>
        /// Prevents <see cref="MissingReferenceException"/> in collision/trigger callbacks where
        /// the other object may already have been destroyed earlier in the same frame (e.g. an
        /// explosion destroying multiple overlapping objects in sequence).
        /// </summary>
        /// <param name="go">GameObject to test.</param>
        /// <param name="tag">Tag to compare against.</param>
        /// <returns><c>true</c> if <paramref name="go"/> is alive and its tag matches <paramref name="tag"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CompareTagSafe(this GameObject go, string tag)
            => !go.IsUnityNull() && go.CompareTag(tag);

        /// <summary>
        /// Returns <c>true</c> if <paramref name="go"/>'s layer is included in <paramref name="mask"/>.
        /// <br/>
        /// Standard bitwise check for inspector-driven <see cref="LayerMask"/> fields used in
        /// aggro detection, raycast filtering and area-of-effect queries — avoids fragile manual
        /// <c>go.layer == someInt</c> comparisons.
        /// </summary>
        /// <param name="go">GameObject to test.</param>
        /// <param name="mask">Layer mask to test membership against.</param>
        /// <returns><c>true</c> if the object's layer bit is set in <paramref name="mask"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsInLayerMask(this GameObject go, LayerMask mask)
            => (mask.value & (1 << go.layer)) != 0;

        /// <summary>
        /// Recursively changes the layer of this GameObject and all of its child transforms.
        /// </summary>
        public static void SetLayerRecursively(this GameObject root, int layer)
        {
            root.layer = layer;
            var transform = root.transform;
            int childCount = transform.childCount;

            for (int i = 0; i < childCount; i++)
            {
                transform.GetChild(i).gameObject.SetLayerRecursively(layer);
            }
        }

        #endregion

        #region Hierarchy

        /// <summary>
        /// Returns the immediate child <see cref="GameObject"/> of <paramref name="go"/> with the
        /// given <paramref name="name"/>, creating and returning a new empty child if none exists.
        /// <br/>
        /// The standard pattern for procedurally building predictable sub-hierarchies (a
        /// "VFX Sockets" or "UI Containers" grouping node) that should be created once and reused
        /// on subsequent calls rather than duplicated every time.
        /// </summary>
        /// <param name="go">Parent GameObject to search or create under.</param>
        /// <param name="name">Name of the child to find or create.</param>
        /// <returns>The existing or newly created child GameObject.</returns>
        public static GameObject GetOrCreateChild(this GameObject go, string name)
        {
            Transform existing = go.transform.FindDirectChild(name);
            if (existing != null)
                return existing.gameObject;

            GameObject child = new(name);
            child.transform.SetParent(go.transform, false);
            return child;
        }

        /// <summary>
        /// Returns the first descendant <see cref="GameObject"/> of <paramref name="go"/> with the
        /// given <paramref name="name"/>, searching depth-first through the entire hierarchy.
        /// <br/>
        /// Forwards to <see cref="TransformExtensions.FindChildRecursive(Transform, string)"/>,
        /// exposed here as a <see cref="GameObject"/> result since spawned prefab references and
        /// scene queries are typically handled as GameObjects rather than Transforms.
        /// </summary>
        /// <param name="go">Root GameObject to search from.</param>
        /// <param name="name">Name of the descendant to find.</param>
        /// <returns>The matching descendant GameObject, or null if not found.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static GameObject FindChildRecursive(this GameObject go, string name)
        {
            Transform found = go.transform.FindChildRecursive(name);
            return found != null ? found.gameObject : null;
        }

        /// <summary>
        /// Destroys every child of <paramref name="go"/>, using the play-mode-aware destruction
        /// logic from <see cref="ObjectExtensions.DestroySafe(UnityEngine.Object, bool)"/>.
        /// <br/>
        /// Forwards to <see cref="TransformExtensions.DestroyAllChildren(Transform)"/>. Common
        /// when clearing a dynamically populated container (inventory grid, spawned wave of
        /// enemies) before repopulating it.
        /// </summary>
        /// <param name="go">Parent GameObject whose children are destroyed.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DestroyAllChildren(this GameObject go)
            => go.transform.DestroyAllChildren();

        /// <summary>
        /// Sets the active state of every immediate child of <paramref name="go"/>.
        /// <br/>
        /// Common for toggling an entire set of UI options, weapon attachment slots or spawned
        /// pooled effects in one call without manually looping at each call site.
        /// </summary>
        /// <param name="go">Parent GameObject whose direct children are toggled.</param>
        /// <param name="active">Desired active state for all children.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetChildrenActive(this GameObject go, bool active)
            => go.transform.SetChildrenActive(active);

        /// <summary>
        /// Returns the full hierarchy path of the GameObject (e.g., "UI/Screens/MainMenu/StartButton").
        /// Invaluable for debugging and error logging.
        /// </summary>
        public static string GetHierarchyPath(this GameObject gameObject, char pathSeparator = '/')
        {
            Transform current = gameObject.transform;
            if (current.parent == null) return current.name;

            var builder = new System.Text.StringBuilder(current.name);
            while (current.parent != null)
            {
                current = current.parent;
                builder.Insert(0, pathSeparator).Insert(0, current.name);
            }

            return builder.ToString();
        }

        #endregion

        #region Debug

        /// <summary>
        /// Returns a formatted debug string describing <paramref name="go"/>'s hierarchy path,
        /// active state and layer.
        /// <br/>
        /// More informative than logging <see cref="Object.name"/> alone when diagnosing pooling
        /// or activation bugs across many similarly-named instances. Allocates a string and is
        /// intended for logs/inspectors, not hot paths.
        /// </summary>
        /// <param name="go">GameObject to describe.</param>
        /// <returns>A formatted debug string, or a placeholder if <paramref name="go"/> is Unity-null.</returns>
        public static string ToDebugString(this GameObject go)
        {
            if (go.IsUnityNull())
                return "<null GameObject>";

            return $"{go.GetHierarchyPath()} (Active: {go.activeSelf}, Layer: {go.layer})";
        }

        #endregion
    }
}
