using System.Runtime.CompilerServices;
using UnityEngine;

namespace Nexus.Core.Extensions
{
    /// <summary>
    /// Extension methods for <see cref="Texture2D"/> covering safe pixel access, texture
    /// duplication, and solid-color construction.
    /// <para>Design rules:</para>
    /// <list type="bullet">
    /// <item><see cref="Texture2D.GetPixel(int, int)"/>, <see cref="Texture2D.SetPixel(int, int, Color)"/>,
    /// and <see cref="Texture2D.GetPixels"/> throw <see cref="UnityException"/> when
    /// <see cref="Texture2D.isReadable"/> is <c>false</c> — the default for most imported
    /// textures. Every pixel-access method here checks readability first and fails safely
    /// (returning a fallback color or a <c>bool</c>) instead of crashing gameplay code that
    /// runs on textures whose import settings were changed after the code was written.</item>
    /// <item>Coordinate-clamping variants exist because procedural sampling (noise-based
    /// terrain painting, flood fill, edge detection) routinely computes neighbor coordinates
    /// that fall one step outside the texture bounds; clamping is almost always the correct
    /// behavior at an edge, and is far cheaper than branching at every call site.</item>
    /// <item>Bulk pixel operations (<see cref="Clone"/>, <see cref="CreateSolid"/>) are
    /// exempt from the zero-allocation rule: they exist specifically to allocate a new
    /// texture or pixel array, and are expected to run at load time or on user action, not
    /// per frame.</item>
    /// <item><see cref="Texture2D.Apply(bool, bool)"/> uploads pixel data to the GPU and is
    /// relatively expensive; batch multiple <c>SetPixel</c> calls and apply once, which every
    /// method here that writes multiple pixels already does internally.</item>
    /// </list>
    /// </summary>
    public static class Texture2DExtensions
    {
        #region Safe Pixel Access

        /// <summary>
        /// True if pixel data can be read from or written to this texture on the CPU. <br/>
        /// Check this before any pixel-level operation. A texture imported with "Read/Write"
        /// disabled (the default, to save memory) throws on every method in this region
        /// otherwise, and that setting can be changed by an artist after gameplay code
        /// was written and tested against it.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsReadableSafe(this Texture2D texture) => texture.isReadable;

        /// <summary>
        /// Reads the pixel at <paramref name="x"/>, <paramref name="y"/>, clamped to the
        /// texture's valid coordinate range, returning <paramref name="fallback"/> if the
        /// texture is not readable. <br/>
        /// The clamp makes this safe to call with an out-of-range neighbor coordinate — for
        /// example <c>(x - 1, y)</c> at the left edge of a heightmap during erosion or blur
        /// processing — without a separate bounds check at every call site.
        /// </summary>
        public static Color GetPixelClamped(this Texture2D texture, int x, int y, Color fallback = default)
        {
            if (!texture.isReadable) return fallback;

            int cx = Mathf.Clamp(x, 0, texture.width - 1);
            int cy = Mathf.Clamp(y, 0, texture.height - 1);
            return texture.GetPixel(cx, cy);
        }

        /// <summary>
        /// Writes <paramref name="color"/> to the pixel at <paramref name="x"/>, <paramref name="y"/>,
        /// clamped to the texture's valid coordinate range, returning <c>false</c> without
        /// effect if the texture is not readable. <br/>
        /// Does not call <see cref="Texture2D.Apply(bool, bool)"/>; batch several writes and
        /// apply once via <see cref="ApplySafe"/> to avoid uploading to the GPU on every
        /// single pixel change.
        /// </summary>
        public static bool SetPixelClamped(this Texture2D texture, int x, int y, Color color)
        {
            if (!texture.isReadable) return false;

            int cx = Mathf.Clamp(x, 0, texture.width - 1);
            int cy = Mathf.Clamp(y, 0, texture.height - 1);
            texture.SetPixel(cx, cy, color);
            return true;
        }

        /// <summary>
        /// Samples the pixel nearest to normalized UV coordinate <paramref name="uv"/>,
        /// returning <paramref name="fallback"/> if the texture is not readable. <br/>
        /// Point-sampled rather than bilinear, making it the correct choice for reading data
        /// textures (splat maps, ID maps, heightmaps encoded in color channels) where
        /// blending between texels would corrupt the encoded value — unlike
        /// <see cref="Texture2D.GetPixelBilinear(float, float)"/>, which is meant for visual
        /// color sampling.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color SampleUV(this Texture2D texture, Vector2 uv, Color fallback = default)
        {
            if (!texture.isReadable) return fallback;
            Vector2Int pixel = texture.UVToPixel(uv);
            return texture.GetPixel(pixel.x, pixel.y);
        }

        /// <summary>
        /// Calls <see cref="Texture2D.Apply(bool, bool)"/> only if the texture is readable,
        /// returning <c>false</c> instead of throwing otherwise. <br/>
        /// Wraps the single most common crash point in runtime texture editing tools —
        /// forgetting that <c>Apply</c> also requires read/write access — behind a safe,
        /// inspectable boolean result.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ApplySafe(this Texture2D texture, bool updateMipmaps = true, bool makeNoLongerReadable = false)
        {
            if (!texture.isReadable) return false;
            texture.Apply(updateMipmaps, makeNoLongerReadable);
            return true;
        }

        #endregion

        #region Bulk Fill

        /// <summary>
        /// Fills every pixel with <paramref name="color"/> and uploads the result, returning
        /// <c>false</c> if the texture is not readable. <br/>
        /// Uses a single <see cref="Texture2D.SetPixels(Color[])"/> call rather than a
        /// per-pixel loop of <see cref="Texture2D.SetPixel(int, int, Color)"/>, which is
        /// dramatically faster for anything larger than a few dozen pixels — the difference
        /// between an instant fill and a visible hitch on a large procedural texture.
        /// </summary>
        public static bool FillSafe(this Texture2D texture, Color color)
        {
            if (!texture.isReadable) return false;

            Color[] pixels = new Color[texture.width * texture.height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = color;

            texture.SetPixels(pixels);
            texture.Apply();
            return true;
        }

        #endregion

        #region Duplication & Construction

        /// <summary>
        /// Creates a new, guaranteed-readable copy of this texture by rendering it through a
        /// temporary <see cref="RenderTexture"/> and reading the result back with
        /// <see cref="Texture2D.ReadPixels(Rect, int, int)"/>. <br/>
        /// This is the standard technique for obtaining pixel access to a texture whose
        /// import settings disable Read/Write — for example a texture loaded from an
        /// <c>AssetBundle</c> or streamed from disk at runtime — without needing to
        /// re-import the source asset.
        /// <para>
        /// Involves a GPU round-trip and a full pixel readback, both of which are
        /// comparatively expensive; use for one-off tool operations (screenshot processing,
        /// texture export) rather than per-frame calls.
        /// </para>
        /// </summary>
        public static Texture2D Clone(this Texture2D source)
        {
            RenderTexture temp = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            RenderTexture previousActive = RenderTexture.active;

            Graphics.Blit(source, temp);
            RenderTexture.active = temp;

            Texture2D copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            copy.Apply();

            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(temp);

            return copy;
        }

        /// <summary>
        /// Creates a new readable <see cref="Texture2D"/> of the given size filled entirely
        /// with <paramref name="color"/>. <br/>
        /// Useful for runtime placeholder icons, colored UI swatches (a team-color chip, a
        /// rarity-tier background), and unit-test fixtures that need a texture instance
        /// without a corresponding asset on disk.
        /// </summary>
        public static Texture2D CreateSolid(int width, int height, Color color)
        {
            Texture2D texture = new Texture2D(Mathf.Max(1, width), Mathf.Max(1, height), TextureFormat.RGBA32, false);
            texture.FillSafe(color);
            return texture;
        }

        #endregion
    }
}