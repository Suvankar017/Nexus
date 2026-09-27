using System.Runtime.CompilerServices;
using UnityEngine;

namespace Nexus.Core.Extensions
{
    /// <summary>
    /// Extension methods for <see cref="Shader"/> plus a small set of non-extension static
    /// helpers for global shader state (properties and keywords that affect every material
    /// in the scene simultaneously).
    /// <para>Design rules:</para>
    /// <list type="bullet">
    /// <item>Global property and keyword setters mirror the <c>*Safe</c> naming and guard
    /// behavior of <c>MaterialExtensions</c>, but cannot check <see cref="Material.HasProperty(int)"/>
    /// since global properties are not tied to a specific shader. Numeric setters still
    /// reject <c>NaN</c>/<c>Infinity</c> before they reach every material in the scene at once.</item>
    /// <item><see cref="Shader.Find(string)"/> logs a console error and returns <c>null</c>
    /// when a shader is missing or stripped from a build — a common source of "works in
    /// Editor, pink in build" bugs when a shader is not referenced by anything else and gets
    /// stripped. <see cref="FindOr"/> cannot suppress that engine-level log, but does let
    /// calling code fall back to a known-safe shader in one line instead of a manual null check.</item>
    /// <item>Global keyword and property changes are expensive relative to per-material
    /// changes: they can trigger a shader variant re-resolve across every renderer in the
    /// scene. These are intended for scene-wide effects (a global weather tint, a screen-wide
    /// status effect) — not for per-object state, which belongs in <c>MaterialExtensions</c> instead.</item>
    /// </list>
    /// </summary>
    public static class ShaderExtensions
    {
        #region Instance Queries

        /// <summary>
        /// True if the current platform's GPU and graphics API can run this shader. <br/>
        /// Check this before assigning an advanced shader (e.g. one using geometry or
        /// tessellation stages) to a runtime-created material, so a fallback shader can be
        /// substituted instead of silently rendering pink or black on unsupported hardware.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsSupportedOnDevice(this Shader shader) => shader.isSupported;

        /// <summary>
        /// Sets <see cref="Shader.maximumLOD"/>, clamped to a non-negative value. <br/>
        /// Lowering this at runtime is a lightweight way to force a shader down to its
        /// cheaper LOD variants (e.g. disabling advanced lighting terms) as part of a
        /// dynamic graphics-quality scaler, without swapping the shader reference entirely.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetMaximumLodClamped(this Shader shader, int maximumLod)
            => shader.maximumLOD = Mathf.Max(0, maximumLod);

        #endregion

        #region Safe Lookup

        /// <summary>
        /// Looks up a shader by name, returning <paramref name="fallback"/> instead of
        /// <c>null</c> if it cannot be found. <br/>
        /// <see cref="Shader.Find(string)"/> still logs a console error on a miss — that
        /// engine-level log cannot be suppressed from managed code — but this method
        /// guarantees calling code always receives a usable shader reference, so a missing
        /// or build-stripped effect shader degrades to a known-safe default instead of
        /// leaving a renderer with no shader at all.
        /// </summary>
        public static Shader FindOr(string shaderName, Shader fallback)
        {
            Shader found = Shader.Find(shaderName);
            return found != null ? found : fallback;
        }

        #endregion

        #region Global Property Setters

        /// <summary>
        /// Sets a global float property visible to every material in the scene, only if the
        /// value is finite. <br/>
        /// Intended for scene-wide shader inputs — a global wind strength, a day/night cycle
        /// blend factor — that many materials read via a shared property rather than each
        /// needing an individual per-material update.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetGlobalFloatSafe(int propertyId, float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return;
            Shader.SetGlobalFloat(propertyId, value);
        }

        /// <summary>
        /// Sets a global integer property visible to every material in the scene.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetGlobalIntSafe(int propertyId, int value)
            => Shader.SetGlobalInt(propertyId, value);

        /// <summary>
        /// Sets a global color property visible to every material in the scene. <br/>
        /// The standard mechanism for a screen-wide status tint (poison, freeze, low-health
        /// vignette) that many materials sample simultaneously without individual updates.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetGlobalColorSafe(int propertyId, Color value)
            => Shader.SetGlobalColor(propertyId, value);

        /// <summary>
        /// Sets a global <see cref="Vector4"/> property visible to every material in the
        /// scene, only if every component is finite. <br/>
        /// Commonly used for global wind direction/strength packs or a global light-probe
        /// override consumed by custom lighting shaders.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetGlobalVectorSafe(int propertyId, Vector4 value)
        {
            if (float.IsNaN(value.x) || float.IsNaN(value.y) || float.IsNaN(value.z) || float.IsNaN(value.w)) return;
            Shader.SetGlobalVector(propertyId, value);
        }

        /// <summary>
        /// Sets a global texture property visible to every material in the scene. <br/>
        /// Used for scene-wide lookup textures — a shared noise texture, a ramp texture for
        /// toon shading — that every compatible shader samples via the same global slot.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetGlobalTextureSafe(int propertyId, Texture value)
            => Shader.SetGlobalTexture(propertyId, value);

        #endregion

        #region Global Keywords

        /// <summary>
        /// Enables a global shader keyword only if it is not already enabled and the string
        /// is non-empty. <br/>
        /// Global keyword changes can trigger a shader variant re-resolve across every
        /// renderer in the scene, which is comparatively expensive; the existing-state guard
        /// avoids paying that cost redundantly when called from a system that polls state
        /// every frame (e.g. a weather controller).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EnableGlobalKeywordSafe(string keyword)
        {
            if (string.IsNullOrEmpty(keyword)) return;
            if (!Shader.IsKeywordEnabled(keyword)) Shader.EnableKeyword(keyword);
        }

        /// <summary>
        /// Disables a global shader keyword only if it is currently enabled and the string
        /// is non-empty. <br/>
        /// Symmetric counterpart to <see cref="EnableGlobalKeywordSafe"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DisableGlobalKeywordSafe(string keyword)
        {
            if (string.IsNullOrEmpty(keyword)) return;
            if (Shader.IsKeywordEnabled(keyword)) Shader.DisableKeyword(keyword);
        }

        /// <summary>
        /// Enables or disables a global shader keyword based on <paramref name="enabled"/>. <br/>
        /// Lets a single bool — for example, a global "underwater" state — drive a
        /// scene-wide shader feature without an <c>if</c>/<c>else</c> at every call site.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetGlobalKeywordEnabled(string keyword, bool enabled)
        {
            if (enabled) EnableGlobalKeywordSafe(keyword);
            else DisableGlobalKeywordSafe(keyword);
        }

        #endregion
    }
}