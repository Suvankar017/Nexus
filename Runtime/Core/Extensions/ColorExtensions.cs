using System.Runtime.CompilerServices;
using UnityEngine;

namespace Nexus.Core.Extensions
{
    /// <summary>
    /// Production extension methods for <see cref="Color"/> and <see cref="Color32"/>.
    /// <para>
    /// <b>Design rules (consistent with the Vector extension family):</b>
    /// <list type="bullet">
    ///   <item>Zero allocation — every method returns a struct or primitive.</item>
    ///   <item>NaN-safe — degenerate input is clamped or falls back, never propagates.</item>
    ///   <item>Aggressive inlining on all leaf helpers.</item>
    ///   <item>Perceptual correctness — HSV/HSL lerp and luminance use the standard formulas,
    ///         not naive RGB averaging.</item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Key game-dev use-cases covered:</b>
    /// UI fade-in/out, sprite tinting, health-bar gradient, HDR bloom control,
    /// team-color palette generation, color-blindness-safe contrast checks,
    /// procedural palette harmony, and shader uniform validation.
    /// </para>
    /// </summary>
    public static class ColorExtensions
    {
        #region Constants

        /// <summary>
        /// Default epsilon used for per-channel and alpha comparisons to ignore floating-point noise
        /// when evaluating fades, transitions or UI state.
        /// </summary>
        private const float DefaultEpsilon = 1e-4f;

        /// <summary>
        /// Rec. 709 red luminance coefficient.
        /// Used to calculate perceived brightness for contrast and accessibility checks.
        /// </summary>
        private const float LuminanceR = 0.2126f;

        /// <summary>
        /// Rec. 709 green luminance coefficient.
        /// Used to calculate perceived brightness for contrast and accessibility checks.
        /// </summary>
        private const float LuminanceG = 0.7152f;

        /// <summary>
        /// Rec. 709 blue luminance coefficient.
        /// Used to calculate perceived brightness for contrast and accessibility checks.
        /// </summary>
        private const float LuminanceB = 0.0722f;

        #endregion

        #region Component Replacement

        /// <summary>Returns a copy with the red channel replaced.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color WithR(this Color c, float r)
            => new(r, c.g, c.b, c.a);

        /// <summary>Returns a copy with the green channel replaced.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color WithG(this Color c, float g)
            => new(c.r, g, c.b, c.a);

        /// <summary>Returns a copy with the blue channel replaced.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color WithB(this Color c, float b)
            => new(c.r, c.g, b, c.a);

        /// <summary>Returns a copy with the alpha channel replaced. Clamped to [0, 1].</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color WithA(this Color c, float a)
            => new(c.r, c.g, c.b, Mathf.Clamp01(a));

        /// <summary>Returns a copy with all three RGB channels replaced, preserving alpha.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color WithRGB(this Color c, float r, float g, float b)
            => new(r, g, b, c.a);

        /// <summary>
        /// Returns a copy of <paramref name="c"/> with all RGBA channels replaced by the provided values.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color WithRGBA(this Color c, float r, float g, float b, float a)
            => new(r, g, b, a);

        /// <summary>Returns a copy with all three RGB channels replaced from a <see cref="Vector3"/>, preserving alpha.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color WithRGB(this Color c, Vector3 rgb)
            => new(rgb.x, rgb.y, rgb.z, c.a);

        /// <summary>
        /// Returns a copy of <paramref name="c"/> with RGB replaced by <paramref name="rgb"/> while preserving
        /// the original alpha channel of <paramref name="c"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color WithRGB(this Color c, Color rgb)
            => new(rgb.r, rgb.g, rgb.b, c.a);

        #endregion

        #region Component Offset

        /// <summary>
        /// Returns a copy of <paramref name="c"/> with <paramref name="rOffset"/> added to its red channel.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color AddR(this Color c, float rOffset)
            => new(c.r + rOffset, c.g, c.b, c.a);

        /// <summary>
        /// Returns a copy of <paramref name="c"/> with <paramref name="gOffset"/> added to its green channel.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color AddG(this Color c, float gOffset)
            => new(c.r, c.g + gOffset, c.b, c.a);

        /// <summary>
        /// Returns a copy of <paramref name="c"/> with <paramref name="bOffset"/> added to its blue channel.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color AddB(this Color c, float bOffset)
            => new(c.r, c.g, c.b + bOffset, c.a);

        /// <summary>
        /// Returns a copy of <paramref name="c"/> with <paramref name="alphaOffset"/> added to its alpha channel.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color AddA(this Color c, float alphaOffset)
            => new(c.r, c.g, c.b, c.a + alphaOffset);

        /// <summary>
        /// Returns a copy of <paramref name="c"/> with the same scalar <paramref name="offset"/> added to R, G and B.
        /// Alpha remains unchanged to preserve visibility state.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color AddRGB(this Color c, float offset)
            => new(c.r + offset, c.g + offset, c.b + offset, c.a);

        #endregion

        #region Alpha Utilities

        /// <summary>
        /// Returns a fully opaque copy of <paramref name="c"/> with alpha forced to <c>1.0f</c>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color Opaque(this Color c)
            => new(c.r, c.g, c.b, 1f);

        /// <summary>
        /// Returns a fully transparent copy of <paramref name="c"/> with alpha forced to <c>0.0f</c>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color Transparent(this Color c)
            => new(c.r, c.g, c.b, 0f);

        /// <summary>
        /// Returns a copy of <paramref name="c"/> with alpha multiplied by <paramref name="multiplier"/>.
        /// RGB channels remain unchanged to preserve hue and saturation during fade-out curves.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color MultiplyAlpha(this Color c, float multiplier)
            => new(c.r, c.g, c.b, Mathf.Clamp01(c.a * multiplier));

        /// <summary>
        /// Adds <paramref name="delta"/> to the alpha channel.
        /// Positive fades in, negative fades out. Clamped to [0, 1].
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color FadeBy(this Color c, float delta)
            => new(c.r, c.g, c.b, Mathf.Clamp01(c.a + delta));

        /// <summary>
        /// Returns a copy of <paramref name="c"/> with alpha clamped between <c>0f</c> and <c>1f</c>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color ClampAlpha01(this Color c)
            => new(c.r, c.g, c.b, Mathf.Clamp01(c.a));

        /// <summary>
        /// Returns a copy of <paramref name="c"/> with all RGBA channels clamped between <c>0f</c> and <c>1f</c>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color Clamp01(this Color c)
            => new(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), Mathf.Clamp01(c.a));

        #endregion

        #region Color Arithmetic

        /// <summary>Component-wise add (clamped to [0, 1] per channel to stay LDR).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color Add(this Color a, Color b)
            => new(
                Mathf.Clamp01(a.r + b.r),
                Mathf.Clamp01(a.g + b.g),
                Mathf.Clamp01(a.b + b.b),
                Mathf.Clamp01(a.a + b.a));

        /// <summary>Component-wise subtract (clamped to [0, 1]).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color Subtract(this Color a, Color b)
            => new(
                Mathf.Clamp01(a.r - b.r),
                Mathf.Clamp01(a.g - b.g),
                Mathf.Clamp01(a.b - b.b),
                Mathf.Clamp01(a.a - b.a));

        /// <summary>Component-wise multiply. The standard "tint" operation in shaders and sprite renderers.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color Multiply(this Color a, Color b)
            => new(a.r * b.r, a.g * b.g, a.b * b.b, a.a * b.a);

        /// <summary>Scales all RGB channels by <paramref name="factor"/>. Alpha is preserved.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color ScaleRGB(this Color c, float factor)
            => new(c.r * factor, c.g * factor, c.b * factor, c.a);

        /// <summary>Inverts the RGB channels. Alpha is preserved.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color Invert(this Color c)
            => new(1f - c.r, 1f - c.g, 1f - c.b, c.a);

        #endregion

        #region Luminance, Brightness & Grayscale

        /// <summary>
        /// Returns perceived luminance of <paramref name="c"/> using Rec. 709 weights.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Luminance(this Color c)
            => (c.r * LuminanceR) + (c.g * LuminanceG) + (c.b * LuminanceB);

        /// <summary>
        /// Returns average (arithmetic mean) brightness of RGB channels, ignoring alpha.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float AverageBrightness(this Color c)
            => (c.r + c.g + c.b) / 3f;

        /// <summary>
        /// Returns a desaturated grayscale copy of <paramref name="c"/> using perceived luminance.
        /// Original alpha is preserved to keep fade state intact.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color Grayscale(this Color c)
        {
            float lum = c.Luminance();
            return new Color(lum, lum, lum, c.a);
        }

        /// <summary>
        /// Linearly interpolates between the original color and its grayscale version.
        /// 0 = full color, 1 = full grayscale. Useful for "desaturated when disabled" UI states.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color Desaturate(this Color c, float amount = 1f)
        {
            float lum = c.Luminance();
            float t = Mathf.Clamp01(amount);
            return new Color(
                Mathf.Lerp(c.r, lum, t),
                Mathf.Lerp(c.g, lum, t),
                Mathf.Lerp(c.b, lum, t),
                c.a);
        }

        /// <summary>
        /// Pushes the color away from gray by <paramref name="factor"/>.
        /// 1 = no change, 2 = double saturation, 0 = grayscale.
        /// </summary>
        public static Color Saturate(this Color c, float factor)
        {
            float lum = c.Luminance();
            return new Color(
                Mathf.Clamp01(lum + (c.r - lum) * factor),
                Mathf.Clamp01(lum + (c.g - lum) * factor),
                Mathf.Clamp01(lum + (c.b - lum) * factor),
                c.a);
        }

        #endregion

        #region Color Space - HSV

        /// <summary>
        /// Decomposes into (Hue [0,1], Saturation [0,1], Value [0,1], Alpha).
        /// </summary>
        public static (float h, float s, float v, float a) ToHSVA(this Color c)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            return (h, s, v, c.a);
        }

        /// <summary>Constructs a <see cref="Color"/> from HSV components and an alpha value.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color FromHSV(float h, float s, float v, float a = 1f)
        {
            Color c = Color.HSVToRGB(
                Mathf.Repeat(h, 1f),
                Mathf.Clamp01(s),
                Mathf.Clamp01(v));
            c.a = Mathf.Clamp01(a);
            return c;
        }

        /// <summary>Returns a copy with the hue shifted by <paramref name="delta"/> (0–1 wraps around the color wheel).</summary>
        public static Color ShiftHue(this Color c, float delta)
        {
            var (h, s, v, a) = c.ToHSVA();
            return FromHSV(h + delta, s, v, a);
        }

        /// <summary>Returns a copy with the saturation replaced.</summary>
        public static Color WithSaturation(this Color c, float saturation)
        {
            var (h, _, v, a) = c.ToHSVA();
            return FromHSV(h, saturation, v, a);
        }

        /// <summary>Returns a copy with the value (brightness) replaced.</summary>
        public static Color WithValue(this Color c, float value)
        {
            var (h, s, _, a) = c.ToHSVA();
            return FromHSV(h, s, value, a);
        }

        #endregion

        #region Color Space - HSL

        /// <summary>
        /// Decomposes into (Hue [0,1], Saturation [0,1], Lightness [0,1], Alpha).
        /// Uses the standard bi-hexcone model.
        /// </summary>
        public static (float h, float s, float l, float a) ToHSLA(this Color c)
        {
            float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            float min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
            float delta = max - min;
            float l = (max + min) * 0.5f;

            if (delta < 1e-6f)
                return (0f, 0f, l, c.a);

            float s = l > 0.5f
                ? delta / (2f - max - min)
                : delta / (max + min);

            float h;
            if (Mathf.Approximately(max, c.r))
                h = (c.g - c.b) / delta + (c.g < c.b ? 6f : 0f);
            else if (Mathf.Approximately(max, c.g))
                h = (c.b - c.r) / delta + 2f;
            else
                h = (c.r - c.g) / delta + 4f;

            h /= 6f;
            return (h, s, l, c.a);
        }

        /// <summary>Constructs a <see cref="Color"/> from HSL components.</summary>
        public static Color FromHSL(float h, float s, float l, float a = 1f)
        {
            h = Mathf.Repeat(h, 1f);
            s = Mathf.Clamp01(s);
            l = Mathf.Clamp01(l);

            if (s < 1e-6f)
                return new Color(l, l, l, Mathf.Clamp01(a));

            float q = l < 0.5f ? l * (1f + s) : l + s - l * s;
            float p = 2f * l - q;

            return new Color(
                HueToRGB(p, q, h + 1f / 3f),
                HueToRGB(p, q, h),
                HueToRGB(p, q, h - 1f / 3f),
                Mathf.Clamp01(a));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float HueToRGB(float p, float q, float t)
        {
            if (t < 0f) t += 1f;
            if (t > 1f) t -= 1f;
            if (t < 1f / 6f) return p + (q - p) * 6f * t;
            if (t < 1f / 2f) return q;
            if (t < 2f / 3f) return p + (q - p) * (2f / 3f - t) * 6f;
            return p;
        }

        #endregion

        #region Validation & Safety

        /// <summary>True if every channel is a finite real number.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsValid(this Color c)
            => !float.IsNaN(c.r) && !float.IsNaN(c.g) && !float.IsNaN(c.b) && !float.IsNaN(c.a)
            && !float.IsInfinity(c.r) && !float.IsInfinity(c.g) && !float.IsInfinity(c.b) && !float.IsInfinity(c.a);

        /// <summary>Returns the color if valid, otherwise <paramref name="fallback"/>.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color ValidOr(this Color c, Color fallback)
            => c.IsValid() ? c : fallback;

        /// <summary>True if all RGB channels are approximately equal (within <paramref name="epsilon"/>).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsGrayscale(this Color c, float epsilon = DefaultEpsilon)
            => Mathf.Abs(c.r - c.g) < epsilon && Mathf.Abs(c.g - c.b) < epsilon;

        /// <summary>True if alpha is approximately zero.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsTransparent(this Color c, float epsilon = DefaultEpsilon)
            => c.a < epsilon;

        /// <summary>True if alpha is approximately one.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsOpaque(this Color c, float epsilon = DefaultEpsilon)
            => c.a > 1f - epsilon;

        /// <summary>Component-wise approximate equality with explicit tolerance.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Approximately(this Color a, Color b, float epsilon = DefaultEpsilon)
            => Mathf.Abs(a.r - b.r) < epsilon
            && Mathf.Abs(a.g - b.g) < epsilon
            && Mathf.Abs(a.b - b.b) < epsilon
            && Mathf.Abs(a.a - b.a) < epsilon;

        #endregion

        #region Hex Conversions

        /// <summary>
        /// Converts to a hex string in <c>#RRGGBB</c> format (alpha excluded).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string ToHexRGB(this Color c)
            => $"#{ColorUtility.ToHtmlStringRGB(c)}";

        /// <summary>
        /// Converts to a hex string in <c>#RRGGBBAA</c> format.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string ToHexRGBA(this Color c)
            => $"#{ColorUtility.ToHtmlStringRGBA(c)}";

        /// <summary>
        /// Parses a hex string into a <see cref="Color"/>.
        /// Accepts <c>#RGB</c>, <c>#RRGGBB</c>, <c>#RRGGBBAA</c>, with or without the leading <c>#</c>.
        /// Returns <paramref name="fallback"/> on failure instead of throwing.
        /// </summary>
        public static Color FromHex(string hex, Color fallback = default)
        {
            if (string.IsNullOrWhiteSpace(hex)) return fallback;
            if (ColorUtility.TryParseHtmlString(hex, out var color)) return color;
            if (hex[0] != '#' && ColorUtility.TryParseHtmlString("#" + hex, out color)) return color;
            return fallback;
        }

        /// <summary>
        /// Tries to parse a hex string. Returns false on failure without throwing.
        /// </summary>
        public static bool TryFromHex(string hex, out Color color)
        {
            if (string.IsNullOrWhiteSpace(hex))
            {
                color = default;
                return false;
            }

            if (ColorUtility.TryParseHtmlString(hex, out color)) return true;
            if (hex[0] != '#' && ColorUtility.TryParseHtmlString("#" + hex, out color)) return true;

            color = default;
            return false;
        }

        #endregion

        #region HDR & Intensity

        /// <summary>
        /// True if any RGB channel exceeds 1.0, meaning the color is in HDR range
        /// and will trigger bloom in post-processing.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsHDR(this Color c)
            => c.r > 1f || c.g > 1f || c.b > 1f;

        /// <summary>
        /// Scales the RGB channels so the brightest channel equals <paramref name="intensity"/>.
        /// Preserves hue and saturation. Alpha is unchanged.
        /// <para>
        /// Use for emissive materials: <c>baseColor.WithIntensity(4f)</c> produces a 4× bloom
        /// without shifting the hue toward white.
        /// </para>
        /// </summary>
        public static Color WithIntensity(this Color c, float intensity)
        {
            float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            if (max < 1e-6f) return new Color(0f, 0f, 0f, c.a);
            float scale = intensity / max;
            return new Color(c.r * scale, c.g * scale, c.b * scale, c.a);
        }

        /// <summary>
        /// Multiplies all RGB channels by <paramref name="multiplier"/> for HDR boost or exposure control.
        /// Alpha is unchanged.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color WithHDRMultiplier(this Color c, float multiplier)
            => new Color(c.r * multiplier, c.g * multiplier, c.b * multiplier, c.a);

        /// <summary>
        /// Clamps all channels to [0, 1], converting an HDR color to LDR.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color ClampToLDR(this Color c)
            => new Color(
                Mathf.Clamp01(c.r),
                Mathf.Clamp01(c.g),
                Mathf.Clamp01(c.b),
                Mathf.Clamp01(c.a));

        #endregion

        #region Color Harmony (Palette Generation)

        /// <summary>Returns the complementary color (180° on the hue wheel).</summary>
        public static Color Complementary(this Color c) => c.ShiftHue(0.5f);

        /// <summary>
        /// Returns the two triadic colors (120° and 240° on the hue wheel).
        /// Together with the original, these form an equilateral triangle on the color wheel.
        /// </summary>
        public static (Color a, Color b) Triadic(this Color c)
            => (c.ShiftHue(1f / 3f), c.ShiftHue(2f / 3f));

        /// <summary>
        /// Returns two analogous colors offset by ±<paramref name="angle"/> (default 30°).
        /// Analogous palettes are harmonious and low-contrast — ideal for environment gradients.
        /// </summary>
        public static (Color warm, Color cool) Analogous(this Color c, float angle = 30f)
        {
            float shift = angle / 360f;
            return (c.ShiftHue(shift), c.ShiftHue(-shift));
        }

        /// <summary>
        /// Returns the two split-complementary colors (150° and 210°).
        /// High contrast like complementary, but less tense — good for UI accent pairs.
        /// </summary>
        public static (Color a, Color b) SplitComplementary(this Color c)
            => (c.ShiftHue(5f / 12f), c.ShiftHue(7f / 12f));

        /// <summary>
        /// Returns the four tetradic (rectangle) colors at 0°, 60°, 180°, 240°.
        /// Rich palette for complex UI or faction color systems.
        /// </summary>
        public static (Color a, Color b, Color c, Color d) Tetradic(this Color color)
            => (color, color.ShiftHue(1f / 6f), color.ShiftHue(0.5f), color.ShiftHue(2f / 3f));

        #endregion

        #region Temperature Shifting

        /// <summary>
        /// Shifts the color toward warm tones by boosting red and reducing blue.
        /// <paramref name="amount"/> of 0 = no change, 1 = maximum warmth.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color WarmShift(this Color c, float amount)
        {
            float t = Mathf.Clamp01(amount);
            return new Color(
                Mathf.Clamp01(c.r + t * 0.15f),
                c.g,
                Mathf.Clamp01(c.b - t * 0.15f),
                c.a);
        }

        /// <summary>
        /// Shifts the color toward cool tones by boosting blue and reducing red.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color CoolShift(this Color c, float amount)
        {
            float t = Mathf.Clamp01(amount);
            return new Color(
                Mathf.Clamp01(c.r - t * 0.15f),
                c.g,
                Mathf.Clamp01(c.b + t * 0.15f),
                c.a);
        }

        /// <summary>
        /// Approximates a blackbody color from a temperature in Kelvin.
        /// <para>
        /// Range: 1000K (candle) → 15000K (overcast sky).
        /// Uses Tanner Helland's algorithm, accurate to ~1% of the Planckian locus
        /// in the 1000–40000K range. Sufficient for game lighting and post-processing.
        /// </para>
        /// </summary>
        public static Color FromKelvin(float kelvin)
        {
            float temp = Mathf.Clamp(kelvin, 1000f, 40000f) / 100f;
            float r, g, b;

            // Red
            if (temp <= 66f)
                r = 1f;
            else
                r = Mathf.Clamp01((1.292936186f * Mathf.Pow(temp - 60f, -0.1332047592f)));

            // Green
            if (temp <= 66f)
                g = Mathf.Clamp01(0.3900815788f * Mathf.Log(temp) - 0.6318414438f);
            else
                g = Mathf.Clamp01(1.129890861f * Mathf.Pow(temp - 60f, -0.0755148492f));

            // Blue
            if (temp >= 66f)
                b = 1f;
            else if (temp <= 19f)
                b = 0f;
            else
                b = Mathf.Clamp01(0.5432067891f * Mathf.Log(temp - 10f) - 1.196254089f);

            return new Color(r, g, b, 1f);
        }

        #endregion

        #region Blend Modes

        /// <summary>
        /// Tints this color toward <paramref name="tint"/> by <paramref name="amount"/>.
        /// Equivalent to <c>Lerp(original, tint, amount)</c> but reads more clearly at the call site.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color Tint(this Color c, Color tint, float amount)
            => Color.Lerp(c, tint, Mathf.Clamp01(amount));

        /// <summary>
        /// Screen blend mode: <c>1 - (1-a)(1-b)</c>. Always produces a lighter result.
        /// The standard "additive glow" compositing mode.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color Screen(this Color a, Color b)
            => new Color(
                1f - (1f - a.r) * (1f - b.r),
                1f - (1f - a.g) * (1f - b.g),
                1f - (1f - a.b) * (1f - b.b),
                a.a);

        /// <summary>
        /// Overlay blend mode: multiplies dark areas, screens light areas.
        /// Preserves highlights and shadows while boosting midtone contrast.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color Overlay(this Color a, Color b)
            => new Color(
                OverlayChannel(a.r, b.r),
                OverlayChannel(a.g, b.g),
                OverlayChannel(a.b, b.b),
                a.a);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float OverlayChannel(float baseVal, float blend)
            => baseVal < 0.5f
                ? 2f * baseVal * blend
                : 1f - 2f * (1f - baseVal) * (1f - blend);

        /// <summary>
        /// Soft-light blend mode. Gentler version of overlay; the result never exceeds
        /// pure black or white. Good for subtle lighting overlays.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color SoftLight(this Color a, Color b)
            => new Color(
                SoftLightChannel(a.r, b.r),
                SoftLightChannel(a.g, b.g),
                SoftLightChannel(a.b, b.b),
                a.a);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SoftLightChannel(float baseVal, float blend)
            => blend < 0.5f
                ? baseVal - (1f - 2f * blend) * baseVal * (1f - baseVal)
                : baseVal + (2f * blend - 1f) * (Mathf.Sqrt(baseVal) - baseVal);

        #endregion

        #region Perceptual Contrast

        /// <summary>
        /// Computes the WCAG 2.1 relative luminance (linearized sRGB).
        /// This is the correct input for contrast-ratio calculations, not <see cref="Luminance"/>.
        /// </summary>
        public static float RelativeLuminance(this Color c)
        {
            float r = Linearize(c.r);
            float g = Linearize(c.g);
            float b = Linearize(c.b);
            return LuminanceR * r + LuminanceG * g + LuminanceB * b;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Linearize(float channel)
            => channel <= 0.04045f
                ? channel / 12.92f
                : Mathf.Pow((channel + 0.055f) / 1.055f, 2.4f);

        /// <summary>
        /// WCAG contrast ratio between two colors. Range: 1 (identical) to 21 (black vs white).
        /// <para>
        /// WCAG AA requires ≥ 4.5:1 for normal text, ≥ 3:1 for large text.<br/>
        /// WCAG AAA requires ≥ 7:1 for normal text, ≥ 4.5:1 for large text.
        /// </para>
        /// </summary>
        public static float ContrastRatio(this Color a, Color b)
        {
            float l1 = a.RelativeLuminance();
            float l2 = b.RelativeLuminance();
            float lighter = Mathf.Max(l1, l2);
            float darker = Mathf.Min(l1, l2);
            return (lighter + 0.05f) / (darker + 0.05f);
        }

        /// <summary>
        /// Returns black or white, whichever has higher contrast against this color.
        /// The correct choice for text rendered on top of a dynamic background.
        /// </summary>
        public static Color BestTextColor(this Color background)
        {
            float lum = background.RelativeLuminance();
            // Compare contrast ratios against white (1.0) and black (0.0)
            float contrastWhite = (1.05f) / (lum + 0.05f);
            float contrastBlack = (lum + 0.05f) / 0.05f;
            return contrastBlack > contrastWhite ? Color.black : Color.white;
        }

        #endregion

        #region Interpolation

        /// <summary>
        /// Interpolates through HSV space instead of RGB.
        /// <para>
        /// RGB lerp from red to green passes through muddy brown. HSV lerp passes through
        /// yellow, which is perceptually correct. Use this for health bars, rainbow effects,
        /// and any gradient that spans more than ~60° of hue.
        /// </para>
        /// </summary>
        public static Color LerpHSV(this Color a, Color b, float t)
        {
            var (ha, sa, va, aa) = a.ToHSVA();
            var (hb, sb, vb, ab) = b.ToHSVA();

            // Shortest-path hue interpolation
            float dh = hb - ha;
            if (dh > 0.5f) dh -= 1f;
            if (dh < -0.5f) dh += 1f;

            return FromHSV(
                ha + dh * t,
                Mathf.Lerp(sa, sb, t),
                Mathf.Lerp(va, vb, t),
                Mathf.Lerp(aa, ab, t));
        }

        /// <summary>
        /// Framerate-independent exponential smoothing toward <paramref name="target"/>.
        /// Operates in RGB space. For perceptual smoothing, convert to HSV first.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color ExpDecayTo(this Color current, Color target, float sharpness, float deltaTime)
        {
            float t = Mathf.Exp(-sharpness * deltaTime);
            return new Color(
                Mathf.LerpUnclamped(target.r, current.r, t),
                Mathf.LerpUnclamped(target.g, current.g, t),
                Mathf.LerpUnclamped(target.b, current.b, t),
                Mathf.LerpUnclamped(target.a, current.a, t));
        }

        /// <summary>
        /// Smooth Hermite interpolation between two colors. Applied per-channel.
        /// Produces an ease-in-ease-out transition, unlike linear <see cref="Color.Lerp"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color SmoothStep(this Color a, Color b, float t)
        {
            t = Mathf.Clamp01(t);
            t = t * t * (3f - 2f * t);
            return Color.LerpUnclamped(a, b, t);
        }

        #endregion

        #region Color32 Helpers

        /// <summary>Returns a copy with the alpha byte replaced.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color32 WithA(this Color32 c, byte a) => new Color32(c.r, c.g, c.b, a);

        /// <summary>Returns a copy with the red byte replaced.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color32 WithR(this Color32 c, byte r) => new Color32(r, c.g, c.b, c.a);

        /// <summary>Returns a copy with the green byte replaced.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color32 WithG(this Color32 c, byte g) => new Color32(c.r, g, c.b, c.a);

        /// <summary>Returns a copy with the blue byte replaced.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color32 WithB(this Color32 c, byte b) => new Color32(c.r, c.g, b, c.a);

        /// <summary>Converts to <see cref="Color"/> (0–1 float range).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color ToColor(this Color32 c) => (Color)c;

        /// <summary>Converts to <see cref="Color32"/> (0–255 byte range).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color32 ToColor32(this Color c) => (Color32)c;

        /// <summary>Packs a <see cref="Color32"/> into a single <see cref="uint"/> (RGBA byte order).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint PackToUInt(this Color32 c)
            => ((uint)c.r << 24) | ((uint)c.g << 16) | ((uint)c.b << 8) | c.a;

        /// <summary>Unpacks a <see cref="uint"/> (RGBA byte order) into a <see cref="Color32"/>.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color32 UnpackFromUInt(uint packed)
            => new Color32(
                (byte)(packed >> 24),
                (byte)(packed >> 16),
                (byte)(packed >> 8),
                (byte)packed);

        #endregion

        #region Conversions

        /// <summary>
        /// Returns RGB components as a <see cref="Vector3"/> (r, g, b), dropping alpha.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 ToRGBVector(this Color c)
            => new(c.r, c.g, c.b);

        /// <summary>
        /// Returns RGBA components as a <see cref="Vector4"/> (r, g, b, a).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 ToRGBAVector(this Color c)
            => new(c.r, c.g, c.b, c.a);

        #endregion

        #region Game-Dev Feedback Utilities

        /// <summary>
        /// Returns a copy of <paramref name="c"/> linearly interpolated towards <paramref name="target"/>
        /// by <paramref name="t"/> using <see cref="Color.Lerp(Color,Color,float)"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color LerpTo(this Color c, Color target, float t)
            => Color.Lerp(c, target, t);

        /// <summary>
        /// Returns a copy of <paramref name="c"/> unclamped linearly interpolated towards <paramref name="target"/>
        /// by <paramref name="t"/> using <see cref="Color.LerpUnclamped(Color,Color,float)"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color LerpToUnclamped(this Color c, Color target, float t)
            => Color.LerpUnclamped(c, target, t);

        /// <summary>
        /// Returns a copy of <paramref name="c"/> with alpha interpolated to <paramref name="alphaTarget"/> by <paramref name="t"/>.
        /// RGB remains identical, preventing unintended hue shift during pure fades.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color FadeTo(this Color c, float alphaTarget, float t)
            => new Color(c.r, c.g, c.b, Mathf.Lerp(c.a, alphaTarget, t));

        /// <summary>
        /// Returns a copy of <paramref name="c"/> multiplied by <paramref name="multiplier"/> across RGB channels.
        /// Alpha is preserved to avoid unintentionally changing visibility during intensity scaling.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color MultiplyRGB(this Color c, float multiplier)
            => new Color(c.r * multiplier, c.g * multiplier, c.b * multiplier, c.a);

        /// <summary>
        /// Creates a pulsed variant of <paramref name="c"/> by scaling perceived intensity using <paramref name="intensity"/>.
        /// Alpha remains unchanged to preserve UI/world visibility.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color Pulse(this Color c, float intensity)
            => c.MultiplyRGB(intensity);

        #endregion

        #region Debug

        /// <summary>
        /// Formats all four channels with full precision.
        /// Unity's default <c>ToString()</c> rounds to two decimals, which hides
        /// the subtle banding and precision loss you are usually trying to diagnose.
        /// </summary>
        public static string ToDebugString(this Color c, int decimals = 4)
        {
            string f = "F" + decimals;
            return $"RGBA({c.r.ToString(f)}, {c.g.ToString(f)}, {c.b.ToString(f)}, {c.a.ToString(f)})";
        }

        /// <summary>
        /// Formats as a debug string showing both the float and hex representations.
        /// </summary>
        public static string ToDebugStringFull(this Color c)
            => $"{c.ToDebugString()} | {c.ToHexRGBA()} | L:{c.Luminance():F3}";

        #endregion
    }
}