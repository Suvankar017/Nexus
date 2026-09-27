using System.Collections;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Nexus.Core.Extensions
{
    /// <summary>
    /// Extension methods for <see cref="Material"/> covering safe property access, keyword
    /// toggling, emission control, texture tiling/scrolling, render queue management, and
    /// coroutine-driven property fades.
    /// <para>Design rules:</para>
    /// <list type="bullet">
    /// <item>All property setters accept a pre-cached <c>int</c> property ID obtained via
    /// <see cref="Shader.PropertyToID(string)"/>. Passing a raw string every call re-hashes
    /// it internally on every single invocation; callers should cache the ID once (typically
    /// as a <c>static readonly</c> field) exactly as they would cache an <see cref="Animator"/>
    /// parameter hash.</item>
    /// <item>Every setter guards with <see cref="Material.HasProperty(int)"/> before writing.
    /// Shared shaders often have optional slots (a rim-light color, a dissolve amount) that
    /// not every material variant exposes; without this guard, Unity logs a console warning
    /// per call and silently no-ops anyway, so the guard costs nothing extra and keeps the
    /// console clean.</item>
    /// <item>Numeric setters reject <c>NaN</c>. A <c>NaN</c> shader property does not throw —
    /// it silently corrupts the GPU-side render state for that material, producing invisible
    /// or garbled geometry with no console warning at all.</item>
    /// <item>This library caches the property IDs for common built-in and pipeline-specific
    /// property names (<c>_Color</c>, <c>_BaseColor</c>, <c>_EmissionColor</c>, <c>_MainTex</c>)
    /// once as <c>static readonly</c> fields, since <see cref="Shader.PropertyToID(string)"/>
    /// is safe to call at class-load time but wasteful to call per-frame.</item>
    /// <item>The <c>Fade*</c> methods return <see cref="IEnumerator"/> for use with
    /// <see cref="MonoBehaviour.StartCoroutine(IEnumerator)"/>. Like the fade helpers in
    /// <c>CanvasGroupExtensions</c>, <c>AudioSourceExtensions</c>, and
    /// <c>SpriteRendererExtensions</c>, this is a deliberate exception to the zero-allocation
    /// rule: shader property fades (dissolve, hit-flash, force-field pulse) run once per
    /// event, not per frame across hundreds of materials.</item>
    /// <item>These methods operate on whatever <see cref="Material"/> instance is passed in.
    /// They do not create instances themselves. Calling them on a <see cref="Renderer.sharedMaterial"/>
    /// mutates the asset for every object using it; callers must obtain
    /// <see cref="Renderer.material"/> first if per-instance mutation is intended.</item>
    /// </list>
    /// </summary>
    public static class MaterialExtensions
    {
        /// <summary>
        /// Cached property ID for <c>_Color</c>, the base color property on the Built-in
        /// Render Pipeline's Standard shader and most legacy shaders.
        /// </summary>
        private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");

        /// <summary>
        /// Cached property ID for <c>_BaseColor</c>, the base color property on
        /// Universal/High Definition Render Pipeline Lit shaders.
        /// </summary>
        private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");

        /// <summary>
        /// Cached property ID for <c>_EmissionColor</c>, the HDR emission color property
        /// shared by Built-in, URP, and HDRP lit shaders.
        /// </summary>
        private static readonly int EmissionColorPropertyId = Shader.PropertyToID("_EmissionColor");

        /// <summary>
        /// Cached property ID for <c>_MainTex</c>, the primary texture slot on Built-in
        /// shaders. URP/HDRP Lit shaders instead use <c>_BaseMap</c>, which is not cached
        /// here since tiling/scrolling call sites should cache the correct ID for their
        /// active pipeline explicitly.
        /// </summary>
        private static readonly int MainTexPropertyId = Shader.PropertyToID("_MainTex");

        /// <summary>
        /// Shader keyword that enables emission sampling on Built-in, URP, and HDRP lit
        /// shaders. Setting <c>_EmissionColor</c> without also enabling this keyword has no
        /// visible effect, which is one of the most common sources of "my emissive material
        /// isn't glowing" bug reports.
        /// </summary>
        private const string EmissionKeyword = "_EMISSION";

        /// <summary>
        /// Minimum valid value for <see cref="Material.renderQueue"/> as documented by Unity.
        /// </summary>
        private const int MinRenderQueue = 0;

        /// <summary>
        /// Maximum valid value for <see cref="Material.renderQueue"/> as documented by Unity.
        /// </summary>
        private const int MaxRenderQueue = 5000;

        /// <summary>
        /// Sentinel value for <see cref="Material.renderQueue"/> meaning "use the value
        /// baked into the shader," restoring default sort behavior.
        /// </summary>
        private const int ShaderDefaultRenderQueue = -1;

        #region Safe Property Setters

        /// <summary>
        /// Sets a float property only if it exists on the material and the value is finite. <br/>
        /// Guards against both the console-spam case (property missing on this shader
        /// variant) and the silent-corruption case (<c>NaN</c>/<c>Infinity</c> reaching the
        /// GPU), which together cover the two most common shader-property bugs.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetFloatSafe(this Material material, int propertyId, float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return;
            if (material.HasProperty(propertyId)) material.SetFloat(propertyId, value);
        }

        /// <summary>
        /// Sets an integer property only if it exists on the material. <br/>
        /// Useful for enum-style shader switches (e.g. a blend-mode index) shared across a
        /// family of materials that don't all expose the same property set.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetIntSafe(this Material material, int propertyId, int value)
        {
            if (material.HasProperty(propertyId)) material.SetInt(propertyId, value);
        }

        /// <summary>
        /// Sets a color property only if it exists on the material. <br/>
        /// Safe for both LDR albedo colors and HDR colors (emission, glow) — Unity's
        /// <see cref="Material.SetColor(int, Color)"/> does not clamp HDR intensity, so
        /// values above <c>1</c> are preserved for bloom and glow effects.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetColorSafe(this Material material, int propertyId, Color value)
        {
            if (material.HasProperty(propertyId)) material.SetColor(propertyId, value);
        }

        /// <summary>
        /// Sets a <see cref="Vector4"/> property only if it exists on the material and every
        /// component is finite. <br/>
        /// Vector4 properties commonly encode packed shader data (a direction plus an
        /// intensity, a min/max range pair); a single <c>NaN</c> component corrupts the
        /// whole pack silently.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetVectorSafe(this Material material, int propertyId, Vector4 value)
        {
            if (float.IsNaN(value.x) || float.IsNaN(value.y) || float.IsNaN(value.z) || float.IsNaN(value.w)) return;
            if (material.HasProperty(propertyId)) material.SetVector(propertyId, value);
        }

        /// <summary>
        /// Sets a texture property only if it exists on the material. <br/>
        /// Setting a texture on a nonexistent property is otherwise a silent no-op with a
        /// console warning; this guard removes the warning and makes the failure inspectable
        /// via the material's shader if needed.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetTextureSafe(this Material material, int propertyId, Texture value)
        {
            if (material.HasProperty(propertyId)) material.SetTexture(propertyId, value);
        }

        #endregion

        #region Universal Alpha & Color

        /// <summary>
        /// Sets the alpha channel of whichever base-color property this material actually
        /// exposes — <c>_BaseColor</c> (URP/HDRP) or <c>_Color</c> (Built-in) — clamped to
        /// <c>0</c>–<c>1</c>. <br/>
        /// Fade and hit-flash code written once often needs to run across projects or
        /// materials that target different render pipelines; this method removes the need
        /// to know which pipeline a given material belongs to. Returns <c>false</c> if
        /// neither property exists.
        /// </summary>
        public static bool SetAlphaUniversal(this Material material, float alpha)
        {
            if (float.IsNaN(alpha)) return false;
            float clamped = Mathf.Clamp01(alpha);

            if (material.HasProperty(BaseColorPropertyId))
            {
                Color c = material.GetColor(BaseColorPropertyId);
                c.a = clamped;
                material.SetColor(BaseColorPropertyId, c);
                return true;
            }

            if (material.HasProperty(ColorPropertyId))
            {
                Color c = material.GetColor(ColorPropertyId);
                c.a = clamped;
                material.SetColor(ColorPropertyId, c);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Reads the alpha channel of whichever base-color property this material exposes,
        /// or <paramref name="fallback"/> if neither <c>_BaseColor</c> nor <c>_Color</c> is present. <br/>
        /// The read-side counterpart to <see cref="SetAlphaUniversal"/>, useful for capturing
        /// a starting alpha before a fade without knowing the material's render pipeline.
        /// </summary>
        public static float GetAlphaUniversalOr(this Material material, float fallback)
        {
            if (material.HasProperty(BaseColorPropertyId)) return material.GetColor(BaseColorPropertyId).a;
            if (material.HasProperty(ColorPropertyId)) return material.GetColor(ColorPropertyId).a;
            return fallback;
        }

        #endregion

        #region Emission

        /// <summary>
        /// Enables the <c>_EMISSION</c> keyword and sets <c>_EmissionColor</c> to
        /// <paramref name="baseColor"/> scaled by <paramref name="intensity"/>. <br/>
        /// Setting the color property alone has no visible effect on Built-in, URP, or HDRP
        /// lit shaders until this keyword is enabled — one of the most common causes of "my
        /// glowing material doesn't glow" bugs. Negative intensity is clamped to zero.
        /// <para>
        /// This only sets the render-time property. For the emissive surface to also
        /// contribute to realtime or baked global illumination, additionally set
        /// <see cref="Material.globalIlluminationFlags"/> on the material asset.
        /// </para>
        /// </summary>
        public static void SetEmissionIntensity(this Material material, Color baseColor, float intensity)
        {
            if (!material.HasProperty(EmissionColorPropertyId)) return;
            if (float.IsNaN(intensity)) return;

            material.EnableKeywordSafe(EmissionKeyword);
            material.SetColor(EmissionColorPropertyId, baseColor * Mathf.Max(0f, intensity));
        }

        /// <summary>
        /// Disables the <c>_EMISSION</c> keyword and zeroes <c>_EmissionColor</c>. <br/>
        /// Fully reverting emission requires both steps: leaving the keyword enabled with a
        /// zero color can still cost a small amount of shader evaluation, and leaving a
        /// stale nonzero color with the keyword off can cause a visible flash if something
        /// re-enables the keyword later without resetting the color first.
        /// </summary>
        public static void ClearEmission(this Material material)
        {
            material.DisableKeywordSafe(EmissionKeyword);
            if (material.HasProperty(EmissionColorPropertyId)) material.SetColor(EmissionColorPropertyId, Color.black);
        }

        #endregion

        #region Keywords

        /// <summary>
        /// Enables <paramref name="keyword"/> only if it is not already enabled and the
        /// string is non-empty. <br/>
        /// Guards against the null/empty case, which throws inside Unity's native keyword
        /// system, and skips redundant calls so intent stays explicit at call sites that
        /// toggle keywords every frame (e.g. driving a rim-light keyword from gameplay state).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EnableKeywordSafe(this Material material, string keyword)
        {
            if (string.IsNullOrEmpty(keyword)) return;
            if (!material.IsKeywordEnabled(keyword)) material.EnableKeyword(keyword);
        }

        /// <summary>
        /// Disables <paramref name="keyword"/> only if it is currently enabled and the
        /// string is non-empty. <br/>
        /// Symmetric counterpart to <see cref="EnableKeywordSafe"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DisableKeywordSafe(this Material material, string keyword)
        {
            if (string.IsNullOrEmpty(keyword)) return;
            if (material.IsKeywordEnabled(keyword)) material.DisableKeyword(keyword);
        }

        /// <summary>
        /// Enables or disables <paramref name="keyword"/> based on <paramref name="enabled"/>. <br/>
        /// Lets a single bool — a settings toggle, a status-effect flag — drive a shader
        /// feature without an <c>if</c>/<c>else</c> at every call site.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetKeywordEnabled(this Material material, string keyword, bool enabled)
        {
            if (enabled) material.EnableKeywordSafe(keyword);
            else material.DisableKeywordSafe(keyword);
        }

        #endregion

        #region Texture Tiling & Scrolling

        /// <summary>
        /// Sets the tiling (scale) of a texture property, guarded by <see cref="Material.HasProperty(int)"/>. <br/>
        /// Thin wrapper kept for naming symmetry with the rest of this class's <c>*Safe</c>
        /// setters, since <see cref="Material.SetTextureScale(int, Vector2)"/> silently
        /// no-ops (with a console warning) on a missing property just like the others.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetTextureScaleSafe(this Material material, int propertyId, Vector2 scale)
        {
            if (material.HasProperty(propertyId)) material.SetTextureScale(propertyId, scale);
        }

        /// <summary>
        /// Sets the offset of a texture property, guarded by <see cref="Material.HasProperty(int)"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetTextureOffsetSafe(this Material material, int propertyId, Vector2 offset)
        {
            if (material.HasProperty(propertyId)) material.SetTextureOffset(propertyId, offset);
        }

        /// <summary>
        /// Scrolls a texture property's offset over time at <paramref name="unitsPerSecond"/>,
        /// wrapped into the <c>0</c>–<c>1</c> range using <see cref="Mathf.Repeat(float, float)"/>. <br/>
        /// Drives water, lava, energy-field, and conveyor-belt shaders. Wrapping the offset
        /// every frame — rather than letting it grow unbounded from <c>Time.time * speed</c> —
        /// avoids the floating-point precision loss that otherwise causes visibly jittering
        /// UVs after a play session lasting more than a few hours.
        /// <para>
        /// Call once per frame, typically from <c>Update</c>, passing <see cref="Time.deltaTime"/>.
        /// </para>
        /// </summary>
        public static void ScrollTextureOffset(this Material material, int propertyId, Vector2 unitsPerSecond, float deltaTime)
        {
            if (!material.HasProperty(propertyId)) return;

            Vector2 current = material.GetTextureOffset(propertyId);
            Vector2 next = current + unitsPerSecond * deltaTime;
            next.x = Mathf.Repeat(next.x, 1f);
            next.y = Mathf.Repeat(next.y, 1f);
            material.SetTextureOffset(propertyId, next);
        }

        /// <summary>
        /// Convenience overload of <see cref="ScrollTextureOffset(Material, int, Vector2, float)"/>
        /// targeting the Built-in pipeline's <c>_MainTex</c> property. <br/>
        /// For URP/HDRP Lit shaders, cache and pass the <c>_BaseMap</c> property ID to the
        /// primary overload instead, since those pipelines do not use <c>_MainTex</c>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ScrollMainTexture(this Material material, Vector2 unitsPerSecond, float deltaTime)
            => material.ScrollTextureOffset(MainTexPropertyId, unitsPerSecond, deltaTime);

        #endregion

        #region Render Queue

        /// <summary>
        /// Sets <see cref="Material.renderQueue"/>, clamped to Unity's documented
        /// <c>0</c>–<c>5000</c> range. <br/>
        /// An out-of-range render queue does not throw but can silently break transparency
        /// sorting against other materials in the scene; clamping keeps a runtime-computed
        /// queue offset (e.g. for dynamic sorting layers) from drifting outside the valid range.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetRenderQueueClamped(this Material material, int queue)
            => material.renderQueue = Mathf.Clamp(queue, MinRenderQueue, MaxRenderQueue);

        /// <summary>
        /// Restores <see cref="Material.renderQueue"/> to the value baked into the shader. <br/>
        /// Pairs with <see cref="SetRenderQueueClamped"/> to guarantee a temporary sort-order
        /// override (used, for example, to force a "ghost" material to always render on top)
        /// cannot be left permanently applied to a pooled or shared material instance.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ResetRenderQueue(this Material material)
            => material.renderQueue = ShaderDefaultRenderQueue;

        #endregion

        #region Fade Transitions

        /// <summary>
        /// Coroutine that linearly interpolates a float property from its current value to
        /// <paramref name="targetValue"/> over <paramref name="duration"/> seconds. <br/>
        /// Run with <see cref="MonoBehaviour.StartCoroutine(IEnumerator)"/>. Non-finite or
        /// non-positive durations, and non-finite targets, resolve the value immediately
        /// instead of dividing by zero or looping forever. <br/>
        /// Drives shader-based transitions such as a dissolve amount, a force-field pulse,
        /// or a freeze/burn overlay strength.
        /// </summary>
        public static IEnumerator FadeFloatTo(this Material material, int propertyId, float targetValue, float duration, bool useUnscaledTime = false)
        {
            if (!material.HasProperty(propertyId)) yield break;

            float target = float.IsNaN(targetValue) ? material.GetFloat(propertyId) : targetValue;

            if (float.IsNaN(duration) || duration <= 0f)
            {
                material.SetFloat(propertyId, target);
                yield break;
            }

            float start = material.GetFloat(propertyId);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                material.SetFloat(propertyId, Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / duration)));
                yield return null;
            }

            material.SetFloat(propertyId, target);
        }

        /// <summary>
        /// Coroutine that linearly interpolates a color property from its current value to
        /// <paramref name="targetColor"/> over <paramref name="duration"/> seconds. <br/>
        /// Works for both LDR and HDR color properties (e.g. <c>_EmissionColor</c>), making
        /// it suitable for a hit-flash pulse or a fading glow effect.
        /// </summary>
        public static IEnumerator FadeColorTo(this Material material, int propertyId, Color targetColor, float duration, bool useUnscaledTime = false)
        {
            if (!material.HasProperty(propertyId)) yield break;

            if (float.IsNaN(duration) || duration <= 0f)
            {
                material.SetColor(propertyId, targetColor);
                yield break;
            }

            Color start = material.GetColor(propertyId);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                material.SetColor(propertyId, Color.Lerp(start, targetColor, Mathf.Clamp01(elapsed / duration)));
                yield return null;
            }

            material.SetColor(propertyId, targetColor);
        }

        #endregion

        #region Validation & Instancing

        /// <summary>
        /// True if the material's shader compiled successfully and is not Unity's built-in
        /// pink error shader. <br/>
        /// Catching this at load time turns a silently broken (pink) material into an
        /// inspectable failure, rather than a bug report that just says "this object looks wrong."
        /// </summary>
        public static bool HasValidShader(this Material material)
            => material.shader != null && material.shader.name != "Hidden/InternalErrorShader";

        /// <summary>
        /// Heuristically detects whether this material is a per-renderer instance created by
        /// accessing <see cref="Renderer.material"/>, rather than a shared asset. <br/>
        /// Unity automatically appends <c>" (Instance)"</c> to the name of any material it
        /// duplicates this way. This is a naming convention, not a guaranteed API contract —
        /// a material manually renamed to end with that suffix would produce a false positive —
        /// but it is the same heuristic the Unity community and Editor tooling commonly rely
        /// on to catch accidental material-instance leaks that silently increase draw calls
        /// and memory usage.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsLikelyInstance(this Material material)
            => material.name.EndsWith(" (Instance)");

        #endregion
    }
}