using System.Runtime.CompilerServices;
using UnityEngine;

namespace Nexus.Core.Extensions
{
    /// <summary>
    /// Extension methods for the <see cref="Texture"/> base class covering aspect ratio,
    /// UV/pixel coordinate conversion, and safe wrap/filter configuration.
    /// <para>Design rules:</para>
    /// <list type="bullet">
    /// <item>These methods operate only on properties defined at the <see cref="Texture"/>
    /// base level (<see cref="Texture.width"/>, <see cref="Texture.height"/>,
    /// <see cref="Texture.wrapMode"/>, <see cref="Texture.filterMode"/>), so they apply
    /// equally to <see cref="Texture2D"/>, <see cref="RenderTexture"/>, and
    /// <see cref="Cubemap"/> faces treated as 2D targets.</item>
    /// <item>UV/pixel conversions clamp their results to valid ranges instead of throwing,
    /// since sampling code driven by mouse position, procedural noise, or network data
    /// routinely produces slightly out-of-range coordinates.</item>
    /// <item>Division-by-zero guards return a safe fallback (typically <c>1</c> or
    /// <see cref="Vector2.zero"/>) rather than propagating <c>NaN</c>/<c>Infinity</c>,
    /// since a texture with a zero dimension is a real possibility for a not-yet-loaded
    /// asset or a failed <see cref="RenderTexture"/> allocation.</item>
    /// </list>
    /// </summary>
    public static class TextureExtensions
    {
        #region Aspect Ratio & Shape

        /// <summary>
        /// Width divided by height, or <c>1</c> if the height is zero. <br/>
        /// Used to fit a texture into a UI <see cref="RectTransform"/> or world-space quad
        /// without stretching, without risking a divide-by-zero on a texture that failed to
        /// load and reports a height of <c>0</c>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetAspectRatio(this Texture texture)
            => texture.height != 0 ? (float)texture.width / texture.height : 1f;

        /// <summary>
        /// True if width and height are equal. <br/>
        /// Useful for validating that an imported icon, minimap render target, or shadow
        /// map is square before it is bound to a shader that assumes uniform UV scaling.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsSquare(this Texture texture) => texture.width == texture.height;

        /// <summary>
        /// True if the texture is wider than it is tall. <br/>
        /// Drives layout decisions for dynamically generated UI, such as choosing a
        /// horizontal versus vertical frame for a screenshot thumbnail.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsLandscape(this Texture texture) => texture.width > texture.height;

        /// <summary>
        /// True if the texture is taller than it is wide.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsPortrait(this Texture texture) => texture.height > texture.width;

        /// <summary>
        /// Size, in UV units, of a single texel: <c>(1/width, 1/height)</c>. Returns
        /// <see cref="Vector2.zero"/> if either dimension is zero. <br/>
        /// Feeds shader-side edge-detection, outline, and blur kernels that need to offset
        /// sample coordinates by exactly one texel regardless of the texture's actual resolution.
        /// </summary>
        public static Vector2 GetTexelSize(this Texture texture)
        {
            if (texture.width == 0 || texture.height == 0) return Vector2.zero;
            return new Vector2(1f / texture.width, 1f / texture.height);
        }

        #endregion

        #region UV & Pixel Conversion

        /// <summary>
        /// Converts a normalized UV coordinate to integer pixel coordinates, clamped to the
        /// texture's valid pixel range. <br/>
        /// The clamp matters at the exact edges: a UV of <c>(1, 1)</c> naively multiplied by
        /// dimensions gives an out-of-range pixel index equal to <see cref="Texture.width"/>,
        /// which throws when used to index a pixel array. Clamping keeps edge-of-texture
        /// sampling (common in cursor-driven paint tools and heightmap editors) safe.
        /// </summary>
        public static Vector2Int UVToPixel(this Texture texture, Vector2 uv)
        {
            int x = Mathf.Clamp(Mathf.FloorToInt(uv.x * texture.width), 0, texture.width - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(uv.y * texture.height), 0, texture.height - 1);
            return new Vector2Int(x, y);
        }

        /// <summary>
        /// Converts integer pixel coordinates to a normalized UV coordinate at the pixel's
        /// centre. <br/>
        /// Sampling at the pixel centre, rather than its corner, avoids the half-texel bias
        /// that otherwise causes visibly inconsistent results between
        /// <see cref="Texture2D.GetPixel(int, int)"/>-style lookups and shader-side
        /// <c>tex2D</c> sampling of the same logical pixel.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 PixelToUV(this Texture texture, Vector2Int pixel)
            => new Vector2((pixel.x + 0.5f) / texture.width, (pixel.y + 0.5f) / texture.height);

        #endregion

        #region Wrap & Filter Configuration

        /// <summary>
        /// Sets both <see cref="Texture.wrapModeU"/> and <see cref="Texture.wrapModeV"/> to
        /// the same value in one call. <br/>
        /// Most gameplay code wants uniform wrapping behavior and has no reason to set the
        /// two axes independently; this collapses the two-property boilerplate into a single
        /// intention-revealing call.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetWrapModeUniform(this Texture texture, TextureWrapMode mode)
            => texture.wrapMode = mode;

        /// <summary>
        /// Configures the texture for crisp, non-blurred sampling: <see cref="FilterMode.Point"/>
        /// filtering and no mip bias. <br/>
        /// The standard one-line setup for pixel-art sprites, retro UI icons, and data
        /// textures (heightmaps, ID maps) where bilinear blending between texels would
        /// corrupt the encoded values.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConfigureForPixelArt(this Texture texture)
        {
            texture.filterMode = FilterMode.Point;
            texture.anisoLevel = 0;
        }

        #endregion
    }
}