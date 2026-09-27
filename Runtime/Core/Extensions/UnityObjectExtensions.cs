using System.Runtime.CompilerServices;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nexus.Core.Extensions
{
    /// <summary>
    /// Lifecycle and null-safety helpers for <see cref="UnityEngine.Object"/> references.
    /// <para>
    /// <b>Why this is necessary:</b><br/>
    /// A destroyed Unity object is not garbage-collected immediately; its C# wrapper remains alive 
    /// while the underlying C++ native object is deallocated. Unity overrides <c>operator ==</c> 
    /// to make destroyed objects compare equal to <c>null</c>.
    /// </para>
    /// <para>
    /// However, this overload is silently bypassed when:
    /// <list type="bullet">
    /// <item>Using the C# null-conditional operator (<c>obj?.Method()</c>)</item>
    /// <item>Using the null-coalescing operator (<c>obj ?? fallback</c>)</item>
    /// <item>Accessing objects through generic parameters (<c>T</c>)</item>
    /// <item>Accessing objects through interfaces (e.g., <c>IDamageable</c>)</item>
    /// </list>
    /// </para>
    /// </summary>
    public static class UnityObjectExtensions
    {
        #region Constants

        /// <summary>
        /// Default delay, in seconds, used by destruction helpers that do not require a
        /// deferred timer. Kept as a named constant to make call sites self-documenting
        /// (<c>DestroySafe()</c> vs a magic <c>0f</c> literal).
        /// </summary>
        private const float ImmediateDestroyDelay = 0f;

        #endregion

        #region #region Null Safety & Validation

        /// <summary>
        /// Returns <c>true</c> if the reference is a pure C# null OR has been destroyed by Unity.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsUnityNull(this Object obj)
            => obj == null;

        /// <summary>
        /// Returns <c>true</c> if the reference is valid, non-null, and not destroyed.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsAlive(this Object obj)
            => !obj.IsUnityNull();

        /// <summary>
        /// Returns <c>true</c> if <paramref name="obj"/> has been destroyed or was never assigned.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsDestroyed(this Object obj)
            => obj.IsUnityNull();

        /// <summary>
        /// Returns <c>true</c> if <paramref name="obj"/> has been destroyed or was never assigned.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNullOrDestroyed(this Object obj)
            => obj.IsUnityNull();

        /// <summary>
        /// Returns a true C# <c>null</c> if <paramref name="obj"/> is Unity-null, otherwise returns
        /// <paramref name="obj"/> unchanged.
        /// <br/>
        /// <para>
        /// This is the standard fix for Unity's null-coalescing trap: because Unity overloads
        /// <c>==</c> but not the underlying reference, expressions like <c>target?.DoThing()</c> or
        /// <c>target ?? fallback</c> use the C# compiler's reference-null check internally for
        /// <c>?.</c> and <c>??</c>, which does <b>not</b> go through Unity's overload. A destroyed
        /// component can therefore silently pass through <c>?.</c> and touch destroyed native memory.
        /// </para>
        /// <para>
        /// Calling <c>target.OrNull()?.DoThing()</c> forces the check through Unity's overloaded
        /// equality first, guaranteeing <c>?.</c> short-circuits correctly on destroyed objects.
        /// </para>
        /// </summary>
        /// <typeparam name="T">Concrete <see cref="UnityEngine.Object"/> type.</typeparam>
        /// <param name="obj">Object to test.</param>
        /// <returns><paramref name="obj"/> if alive; otherwise a genuine C# <c>null</c>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T OrNull<T>(this T obj) where T : Object
            => obj.IsUnityNull() ? null : obj;

        /// <summary>
        /// Returns <paramref name="obj"/> if it is alive, otherwise returns <paramref name="fallback"/>.
        /// <br/>
        /// Ideal for resolving optional references (audio source, VFX prefab, UI icon) to a safe
        /// default without scattering conditional null checks across gameplay code.
        /// </summary>
        /// <typeparam name="T">Concrete <see cref="UnityEngine.Object"/> type.</typeparam>
        /// <param name="obj">Candidate object.</param>
        /// <param name="fallback">Object returned when <paramref name="obj"/> is Unity-null.</param>
        /// <returns><paramref name="obj"/> or <paramref name="fallback"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T ValidOr<T>(this T obj, T fallback) where T : Object
            => obj.IsUnityNull() ? fallback : obj;

        #endregion

        #region Destruction

        /// <summary>
        /// Destroys <paramref name="obj"/> using the correct API for the current execution context.
        /// <br/>
        /// <para>
        /// Uses <see cref="Object.Destroy(Object)"/> in Play mode and
        /// <see cref="Object.DestroyImmediate(Object, bool)"/> in Edit mode, since calling
        /// <c>Destroy</c> outside Play mode silently does nothing and calling
        /// <c>DestroyImmediate</c> during Play mode can corrupt object lifecycle expectations.
        /// </para>
        /// <para>
        /// This single call is safe to use inside both runtime gameplay code and editor tooling
        /// (custom inspectors, editor windows) without branching on <see cref="Application.isPlaying"/>
        /// at every call site.
        /// </para>
        /// </summary>
        /// <param name="obj">Object to destroy. No-op if already Unity-null.</param>
        /// <param name="allowDestroyingAssets">
        /// Passed through to <see cref="Object.DestroyImmediate(Object, bool)"/> in Edit mode.
        /// Defaults to <c>false</c> to prevent accidentally deleting project assets (materials,
        /// ScriptableObjects) when this is called from editor tooling on the wrong reference.
        /// </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DestroySafe(this Object obj, bool allowDestroyingAssets = false)
        {
            if (obj.IsUnityNull()) return;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Object.DestroyImmediate(obj, allowDestroyingAssets);
                return;
            }
#endif

            Object.Destroy(obj);
        }

        /// <summary>
        /// Destroys <paramref name="obj"/> after <paramref name="delay"/> seconds via
        /// <see cref="Object.Destroy(Object, float)"/>.
        /// <br/>
        /// Only valid in Play mode; delayed destruction has no meaningful equivalent in Edit mode.
        /// Ideal for timed VFX cleanup, projectile lifetimes and death-animation-then-despawn flows
        /// without manually scheduling a coroutine.
        /// </summary>
        /// <param name="obj">Object to destroy. No-op if already Unity-null.</param>
        /// <param name="delay">Seconds to wait before destruction. Defaults to immediate.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DestroySafe(this Object obj, float delay = ImmediateDestroyDelay)
        {
            if (obj.IsUnityNull()) return;
            Object.Destroy(obj, delay);
        }

        #endregion

        #region Instantiation

        /// <summary>
        /// Instantiates a copy of <paramref name="prefab"/>, guarding against a null prefab reference.
        /// <br/>
        /// A missing prefab assignment in the inspector is one of the most common causes of
        /// <see cref="System.NullReferenceException"/> in spawners and object pools. This logs a
        /// clear <see cref="Debug.LogError(object)"/> and returns <c>null</c> instead of throwing,
        /// preventing a single bad reference from halting an entire spawn/update loop.
        /// </summary>
        /// <typeparam name="T">Concrete <see cref="UnityEngine.Object"/> type being instantiated.</typeparam>
        /// <param name="prefab">Prefab or asset reference to clone.</param>
        /// <returns>The instantiated copy, or <c>null</c> if <paramref name="prefab"/> was Unity-null.</returns>
        public static T InstantiateSafe<T>(this T prefab) where T : Object
        {
            if (prefab.IsUnityNull())
            {
                Debug.LogError("InstantiateSafe failed: prefab reference is null or destroyed.");
                return null;
            }

            return Object.Instantiate(prefab);
        }

        /// <summary>
        /// Instantiates a copy of <paramref name="prefab"/> under <paramref name="parent"/>,
        /// guarding against a null prefab reference.
        /// <br/>
        /// Ideal for UI element pooling and hierarchy-organized VFX/projectile spawning where the
        /// parent transform groups instances for scene readability and batch cleanup.
        /// </summary>
        /// <typeparam name="T">Concrete <see cref="UnityEngine.Object"/> type being instantiated.</typeparam>
        /// <param name="prefab">Prefab or asset reference to clone.</param>
        /// <param name="parent">Transform to parent the new instance under.</param>
        /// <returns>The instantiated copy, or <c>null</c> if <paramref name="prefab"/> was Unity-null.</returns>
        public static T InstantiateSafe<T>(this T prefab, Transform parent) where T : Object
        {
            if (prefab.IsUnityNull())
            {
                Debug.LogError("InstantiateSafe failed: prefab reference is null or destroyed.");
                return null;
            }

            return Object.Instantiate(prefab, parent);
        }

        /// <summary>
        /// Instantiates a copy of <paramref name="prefab"/> at <paramref name="position"/> and
        /// <paramref name="rotation"/>, guarding against a null prefab reference.
        /// <br/>
        /// Standard entry point for gameplay spawning (projectiles, hit-effects, enemies) where
        /// the spawn transform is computed at runtime rather than authored on the prefab itself.
        /// </summary>
        /// <typeparam name="T">Concrete <see cref="UnityEngine.Object"/> type being instantiated.</typeparam>
        /// <param name="prefab">Prefab or asset reference to clone.</param>
        /// <param name="position">World-space spawn position.</param>
        /// <param name="rotation">World-space spawn rotation.</param>
        /// <returns>The instantiated copy, or <c>null</c> if <paramref name="prefab"/> was Unity-null.</returns>
        public static T InstantiateSafe<T>(this T prefab, Vector3 position, Quaternion rotation) where T : Object
        {
            if (prefab.IsUnityNull())
            {
                Debug.LogError("InstantiateSafe failed: prefab reference is null or destroyed.");
                return null;
            }

            return Object.Instantiate(prefab, position, rotation);
        }

        /// <summary>
        /// Instantiates a copy of <paramref name="prefab"/> at <paramref name="position"/> and
        /// <paramref name="rotation"/> under <paramref name="parent"/>, guarding against a null
        /// prefab reference.
        /// <br/>
        /// Combines transform placement and hierarchy organization in a single call for pooled
        /// projectile/VFX systems that spawn directly into a designated container.
        /// </summary>
        /// <typeparam name="T">Concrete <see cref="UnityEngine.Object"/> type being instantiated.</typeparam>
        /// <param name="prefab">Prefab or asset reference to clone.</param>
        /// <param name="position">World-space spawn position.</param>
        /// <param name="rotation">World-space spawn rotation.</param>
        /// <param name="parent">Transform to parent the new instance under.</param>
        /// <returns>The instantiated copy, or <c>null</c> if <paramref name="prefab"/> was Unity-null.</returns>
        public static T InstantiateSafe<T>(this T prefab, Vector3 position, Quaternion rotation, Transform parent) where T : Object
        {
            if (prefab.IsUnityNull())
            {
                Debug.LogError("InstantiateSafe failed: prefab reference is null or destroyed.");
                return null;
            }

            return Object.Instantiate(prefab, position, rotation, parent);
        }

        #endregion

        #region Debug & Identification

        /// <summary>
        /// Returns <see cref="Object.GetInstanceID"/> for <paramref name="obj"/>, or <c>0</c> if
        /// <paramref name="obj"/> is Unity-null.
        /// <br/>
        /// Safe to use as a dictionary key source or pooling identifier without first checking
        /// for null, since instance ID <c>0</c> is never assigned to a real Unity object.
        /// </summary>
        /// <param name="obj">Object to query.</param>
        /// <returns>The instance ID, or <c>0</c> if <paramref name="obj"/> is Unity-null.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetInstanceIdSafe(this Object obj)
            => obj.IsUnityNull() ? 0 : obj.GetInstanceID();

        /// <summary>
        /// Returns the name of the Unity Object without throwing a <see cref="MissingReferenceException"/>
        /// if the object has already been destroyed.
        /// </summary>
        public static string SafeName(this Object obj, string fallback = "<null or destroyed>")
        {
            if (obj.IsUnityNull()) return fallback;

            try
            {
                return obj.name;
            }
            catch (MissingReferenceException)
            {
                return fallback;
            }
        }

        /// <summary>
        /// Returns a human-readable debug string describing <paramref name="obj"/>, safely handling
        /// destroyed or null references instead of throwing.
        /// <br/>
        /// Format: <c>TypeName ("ObjectName", InstanceID: 12345)</c>. Intended for
        /// <see cref="Debug.Log(object)"/> calls, exception messages and runtime inspectors —
        /// not for hot-path logic due to string allocation.
        /// </summary>
        /// <param name="obj">Object to describe.</param>
        /// <returns>A formatted debug string, or a placeholder if <paramref name="obj"/> is Unity-null.</returns>
        public static string ToDebugString(this Object obj)
        {
            if (obj.IsUnityNull())
                return "<null UnityEngine.Object>";

            return $"{obj.GetType().Name} (\"{obj.name}\", InstanceID: {obj.GetInstanceID()})";
        }

        #endregion

        #region Scene Persistence

        /// <summary>
        /// Marks <paramref name="obj"/> to survive scene loads via
        /// <see cref="Object.DontDestroyOnLoad(Object)"/>, guarding against null references and
        /// Edit-mode calls.
        /// <br/>
        /// <para>
        /// Calling <see cref="Object.DontDestroyOnLoad(Object)"/> outside Play mode has no effect
        /// and can mask configuration mistakes in editor scripts. This wrapper silently no-ops in
        /// that case rather than letting a misplaced call appear to succeed.
        /// </para>
        /// <para>
        /// Standard use case: singleton managers (audio, save system, input) that must persist
        /// across scene transitions in a multi-scene game.
        /// </para>
        /// </summary>
        /// <param name="obj">Object to persist. Must be a root-level <see cref="GameObject"/> or
        /// component thereof per Unity's requirements for <see cref="Object.DontDestroyOnLoad(Object)"/>.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MarkPersistentAcrossScenes(this Object obj)
        {
            if (obj.IsUnityNull()) return;
            if (!Application.isPlaying) return;

            Object.DontDestroyOnLoad(obj);
        }

        #endregion
    }
}
