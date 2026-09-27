using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Nexus.Core.Extensions
{
    /// <summary>
    /// Extension methods and scoped helpers for <see cref="RenderTexture"/> covering safe
    /// activation, temporary allocation, clearing, and pixel readback.
    /// <para>Design rules:</para>
    /// <list type="bullet">
    /// <item><see cref="RenderTexture.active"/> is a single global slot shared by the entire
    /// engine. Forgetting to restore it after a manual render (a common cause of a minimap
    /// or portal camera silently breaking every other UI element drawn afterward) is one of
    /// the most persistent bugs in Unity render-to-texture code. <see cref="ActiveScope"/>
    /// fixes this at the language level using a <c>using</c>-scoped struct that restores the
    /// previous active target automatically, even if an exception is thrown inside the block.</item>
    /// <item><see cref="RenderTexture.GetTemporary(int, int, int, RenderTextureFormat)"/> and
    /// <see cref="RenderTexture.ReleaseTemporary(RenderTexture)"/> must always be paired.
    /// A leaked temporary silently exhausts the internal RT pool over a play session, causing
    /// escalating allocation stalls. <see cref="TemporaryScope"/> pairs them automatically
    /// for the same reason <see cref="ActiveScope"/> pairs active-target swaps.</item>
    /// <item>All calls that touch <see cref="RenderTexture.active"/> or the RT's contents
    /// check <see cref="RenderTexture.IsCreated"/> first. Reading from or clearing a
    /// not-yet-created RT is a silent no-op on some platforms and a native error on others;
    /// checking up front makes the failure mode consistent and inspectable.</item>
    /// <item><see cref="ActiveScope"/> and <see cref="TemporaryScope"/> are structs
    /// implementing <see cref="IDisposable"/>, so used in a <c>using</c> statement they incur
    /// zero heap allocation — the C# compiler calls <see cref="IDisposable.Dispose"/>
    /// directly without boxing.</item>
    /// </list>
    /// </summary>
    public static class RenderTextureExtensions
    {
        #region Active Target Scope

        /// <summary>
        /// Disposable scope that sets <see cref="RenderTexture.active"/> to
        /// <paramref name="target"/> for its lifetime and restores the previously active
        /// target on <see cref="Dispose"/>. <br/>
        /// Use in a <c>using</c> block around any manual <c>GL</c>, <c>Graphics</c>, or
        /// <see cref="Texture2D.ReadPixels(Rect, int, int)"/> call that requires a specific
        /// render target — this guarantees the swap is undone even if the block throws,
        /// preventing every subsequent render call in the frame from silently drawing into
        /// the wrong target.
        /// </summary>
        public readonly struct ActiveScope : IDisposable
        {
            private readonly RenderTexture _previous;

            /// <summary>
            /// Captures the currently active render target and makes <paramref name="target"/>
            /// active in its place.
            /// </summary>
            public ActiveScope(RenderTexture target)
            {
                _previous = RenderTexture.active;
                RenderTexture.active = target;
            }

            /// <summary>
            /// Restores the render target that was active before this scope began.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose() => RenderTexture.active = _previous;
        }

        /// <summary>
        /// Begins an <see cref="ActiveScope"/> for this render texture. <br/>
        /// Typical usage: <c>using (rt.MakeActive()) { GL.Clear(...); }</c> — the previous
        /// active target is restored automatically when the block exits.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ActiveScope MakeActive(this RenderTexture rt) => new ActiveScope(rt);

        #endregion

        #region Temporary Allocation Scope

        /// <summary>
        /// Disposable scope that requests a pooled <see cref="RenderTexture"/> via
        /// <see cref="RenderTexture.GetTemporary(int, int, int, RenderTextureFormat)"/> on
        /// construction and releases it via <see cref="RenderTexture.ReleaseTemporary(RenderTexture)"/>
        /// on <see cref="Dispose"/>. <br/>
        /// Guarantees the get/release pair cannot be separated by an early return or an
        /// exception, which is otherwise the most common way a temporary RT leaks and slowly
        /// exhausts Unity's internal RT pool over a long play session.
        /// </summary>
        public readonly struct TemporaryScope : IDisposable
        {
            /// <summary>
            /// The pooled render texture obtained for the lifetime of this scope.
            /// </summary>
            public RenderTexture Texture { get; }

            /// <summary>
            /// Requests a temporary render texture of the given size, depth, and format.
            /// </summary>
            public TemporaryScope(int width, int height, int depthBits, RenderTextureFormat format)
            {
                Texture = RenderTexture.GetTemporary(width, height, depthBits, format);
            }

            /// <summary>
            /// Releases the temporary render texture back to Unity's internal pool.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose() => RenderTexture.ReleaseTemporary(Texture);
        }

        /// <summary>
        /// Begins a <see cref="TemporaryScope"/>, allocating a pooled RGBA32 render texture
        /// with no depth buffer. <br/>
        /// Typical usage: <c>using (var scope = RenderTextureExtensions.GetTemporaryScoped(w, h))
        /// { Graphics.Blit(src, scope.Texture); ... }</c> — the temporary is always released,
        /// even if the block throws.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TemporaryScope GetTemporaryScoped(int width, int height, int depthBits = 0, RenderTextureFormat format = RenderTextureFormat.ARGB32)
            => new TemporaryScope(width, height, depthBits, format);

        #endregion

        #region Creation & Release

        /// <summary>
        /// Ensures the render texture's native GPU resources exist, creating them only if
        /// necessary. <br/>
        /// Thin wrapper around the check-then-create pattern required before manually
        /// binding a <see cref="RenderTexture"/> that was never assigned to a
        /// <see cref="Camera.targetTexture"/> or used in a <see cref="Graphics.Blit(Texture, RenderTexture)"/>
        /// call, both of which create it implicitly.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EnsureCreated(this RenderTexture rt)
        {
            if (!rt.IsCreated()) rt.Create();
        }

        /// <summary>
        /// Releases the render texture's native GPU resources only if they currently exist. <br/>
        /// Calling <see cref="RenderTexture.Release"/> on an already-released or never-created
        /// texture is harmless, but this guard keeps intent explicit at pooling and cleanup
        /// call sites (e.g. a minimap system tearing down its render target on scene unload).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReleaseSafe(this RenderTexture rt)
        {
            if (rt.IsCreated()) rt.Release();
        }

        /// <summary>
        /// Releases and recreates the render texture at a new size. <br/>
        /// <see cref="RenderTexture.width"/>/<see cref="RenderTexture.height"/> cannot be
        /// changed while the native resource is allocated; this method performs the
        /// required release-resize-recreate sequence in the correct order, which is easy to
        /// get wrong (assigning the new size before releasing throws) when done manually for
        /// a dynamic-resolution or window-resize feature.
        /// </summary>
        public static void Resize(this RenderTexture rt, int width, int height)
        {
            rt.ReleaseSafe();
            rt.width = Mathf.Max(1, width);
            rt.height = Mathf.Max(1, height);
            rt.Create();
        }

        #endregion

        #region Clearing

        /// <summary>
        /// Clears the render texture to <paramref name="color"/> using
        /// <see cref="ActiveScope"/> to safely swap and restore the active render target. <br/>
        /// The correct way to reset a persistent render target — a trail-accumulation buffer,
        /// a fog-of-war texture — between uses without disturbing whatever the rest of the
        /// frame's rendering expects to be active.
        /// </summary>
        public static void ClearSafe(this RenderTexture rt, Color color, bool clearDepth = true, bool clearColor = true)
        {
            rt.EnsureCreated();
            using (rt.MakeActive())
            {
                GL.Clear(clearDepth, clearColor, color);
            }
        }

        #endregion

        #region Pixel Readback

        /// <summary>
        /// Reads the render texture's contents into a new readable <see cref="Texture2D"/>. <br/>
        /// This is the standard way to capture a screenshot from a camera's
        /// <see cref="Camera.targetTexture"/>, export a procedurally rendered texture to a
        /// PNG, or feed rendered output into CPU-side analysis (for example, sampling an
        /// occlusion buffer for AI line-of-sight checks).
        /// <para>
        /// Involves a full GPU-to-CPU pixel transfer, which stalls the render pipeline while
        /// it completes; use for one-off captures rather than per-frame calls. For per-frame
        /// GPU readback, use <see cref="AsyncGPUReadback"/> instead.
        /// </para>
        /// </summary>
        public static Texture2D ToTexture2D(this RenderTexture rt, TextureFormat format = TextureFormat.RGBA32)
        {
            rt.EnsureCreated();

            using (rt.MakeActive())
            {
                Texture2D result = new Texture2D(rt.width, rt.height, format, false);
                result.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                result.Apply();
                return result;
            }
        }

        #endregion

        #region Blit Helpers

        /// <summary>
        /// Blits <paramref name="source"/> onto this render texture only if both textures
        /// are non-null. <br/>
        /// Guards the most common cause of a mysteriously black post-processing or portal
        /// effect: a source or destination reference that went <c>null</c> after a scene
        /// transition or object pool return, which <see cref="Graphics.Blit(Texture, RenderTexture)"/>
        /// itself does not check for.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void BlitFromSafe(this RenderTexture destination, Texture source)
        {
            if (source == null || destination == null) return;
            Graphics.Blit(source, destination);
        }

        /// <summary>
        /// Blits <paramref name="source"/> onto this render texture through
        /// <paramref name="material"/>, only if the source and material are both non-null. <br/>
        /// The standard entry point for applying a full-screen shader effect (color grading,
        /// dissolve, chromatic aberration) to an intermediate render target.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void BlitFromSafe(this RenderTexture destination, Texture source, Material material)
        {
            if (source == null || destination == null || material == null) return;
            Graphics.Blit(source, destination, material);
        }

        #endregion

        #region Queries

        /// <summary>
        /// True if the render texture uses a floating-point or half-precision format
        /// (HDR-capable), rather than a standard 8-bit-per-channel format. <br/>
        /// Check this before assuming pixel values read back via <see cref="ToTexture2D"/>
        /// are in the standard <c>0</c>–<c>1</c> range — HDR render targets routinely
        /// contain values above <c>1</c> that a non-HDR <see cref="Texture2D"/> destination
        /// format would silently clip.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsHdrFormat(this RenderTexture rt)
            => rt.format == RenderTextureFormat.ARGBFloat
            || rt.format == RenderTextureFormat.ARGBHalf
            || rt.format == RenderTextureFormat.RGB111110Float;

        #endregion
    }
}