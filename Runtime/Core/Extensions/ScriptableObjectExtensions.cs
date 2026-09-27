using System;
using System.Runtime.CompilerServices;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Nexus.Core.Extensions
{
    /// <summary>
    /// Extension methods and creation helpers for <see cref="ScriptableObject"/> covering
    /// safe runtime cloning, asset-vs-instance detection, JSON-based value copying, and
    /// editor persistence.
    /// <para>Design rules:</para>
    /// <list type="bullet">
    /// <item>A <see cref="ScriptableObject"/> asset referenced by multiple consumers is a
    /// single shared serialized instance. Mutating its fields directly at runtime — a very
    /// common beginner mistake — silently changes that data for every other object holding
    /// the same reference, and in the Editor can persist the change into the asset file
    /// itself when domain reload is disabled. Every mutating helper here either operates on
    /// an explicit runtime copy or is clearly named to signal that it edits the shared asset.</item>
    /// <item><see cref="CopyValuesFrom"/>, <see cref="ResetToDefaults"/>, <see cref="ToJson"/>,
    /// and <see cref="OverwriteFromJsonSafe"/> route through <see cref="JsonUtility"/>, which
    /// only serializes fields Unity itself would serialize (public fields and
    /// <c>[SerializeField]</c> private fields). Properties, auto-implemented backing fields
    /// without the attribute, and non-serializable types (delegates, events, interfaces) are
    /// silently skipped. This mirrors Unity's own Inspector serialization exactly, so if a
    /// field shows up in the Inspector, it round-trips through these methods correctly.</item>
    /// <item>These JSON-based methods allocate managed strings and are a deliberate exception
    /// to the zero-allocation rule, matching the same exception already made for coroutine
    /// fades elsewhere in this library. They are intended for editor tooling, save/load, and
    /// initialization — not per-frame gameplay code.</item>
    /// <item>Editor-only operations (<see cref="MarkDirtySafe"/>, <see cref="SaveAssetSafe"/>)
    /// are compiled out of player builds via <c>#if UNITY_EDITOR</c> and become harmless
    /// no-ops there, so calling code does not need its own conditional compilation to stay
    /// build-safe.</item>
    /// </list>
    /// </summary>
    public static class ScriptableObjectExtensions
    {
        /// <summary>
        /// Suffix Unity appends to the name of any <see cref="UnityEngine.Object"/> produced
        /// by <see cref="UnityEngine.Object.Instantiate(UnityEngine.Object)"/>. Stripped by
        /// <see cref="GetCleanName"/> so runtime copies display the same name as their source
        /// asset in debug logs and tooling.
        /// </summary>
        private const string CloneSuffix = " (Clone)";

        #region Runtime Cloning & Value Copying

        /// <summary>
        /// Creates an independent runtime copy of this <see cref="ScriptableObject"/> via
        /// <see cref="UnityEngine.Object.Instantiate(UnityEngine.Object)"/>. <br/>
        /// This is the correct way to give each consumer of a shared config asset (an enemy
        /// archetype, a weapon definition, a dialogue tree) its own mutable runtime state —
        /// for example current ammo count on a weapon SO — without corrupting the source
        /// asset for every other instance that references it.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T CreateRuntimeCopy<T>(this T source) where T : ScriptableObject
            => UnityEngine.Object.Instantiate(source);

        /// <summary>
        /// Overwrites this object's serialized fields with values copied from
        /// <paramref name="source"/> via a JSON round-trip. Does nothing if the two objects
        /// are not of the same runtime type, since <see cref="JsonUtility.FromJsonOverwrite"/>
        /// silently ignores mismatched fields rather than throwing. <br/>
        /// Useful for "apply preset" tooling — copying one difficulty preset's values onto
        /// the currently active settings object — without hand-writing a field-by-field copy
        /// that has to be kept in sync every time a new field is added.
        /// </summary>
        public static void CopyValuesFrom(this ScriptableObject target, ScriptableObject source)
        {
            if (source == null || target.GetType() != source.GetType()) return;
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source), target);
        }

        /// <summary>
        /// Resets every serialized field on this object back to the value it would have on a
        /// freshly created instance of the same type. <br/>
        /// Powers a "Reset to Defaults" button in custom editor tooling without needing a
        /// separately maintained default-values asset. Internally creates a temporary
        /// instance via <see cref="ScriptableObject.CreateInstance(Type)"/>, copies its
        /// values onto this object, then immediately destroys the temporary instance so it
        /// never lingers in memory or shows up in profiler snapshots.
        /// </summary>
        public static void ResetToDefaults(this ScriptableObject target)
        {
            ScriptableObject temp = ScriptableObject.CreateInstance(target.GetType());
            try
            {
                target.CopyValuesFrom(temp);
            }
            finally
            {
                temp.DestroyRuntimeCopySafe();
            }
        }

        #endregion

        #region Asset & Instance Identity

        /// <summary>
        /// True if this object is a persistent asset stored in the project (editor-only
        /// check). <br/>
        /// Use before mutating a <see cref="ScriptableObject"/> reference obtained from an
        /// unknown source, to decide whether it is safe to modify directly or whether
        /// <see cref="CreateRuntimeCopy{T}"/> should be called first — the single most
        /// effective guard against the classic "editing the asset instead of a copy" bug.
        /// <para>
        /// Outside the editor there is no reliable managed API to distinguish a loaded asset
        /// (via <see cref="Resources.Load"/> or an <c>AssetBundle</c>) from a runtime-created
        /// instance, since both are ordinary C# objects at that point; this method always
        /// returns <c>false</c> in player builds. Treat it strictly as an editor-time safety
        /// check, not a runtime capability query.
        /// </para>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsPersistentAsset(this ScriptableObject so)
        {
#if UNITY_EDITOR
            return AssetDatabase.Contains(so);
#else
            return false;
#endif
        }

        /// <summary>
        /// This object's name with any trailing <c>" (Clone)"</c> suffix removed. <br/>
        /// <see cref="UnityEngine.Object.Instantiate(UnityEngine.Object)"/> automatically
        /// appends this suffix to every runtime copy, which otherwise leaks into debug logs,
        /// save-file identifiers, and UI labels that expect the clean asset name (e.g. an
        /// item tooltip built from a duplicated inventory-item SO).
        /// </summary>
        public static string GetCleanName(this ScriptableObject so)
        {
            string objectName = so.name;
            return objectName.EndsWith(CloneSuffix, StringComparison.Ordinal)
                ? objectName.Substring(0, objectName.Length - CloneSuffix.Length)
                : objectName;
        }

        #endregion

        #region Safe Destruction

        /// <summary>
        /// Destroys this object, using <see cref="UnityEngine.Object.Destroy(UnityEngine.Object)"/>
        /// during play mode or <see cref="UnityEngine.Object.DestroyImmediate(UnityEngine.Object)"/>
        /// otherwise. <br/>
        /// Calling <c>Destroy</c> outside play mode silently does nothing, and calling
        /// <c>DestroyImmediate</c> during play mode works but is not the idiomatic choice —
        /// this method picks the correct one automatically so cleanup code (temporary
        /// instances from <see cref="ResetToDefaults"/>, pooled runtime copies) works
        /// correctly whether it runs during gameplay or from an editor tool.
        /// <para>
        /// Intended only for runtime-created instances (see <see cref="CreateRuntimeCopy{T}"/>).
        /// Calling this on a persistent asset reference will destroy the in-memory
        /// representation of that asset; check <see cref="IsPersistentAsset"/> first if the
        /// source of the reference is not guaranteed to be a runtime copy.
        /// </para>
        /// </summary>
        public static void DestroyRuntimeCopySafe(this ScriptableObject so)
        {
            if (so == null) return;

            if (Application.isPlaying) UnityEngine.Object.Destroy(so);
            else UnityEngine.Object.DestroyImmediate(so);
        }

        #endregion

        #region Editor Persistence

        /// <summary>
        /// Marks this object as dirty so the editor knows to save its changes to disk,
        /// compiled out to a no-op in player builds. <br/>
        /// Runtime and editor-tool code that mutates a <see cref="ScriptableObject"/> asset
        /// directly (deliberately, unlike the accidental mutation this file mostly guards
        /// against — for example a level-editor tool writing designer data) must call this
        /// or the change is silently discarded the next time the asset is reloaded, which is
        /// one of the most common "my edits keep disappearing" reports from tools built on
        /// custom ScriptableObject editors.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MarkDirtySafe(this ScriptableObject so)
        {
#if UNITY_EDITOR
            if (so != null) EditorUtility.SetDirty(so);
#endif
        }

        /// <summary>
        /// Marks this object dirty and immediately forces the editor to write all dirty
        /// assets to disk, compiled out to a no-op in player builds. <br/>
        /// <see cref="AssetDatabase.SaveAssets"/> flushes every pending change project-wide,
        /// not just this object, and touches disk I/O — noticeably expensive if called
        /// repeatedly. Prefer <see cref="MarkDirtySafe"/> alone for per-edit calls and reserve
        /// this method for explicit "Save" actions in custom editor tooling.
        /// </summary>
        public static void SaveAssetSafe(this ScriptableObject so)
        {
#if UNITY_EDITOR
            if (so == null) return;
            EditorUtility.SetDirty(so);
            AssetDatabase.SaveAssets();
#endif
        }

        #endregion

        #region JSON Serialization

        /// <summary>
        /// Serializes this object's fields to a JSON string via <see cref="JsonUtility"/>. <br/>
        /// The basis for simple save-data snapshots of a runtime SO copy (e.g. persisting a
        /// player-modified settings object between sessions) without hand-writing a
        /// serialization format. Only fields Unity itself serializes are included; see the
        /// class-level remarks for the exact rules.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string ToJson(this ScriptableObject so, bool prettyPrint = false)
            => JsonUtility.ToJson(so, prettyPrint);

        /// <summary>
        /// Overwrites this object's fields from a JSON string produced by <see cref="ToJson"/>,
        /// returning <c>false</c> instead of throwing if <paramref name="json"/> is <c>null</c>
        /// or empty. <br/>
        /// Pairs with <see cref="ToJson"/> to load a previously saved runtime SO snapshot.
        /// Malformed JSON still propagates a native exception from <see cref="JsonUtility"/>
        /// itself; this guard only covers the trivially common empty-string case, such as a
        /// missing save file being treated as an empty string instead of <c>null</c>.
        /// </summary>
        public static bool OverwriteFromJsonSafe(this ScriptableObject so, string json)
        {
            if (string.IsNullOrEmpty(json)) return false;
            JsonUtility.FromJsonOverwrite(json, so);
            return true;
        }

        #endregion

        #region Loading & Creation

        /// <summary>
        /// Loads a <see cref="ScriptableObject"/> of type <typeparamref name="T"/> from
        /// <paramref name="resourcesPath"/>, or creates a new runtime instance via
        /// <see cref="ScriptableObject.CreateInstance{T}"/> if no asset is found. <br/>
        /// The standard bootstrap for a ScriptableObject-based singleton or settings object:
        /// designers can optionally author a configured asset under a <c>Resources</c>
        /// folder, but the game still runs with sensible defaults if that asset is missing —
        /// for example in a fresh checkout before the design team has added it.
        /// </summary>
        public static T LoadOrCreate<T>(string resourcesPath) where T : ScriptableObject
        {
            T loaded = Resources.Load<T>(resourcesPath);
            return loaded != null ? loaded : ScriptableObject.CreateInstance<T>();
        }

        /// <summary>
        /// Creates a new runtime instance of <typeparamref name="T"/> with its
        /// <see cref="UnityEngine.Object.name"/> set to <paramref name="instanceName"/>. <br/>
        /// Thin convenience wrapper around <see cref="ScriptableObject.CreateInstance{T}"/>
        /// that avoids the two-line "create, then assign name" boilerplate at call sites that
        /// generate many named runtime instances, such as procedurally created item or
        /// ability definitions.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T CreateNamedInstance<T>(string instanceName) where T : ScriptableObject
        {
            T instance = ScriptableObject.CreateInstance<T>();
            instance.name = instanceName;
            return instance;
        }

        #endregion
    }
}