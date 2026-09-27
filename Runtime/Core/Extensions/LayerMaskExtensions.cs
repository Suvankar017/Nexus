using System.Runtime.CompilerServices;
using UnityEngine;

namespace Nexus.Core.Extensions
{
    /// <summary>
    /// Production-ready extension methods for <see cref="LayerMask"/> and raw layer indices,
    /// covering bitmask membership checks, combination, single-layer extraction and safe
    /// name-based construction.
    /// <para>Design rules:</para>
    /// <list type="bullet">
    ///     <item>All membership and combination operations are pure bitwise math on <see cref="LayerMask.value"/>; no method ever touches <see cref="LayerMask.LayerToName(int)"/> or <see cref="LayerMask.NameToLayer(string)"/> in a hot path, since those perform an internal string lookup.</item>
    ///     <item>Name-based construction methods are explicitly scoped to initialization code (Awake, inspector-driven setup) and are documented as allocating, matching Unity's own string-based layer API.</item>
    ///     <item>A raw <see cref="int"/> layer index (as returned by <see cref="GameObject.layer"/>) and a <see cref="LayerMask"/> bitmask are two different representations of "layer" in Unity; this class is explicit in every method name and parameter about which one is expected.</item>
    ///     <item>Zero allocation on every bitwise member. No boxing, no LINQ.</item>
    /// </list>
    /// </summary>
    public static class LayerMaskExtensions
    {
        #region Constants

        /// <summary>
        /// A <see cref="LayerMask"/> value representing every layer, used as the default "match
        /// anything" mask for raycasts and overlap queries that have not been configured with a
        /// specific mask.
        /// </summary>
        public const int AllLayersValue = ~0;

        /// <summary>
        /// A <see cref="LayerMask"/> value representing no layers, used as the base accumulator
        /// when building a mask incrementally via <see cref="Add(LayerMask, int)"/>.
        /// </summary>
        public const int NoLayersValue = 0;

        #endregion

        #region Membership

        /// <summary>
        /// Returns <c>true</c> if <paramref name="layer"/> (a raw layer index, e.g. from
        /// <see cref="GameObject.layer"/>) is included in <paramref name="mask"/>.
        /// <br/>
        /// The standard bitwise check for inspector-driven <see cref="LayerMask"/> fields used in
        /// raycast filtering, aggro detection and area-of-effect queries — avoids fragile manual
        /// <c>mask.value == (1 &lt;&lt; layer)</c> comparisons scattered across gameplay code.
        /// </summary>
        /// <param name="mask">Mask to test membership against.</param>
        /// <param name="layer">Raw layer index to test.</param>
        /// <returns><c>true</c> if <paramref name="layer"/>'s bit is set in <paramref name="mask"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Contains(this LayerMask mask, int layer)
            => (mask.value & (1 << layer)) != 0;

        /// <summary>
        /// Returns <c>true</c> if <paramref name="go"/>'s layer is included in <paramref name="mask"/>.
        /// <br/>
        /// Convenience overload avoiding the boilerplate <c>mask.Contains(go.layer)</c> at call
        /// sites filtering trigger overlaps or raycast hits directly against a <see cref="GameObject"/>.
        /// </summary>
        /// <param name="mask">Mask to test membership against.</param>
        /// <param name="go">GameObject whose layer is tested.</param>
        /// <returns><c>true</c> if <paramref name="go"/>'s layer bit is set in <paramref name="mask"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Contains(this LayerMask mask, GameObject go)
            => mask.Contains(go.layer);

        /// <summary>
        /// Returns <c>true</c> if <paramref name="mask"/> contains every layer included in
        /// <paramref name="other"/>.
        /// <br/>
        /// Useful for validating configuration consistency — e.g. asserting that a "damageable"
        /// layer mask is a subset of a broader "interactable" mask during editor validation.
        /// </summary>
        /// <param name="mask">Mask to test.</param>
        /// <param name="other">Mask whose layers must all be present in <paramref name="mask"/>.</param>
        /// <returns><c>true</c> if every bit set in <paramref name="other"/> is also set in <paramref name="mask"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ContainsAll(this LayerMask mask, LayerMask other)
            => (mask.value & other.value) == other.value;

        /// <summary>
        /// Returns <c>true</c> if <paramref name="mask"/> and <paramref name="other"/> share at
        /// least one common layer.
        /// <br/>
        /// Standard check for whether two independently-configured masks (e.g. a projectile's
        /// "can hit" mask and a shield's "blocks" mask) would ever interact, without needing to
        /// enumerate individual layers.
        /// </summary>
        /// <param name="mask">First mask.</param>
        /// <param name="other">Second mask.</param>
        /// <returns><c>true</c> if any layer bit is set in both masks.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Overlaps(this LayerMask mask, LayerMask other)
            => (mask.value & other.value) != 0;

        /// <summary>
        /// Returns <c>true</c> if <paramref name="mask"/> has no layers set.
        /// <br/>
        /// Useful for detecting an unconfigured <see cref="LayerMask"/> inspector field left at its
        /// default zero value, which would otherwise cause every raycast/overlap query using it to
        /// silently match nothing.
        /// </summary>
        /// <param name="mask">Mask to test.</param>
        /// <returns><c>true</c> if <paramref name="mask"/>.value is <c>0</c>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsEmpty(this LayerMask mask)
            => mask.value == 0;

        #endregion

        #region Combination

        /// <summary>
        /// Returns a copy of <paramref name="mask"/> with <paramref name="layer"/> added.
        /// <br/>
        /// Standard way to incrementally build a mask at runtime (e.g. adding the "Player" layer
        /// to an enemy's target mask when a stealth ability expires) without manually manipulating
        /// the raw <see cref="LayerMask.value"/> integer.
        /// </summary>
        /// <param name="mask">Original mask.</param>
        /// <param name="layer">Raw layer index to add.</param>
        /// <returns>A new mask with <paramref name="layer"/> included.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static LayerMask Add(this LayerMask mask, int layer)
            => mask.value | (1 << layer);

        /// <summary>
        /// Returns a copy of <paramref name="mask"/> with <paramref name="layer"/> removed.
        /// <br/>
        /// Standard way to revoke a previously-added layer at runtime (e.g. removing "Player" from
        /// an enemy's target mask when stealth is activated) without manually manipulating the raw
        /// <see cref="LayerMask.value"/> integer.
        /// </summary>
        /// <param name="mask">Original mask.</param>
        /// <param name="layer">Raw layer index to remove.</param>
        /// <returns>A new mask with <paramref name="layer"/> excluded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static LayerMask Remove(this LayerMask mask, int layer)
            => mask.value & ~(1 << layer);

        /// <summary>
        /// Returns the union of <paramref name="mask"/> and <paramref name="other"/>.
        /// <br/>
        /// Useful for merging two independently-authored masks at runtime, such as combining a
        /// weapon's base target mask with a temporary "reveal hidden enemies" buff mask.
        /// </summary>
        /// <param name="mask">First mask.</param>
        /// <param name="other">Second mask.</param>
        /// <returns>A new mask containing every layer present in either input.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static LayerMask CombinedWith(this LayerMask mask, LayerMask other)
            => mask.value | other.value;

        /// <summary>
        /// Returns the intersection of <paramref name="mask"/> and <paramref name="other"/>.
        /// <br/>
        /// Useful for narrowing a broad "interactable" mask down to only the layers also present
        /// in a more specific "currently reachable" mask, without enumerating individual layers.
        /// </summary>
        /// <param name="mask">First mask.</param>
        /// <param name="other">Second mask.</param>
        /// <returns>A new mask containing only layers present in both inputs.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static LayerMask IntersectedWith(this LayerMask mask, LayerMask other)
            => mask.value & other.value;

        /// <summary>
        /// Returns the bitwise inverse of <paramref name="mask"/> — every layer not present in
        /// the original.
        /// <br/>
        /// Useful for deriving an "everything except X" mask at runtime, such as computing a
        /// raycast mask that ignores a specific "Ignore Raycast"-style layer without hand-authoring
        /// the inverse in the inspector.
        /// </summary>
        /// <param name="mask">Mask to invert.</param>
        /// <returns>A new mask containing every layer not present in <paramref name="mask"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static LayerMask Inverted(this LayerMask mask)
            => ~mask.value;

        #endregion

        #region Single-Layer Extraction

        /// <summary>
        /// Returns <c>true</c> if <paramref name="mask"/> has exactly one layer bit set, and
        /// outputs that layer's raw index via <paramref name="layer"/>.
        /// <br/>
        /// Many APIs (e.g. <see cref="GameObject.layer"/> assignment, some third-party plugin
        /// configs) require a single raw layer index rather than a multi-layer mask. This
        /// validates that assumption before extracting the value, instead of silently returning
        /// an arbitrary bit from a multi-layer mask.
        /// </summary>
        /// <param name="mask">Mask to inspect.</param>
        /// <param name="layer">The single layer index if the mask contains exactly one layer; otherwise <c>-1</c>.</param>
        /// <returns><c>true</c> if <paramref name="mask"/> represents exactly one layer.</returns>
        public static bool TryGetSingleLayer(this LayerMask mask, out int layer)
        {
            int value = mask.value;

            if (value == 0 || (value & (value - 1)) != 0)
            {
                layer = -1;
                return false;
            }

            layer = 0;
            while ((value & 1) == 0)
            {
                value >>= 1;
                layer++;
            }

            return true;
        }

        /// <summary>
        /// Returns the number of layer bits set in <paramref name="mask"/>.
        /// <br/>
        /// Uses Brian Kernighan's bit-counting trick rather than iterating all 32 possible layers,
        /// making it cheap enough for per-frame diagnostic or validation checks (e.g. warning if a
        /// mask meant to represent a single layer contains more than one bit).
        /// </summary>
        /// <param name="mask">Mask to inspect.</param>
        /// <returns>The count of set layer bits.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int LayerCount(this LayerMask mask)
        {
            int value = mask.value;
            int count = 0;
            while (value != 0)
            {
                value &= value - 1;
                count++;
            }
            return count;
        }

        #endregion

        #region Construction

        /// <summary>
        /// Builds a <see cref="LayerMask"/> containing only <paramref name="layer"/>.
        /// <br/>
        /// Reads more clearly than a raw <c>1 &lt;&lt; layer</c> bit-shift expression at call
        /// sites converting a <see cref="GameObject.layer"/> index into a mask for use with
        /// <see cref="Physics.Raycast(Ray, out RaycastHit, float, int)"/> and similar APIs.
        /// </summary>
        /// <param name="layer">Raw layer index to convert.</param>
        /// <returns>A mask containing only <paramref name="layer"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static LayerMask ToMask(this int layer)
            => 1 << layer;

        /// <summary>
        /// Builds a <see cref="LayerMask"/> from a set of raw layer indices.
        /// <br/>
        /// Avoids repeated <c>mask.Add(layer)</c> chaining when constructing a mask from several
        /// known layer indices at once, such as combining "Player", "Enemy" and "Neutral" into a
        /// single hit-detection mask during initialization.
        /// </summary>
        /// <param name="layers">Raw layer indices to include. Uses <c>params</c> for call-site readability; intended for initialization, not hot paths.</param>
        /// <returns>A mask containing every specified layer.</returns>
        public static LayerMask FromLayers(params int[] layers)
        {
            int value = NoLayersValue;
            for (int i = 0; i < layers.Length; i++)
                value |= 1 << layers[i];

            return value;
        }

        /// <summary>
        /// Builds a <see cref="LayerMask"/> from a set of layer names via
        /// <see cref="LayerMask.NameToLayer(string)"/>, silently skipping any name that does not
        /// match a configured layer.
        /// <br/>
        /// <para>
        /// Performs a string lookup per name and is explicitly scoped to initialization code
        /// (Awake, ScriptableObject configuration loading) — never call this from a hot path, as
        /// <see cref="LayerMask.NameToLayer(string)"/> is not a cheap bitwise operation.
        /// </para>
        /// <para>
        /// Useful for building a mask from designer-authored layer name lists in a config asset,
        /// where storing names rather than raw indices survives layer reordering in Project Settings.
        /// </para>
        /// </summary>
        /// <param name="layerNames">Layer names to resolve and include. Uses <c>params</c> for call-site readability.</param>
        /// <returns>A mask containing every successfully resolved layer.</returns>
        public static LayerMask FromNames(params string[] layerNames)
        {
            int value = NoLayersValue;
            for (int i = 0; i < layerNames.Length; i++)
            {
                int layer = LayerMask.NameToLayer(layerNames[i]);
                if (layer >= 0)
                    value |= 1 << layer;
            }

            return value;
        }

        #endregion

        #region Debug

        /// <summary>
        /// Returns a comma-separated string of every layer name included in <paramref name="mask"/>.
        /// <br/>
        /// Performs a <see cref="LayerMask.LayerToName(int)"/> lookup per set bit and allocates a
        /// string; intended for <see cref="Debug.Log(object)"/> calls and inspector tooling when
        /// diagnosing why a raycast or overlap query matched (or failed to match) an object, not
        /// for hot-path logic.
        /// </summary>
        /// <param name="mask">Mask to describe.</param>
        /// <returns>A formatted debug string listing every included layer name.</returns>
        public static string ToDebugString(this LayerMask mask)
        {
            if (mask.IsEmpty())
                return "LayerMask(none)";

            var sb = new System.Text.StringBuilder("LayerMask(");
            bool first = true;

            for (int i = 0; i < 32; i++)
            {
                if (!mask.Contains(i)) continue;

                string name = LayerMask.LayerToName(i);
                if (name.IsNullOrEmpty()) continue;

                if (!first) sb.Append(", ");
                sb.Append(name);
                first = false;
            }

            sb.Append(')');
            return sb.ToString();
        }

        #endregion
    }
}