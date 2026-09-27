using System;
using System.Reflection;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nexus.Core.Extensions
{
    /// <summary>
    /// Ergonomic and allocation-friendly extensions for <see cref="Component"/> and <see cref="GameObject"/>.
    /// </summary>
    public static class ComponentExtensions
    {
        #region Constants

        /// <summary>
        /// Separator used when building a readable hierarchy path via
        /// <see cref="GetHierarchyPath(Component)"/>. Matches Unity's own convention for
        /// <see cref="Transform.Find(string)"/> paths.
        /// </summary>
        private const char HierarchyPathSeparator = '/';

        #endregion

        #region Component Retrieval

        /// <summary>
        /// Returns the existing component of type <typeparamref name="T"/> on this Component's GameObject, 
        /// or attaches and returns a new one if it does not exist.
        /// </summary>
        public static T GetOrAddComponent<T>(this Component component) where T : Component
        {
            return component.gameObject.GetOrAddComponent<T>();
        }

        /// <summary>
        /// Non-generic version of <see cref="GetOrAddComponent{T}(Component)"/>.
        /// </summary>
        public static Component GetOrAddComponent(this Component component, Type type)
        {
            return component.gameObject.GetOrAddComponent(type);
        }

        public static T GetOrAddComponent<T>(this Component component, Action<T> onAdded) where T : Component
        {
            return component.gameObject.GetOrAddComponent<T>(onAdded);
        }

        public static Component GetOrAddComponent(this Component component, Type type, Action<Component> onAdded)
        {
            return component.gameObject.GetOrAddComponent(type, onAdded);
        }

        /// <summary>
        /// Returns the component of type <typeparamref name="T"/> on <paramref name="c"/>, logging an
        /// error if it is missing instead of allowing a downstream <see cref="System.NullReferenceException"/>.
        /// <br/>
        /// Intended for <c>Awake</c>/<c>OnEnable</c> validation of hard dependencies (e.g. a
        /// <c>PlayerController</c> requiring a <see cref="Rigidbody"/>), so a misconfigured prefab
        /// fails loudly at the point of setup rather than crashing deep in an update loop.
        /// </summary>
        /// <typeparam name="T">Required component type.</typeparam>
        /// <param name="c">Component whose <see cref="GameObject"/> is queried.</param>
        /// <returns>The found component, or a Unity-null reference if missing.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T RequireComponent<T>(this Component c) where T : Component
        {
            T comp = c.GetComponent<T>();
            if (comp.IsUnityNull())
                Debug.LogError($"Required component of type {typeof(T).Name} is missing on \"{c.gameObject.name}\".");

            return comp;
        }

        /// <summary>
        /// Returns the component of type <typeparamref name="T"/> on <paramref name="c"/>, or
        /// <paramref name="fallback"/> if it is not present.
        /// <br/>
        /// Ideal for optional dependencies (an optional <see cref="ParticleSystem"/> tint override,
        /// an optional footstep <see cref="AudioSource"/>) where the absence of the component is a
        /// valid, expected configuration rather than an error.
        /// </summary>
        /// <typeparam name="T">Component type to retrieve.</typeparam>
        /// <param name="c">Component whose <see cref="GameObject"/> is queried.</param>
        /// <param name="fallback">Value returned when the component is absent.</param>
        /// <returns>The found component, or <paramref name="fallback"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T GetComponentOr<T>(this Component c, T fallback) where T : Component
        {
            T comp = c.GetComponent<T>();
            return comp.IsUnityNull() ? fallback : comp;
        }

        /// <summary>
        /// Attempts to find a component of type <typeparamref name="T"/> in parents.
        /// </summary>
        public static bool TryGetComponentInParent<T>(this Component component, out T result, bool includeInactive = false)
        {
            result = component.GetComponentInParent<T>(includeInactive);
            return result != null;
        }

        /// <summary>
        /// Attempts to find a component of type <typeparamref name="T"/> in children.
        /// </summary>
        public static bool TryGetComponentInChildren<T>(this Component component, out T result, bool includeInactive = false)
        {
            result = component.GetComponentInChildren<T>(includeInactive);
            return result != null;
        }

        #endregion

        #region Existence Checks

        /// <summary>
        /// Checks if a component of type <typeparamref name="T"/> exists on this Component's GameObject without allocating.
        /// </summary>
        public static bool HasComponent<T>(this Component component)
        {
            return component.TryGetComponent<T>(out _);
        }

        /// <summary>
        /// Checks if a component of <see cref="Type"/> exists on this Component's GameObject without allocating.
        /// </summary>
        public static bool HasComponent(this Component component, Type type)
        {
            return component.TryGetComponent(type, out _);
        }

        /// <summary>
        /// Returns <c>true</c> if a component of type <typeparamref name="T"/> exists anywhere in
        /// <paramref name="c"/>'s children (including <paramref name="c"/> itself, per Unity's
        /// own <see cref="Component.GetComponentInChildren{T}()"/> semantics).
        /// <br/>
        /// Useful for structural validation, e.g. confirming a weapon socket hierarchy contains
        /// at least one <c>MuzzleFlash</c> component before enabling a fire effect.
        /// </summary>
        /// <typeparam name="T">Component type to search for.</typeparam>
        /// <param name="c">Root component to search from.</param>
        /// <returns><c>true</c> if any matching component is found.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasComponentInChildren<T>(this Component c) where T : Component
            => c.GetComponentInChildren<T>() != null;

        /// <summary>
        /// Returns <c>true</c> if a component of type <typeparamref name="T"/> exists on
        /// <paramref name="c"/> or any of its ancestors.
        /// <br/>
        /// Common in hit-detection code that needs to find an owning <c>Damageable</c> or
        /// <c>Faction</c> component regardless of which child collider registered the hit.
        /// </summary>
        /// <typeparam name="T">Component type to search for.</typeparam>
        /// <param name="c">Component to search upward from.</param>
        /// <returns><c>true</c> if any matching component is found.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasComponentInParent<T>(this Component c) where T : Component
            => c.GetComponentInParent<T>() != null;

        #endregion

        #region Hierarchy Search

        /// <summary>
        /// Finds a component of type <typeparamref name="T"/> in the parent chain, 
        /// <c>EXCLUDING</c> the calling object's own GameObject.
        /// <para>
        /// (Standard Unity <see cref="Component.GetComponentInParent{T}()"/> checks self first, 
        /// which often returns unwanted local components).
        /// </para>
        /// </summary>
        public static T GetComponentInParentOnly<T>(this Component component, bool includeInactive = false) where T : class
        {
            return component.gameObject.GetComponentInParentOnly<T>(includeInactive);
        }

        /// <summary>
        /// Finds all components of type <typeparamref name="T"/> in children, 
        /// <c>EXCLUDING</c> the calling object's own GameObject.
        /// </summary>
        public static void GetComponentsInChildrenOnly<T>(
            this Component component,
            List<T> results,
            bool includeInactive = false) where T : class
        {
            component.gameObject.GetComponentsInChildrenOnly<T>(results, includeInactive);
        }

        #endregion

        #region Tag & Layer

        /// <summary>
        /// Safely compares <paramref name="c"/>'s <see cref="GameObject"/> tag against
        /// <paramref name="tag"/>, guarding against a destroyed component.
        /// <br/>
        /// Prevents <see cref="MissingReferenceException"/> in collision/trigger callbacks where the
        /// other object may already have been destroyed earlier in the same frame (e.g. an explosion
        /// destroying multiple overlapping objects in sequence).
        /// </summary>
        /// <param name="c">Component to test.</param>
        /// <param name="tag">Tag to compare against.</param>
        /// <returns><c>true</c> if <paramref name="c"/> is alive and its tag matches <paramref name="tag"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CompareTagSafe(this Component c, string tag)
            => !c.IsUnityNull() && c.CompareTag(tag);

        /// <summary>
        /// Checks if the Component's GameObject layer is included inside a <see cref="LayerMask"/>.
        /// </summary>
        public static bool IsInLayerMask(this Component component, LayerMask layerMask)
        {
            return component.gameObject.IsInLayerMask(layerMask);
        }

        /// <summary>
        /// Recursively changes the layer of this Component's root and all of its child transforms.
        /// </summary>
        public static void SetLayerRecursively(this Component component, int layer)
        {
            component.gameObject.SetLayerRecursively(layer);
        }

        #endregion

        #region Debug & Hierarchy Path

        /// <summary>
        /// Returns the full hierarchy path of the Component's GameObject.
        /// </summary>
        public static string GetHierarchyPath(this Component component)
        {
            return component.gameObject.GetHierarchyPath(HierarchyPathSeparator);
        }

        /// <summary>
        /// Returns a human-readable debug string identifying <paramref name="c"/> by type and full
        /// scene hierarchy path.
        /// <br/>
        /// Format: <c>TypeName on "Root/Parent/Child"</c>. Intended for <see cref="Debug.Log(object)"/>
        /// calls and exception messages, not hot-path logic, due to string allocation.
        /// </summary>
        /// <param name="c">Component to describe.</param>
        /// <returns>A formatted debug string, or a placeholder if <paramref name="c"/> is Unity-null.</returns>
        public static string ToDebugString(this Component c)
        {
            if (c.IsUnityNull())
                return "<null Component>";

            return $"{c.GetType().Name} on \"{c.GetHierarchyPath()}\"";
        }

        #endregion

        #region Component Copying (Editor / Tooling)

        /// <summary>
        /// Copies every public instance field from <paramref name="source"/> onto <paramref name="target"/>
        /// via reflection.
        /// <para>
        /// This method allocates and boxes value-type fields internally as an unavoidable consequence
        /// of <see cref="FieldInfo.GetValue(object)"/> / <see cref="FieldInfo.SetValue(object, object)"/>.
        /// It is intentionally excluded from the zero-allocation guarantee that governs the rest of
        /// this class and must never be called from gameplay hot paths (update loops, physics callbacks).
        /// </para>
        /// <para>
        /// Intended for editor tooling and one-off migration scripts — e.g. copying tuned values from
        /// a reference prefab component onto a batch of prefab variants that fell out of sync.
        /// </para>
        /// </summary>
        /// <typeparam name="T">Concrete component type shared by both <paramref name="target"/> and <paramref name="source"/>.</typeparam>
        /// <param name="target">Component whose fields are overwritten.</param>
        /// <param name="source">Component whose field values are read.</param>
        public static void CopyValuesFrom<T>(this T target, T source) where T : Component
        {
            if (target.IsUnityNull() || source.IsUnityNull()) return;

            FieldInfo[] fields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < fields.Length; i++)
            {
                fields[i].SetValue(target, fields[i].GetValue(source));
            }
        }

        #endregion
    }
}
