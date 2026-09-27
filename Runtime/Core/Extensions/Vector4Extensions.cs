using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Nexus.Core.Extensions
{
    /// <summary>
    /// Production extension methods for <see cref="Vector4"/>.
    /// <para>
    /// <b>Primary use-cases in Unity:</b>
    /// <list type="bullet">
    ///   <item>Shader parameters (<see cref="Material.SetVector"/>, custom SRP properties)</item>
    ///   <item>Color data (implicit conversion to/from <see cref="Color"/>)</item>
    ///   <item>UV tiling + offset packed into a single property</item>
    ///   <item>Plane equations (nx, ny, nz, d)</item>
    ///   <item>Homogeneous clip-space coordinates (x, y, z, w)</item>
    ///   <item>Bone weights and animation blend factors</item>
    ///   <item>Compute shader structured-buffer payloads</item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Design rules (consistent with <see cref="Vector3Extensions"/> and <see cref="Vector2Extensions"/>):</b>
    /// zero allocation, no hidden square roots, NaN-safe, aggressive inlining.
    /// </para>
    /// </summary>
    public static class Vector4Extensions
    {
        /// <summary>Squared-magnitude threshold below which a vector is treated as degenerate.</summary>
        private const float SqrEpsilon = 1e-10f;

        /// <summary>Default tolerance for component-wise float comparison.</summary>
        private const float DefaultEpsilon = 1e-5f;

        #region Component Replacement

        /// <summary>Returns a copy with X component replaced.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 WithX(this Vector4 v, float x) => new(x, v.y, v.z, v.w);

        /// <summary>Returns a copy with Y component replaced.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 WithY(this Vector4 v, float y) => new(v.x, y, v.z, v.w);

        /// <summary>Returns a copy with Z component replaced.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 WithZ(this Vector4 v, float z) => new(v.x, v.y, z, v.w);

        /// <summary>Returns a copy with W component replaced.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 WithW(this Vector4 v, float w) => new(v.x, v.y, v.z, w);

        /// <summary>Returns a copy with X and Y replaced.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 WithXY(this Vector4 v, float x, float y) => new(x, y, v.z, v.w);

        /// <summary>Returns a copy with X and Z replaced.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 WithXZ(this Vector4 v, float x, float z) => new(x, v.y, z, v.w);

        /// <summary>Returns a copy with X and W replaced.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 WithXW(this Vector4 v, float x, float w) => new(x, v.y, v.z, w);

        /// <summary>Returns a copy with Y and Z replaced.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 WithYZ(this Vector4 v, float y, float z) => new(v.x, y, z, v.w);

        /// <summary>Returns a copy with Y and W replaced.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 WithYW(this Vector4 v, float y, float w) => new(v.x, y, v.z, w);

        /// <summary>Returns a copy with Z and W replaced.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 WithZW(this Vector4 v, float z, float w) => new(v.x, v.y, z, w);

        /// <summary>Returns a copy with X, Y, and Z replaced (leaving W intact, e.g. preserving alpha or homogeneous w).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 WithXYZ(this Vector4 v, float x, float y, float z) => new(x, y, z, v.w);

        #endregion

        #region Component Offset

        /// <summary>Returns a copy with <paramref name="amount"/> added to X.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 AddX(this Vector4 v, float amount) => new(v.x + amount, v.y, v.z, v.w);

        /// <summary>Returns a copy with <paramref name="amount"/> added to Y.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 AddY(this Vector4 v, float amount) => new(v.x, v.y + amount, v.z, v.w);

        /// <summary>Returns a copy with <paramref name="amount"/> added to Z.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 AddZ(this Vector4 v, float amount) => new(v.x, v.y, v.z + amount, v.w);

        /// <summary>Returns a copy with <paramref name="amount"/> added to W.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 AddW(this Vector4 v, float amount) => new(v.x, v.y, v.z, v.w + amount);

        /// <summary>Returns a copy offset by the given per-axis amounts.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 Add(this Vector4 v, float x = 0f, float y = 0f, float z = 0f, float w = 0f)
            => new(v.x + x, v.y + y, v.z + z, v.w + w);

        #endregion

        #region Swizzle / Projection To Lower Dimensions

        /// <summary>
        /// Replaces the XYZ components from a <see cref="Vector3"/>, preserving W.
        /// Useful when you have a 3D position and a separate weight/alpha to pack.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 WithXYZ(this Vector4 v, Vector3 xyz) => new(xyz.x, xyz.y, xyz.z, v.w);

        /// <summary>
        /// Replaces the XY components from a <see cref="Vector2"/>, preserving ZW.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 WithXY(this Vector4 v, Vector2 xy) => new(xy.x, xy.y, v.z, v.w);

        /// <summary>
        /// Replaces the ZW components from a <see cref="Vector2"/>, preserving XY.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 WithZW(this Vector4 v, Vector2 zw) => new(v.x, v.y, zw.x, zw.y);

        #endregion

        #region Distance (no sqrt; use for comparisons)

        /// <summary>Squared Euclidean distance in 4D space.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SqrDistanceTo(this Vector4 from, Vector4 to)
        {
            float dx = to.x - from.x;
            float dy = to.y - from.y;
            float dz = to.z - from.z;
            float dw = to.w - from.w;
            return dx * dx + dy * dy + dz * dz + dw * dw;
        }

        /// <summary>Euclidean distance in 4D space.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DistanceTo(this Vector4 from, Vector4 to)
            => Mathf.Sqrt(from.SqrDistanceTo(to));

        #endregion

        #region RANGE CHECKS (sqrt-free)

        /// <summary>True if <paramref name="to"/> lies within <paramref name="range"/> in 4D space.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsInRangeOf(this Vector4 from, Vector4 to, float range)
            => from.SqrDistanceTo(to) <= range * range;

        #endregion

        #region NORMALIZATION (NaN-safe)

        /// <summary>
        /// Normalizes without producing NaN on a zero-length vector.
        /// Short-circuits before the square root.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 SafeNormalized(this Vector4 v)
        {
            float sqrMag = v.sqrMagnitude;
            if (sqrMag < SqrEpsilon) return Vector4.zero;
            return v / Mathf.Sqrt(sqrMag);
        }

        /// <summary>Normalizes, or returns <paramref name="fallback"/> if the vector is degenerate.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 NormalizedOr(this Vector4 v, Vector4 fallback)
        {
            float sqrMag = v.sqrMagnitude;
            if (sqrMag < SqrEpsilon) return fallback;
            return v / Mathf.Sqrt(sqrMag);
        }

        /// <summary>Returns the vector scaled to exactly <paramref name="length"/>. Zero-safe.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 WithMagnitude(this Vector4 v, float length)
            => v.SafeNormalized() * length;

        #endregion

        #region COMPONENT-WISE ARITHMETIC

        /// <summary>Component-wise multiply.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 Multiply(this Vector4 a, Vector4 b)
            => new(a.x * b.x, a.y * b.y, a.z * b.z, a.w * b.w);

        /// <summary>
        /// Component-wise divide. Any axis whose divisor is ~0 yields 0 rather than infinity,
        /// preventing NaN propagation into shader uniforms.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 Divide(this Vector4 a, Vector4 b)
            => new(
                Mathf.Abs(b.x) < Mathf.Epsilon ? 0f : a.x / b.x,
                Mathf.Abs(b.y) < Mathf.Epsilon ? 0f : a.y / b.y,
                Mathf.Abs(b.z) < Mathf.Epsilon ? 0f : a.z / b.z,
                Mathf.Abs(b.w) < Mathf.Epsilon ? 0f : a.w / b.w);

        /// <summary>Per-component absolute value.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 Abs(this Vector4 v)
            => new(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z), Mathf.Abs(v.w));

        /// <summary>Per-component sign (−1, 0, or 1). Exact zero maps to zero.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 Sign(this Vector4 v)
            => new(
                v.x == 0f ? 0f : Mathf.Sign(v.x),
                v.y == 0f ? 0f : Mathf.Sign(v.y),
                v.z == 0f ? 0f : Mathf.Sign(v.z),
                v.w == 0f ? 0f : Mathf.Sign(v.w));

        /// <summary>Largest component value.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float MaxComponent(this Vector4 v)
            => Mathf.Max(Mathf.Max(v.x, v.y), Mathf.Max(v.z, v.w));

        /// <summary>Smallest component value.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float MinComponent(this Vector4 v)
            => Mathf.Min(Mathf.Min(v.x, v.y), Mathf.Min(v.z, v.w));

        /// <summary>Index of the largest component (0 = X, 1 = Y, 2 = Z, 3 = W).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int MaxComponentIndex(this Vector4 v)
        {
            int idx = 0;
            float max = v.x;
            if (v.y > max) { max = v.y; idx = 1; }
            if (v.z > max) { max = v.z; idx = 2; }
            if (v.w > max) { idx = 3; }
            return idx;
        }

        /// <summary>Index of the smallest component (0 = X, 1 = Y, 2 = Z, 3 = W).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int MinComponentIndex(this Vector4 v)
        {
            int idx = 0;
            float min = v.x;
            if (v.y < min) { min = v.y; idx = 1; }
            if (v.z < min) { min = v.z; idx = 2; }
            if (v.w < min) { idx = 3; }
            return idx;
        }

        /// <summary>Sum of all four components. Useful for blend-weight validation (should equal 1).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ComponentSum(this Vector4 v) => v.x + v.y + v.z + v.w;

        /// <summary>
        /// Returns the vector with all components normalized so they sum to 1.
        /// Essential for bone weights and blend-tree parameters that must form a partition of unity.
        /// Returns zero if the sum is degenerate.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 NormalizeSum(this Vector4 v)
        {
            float sum = v.ComponentSum();
            if (Mathf.Abs(sum) < Mathf.Epsilon) return Vector4.zero;
            return v / sum;
        }

        #endregion

        #region CLAMPING

        /// <summary>Clamps magnitude to at most <paramref name="maxLength"/>.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 ClampMagnitude(this Vector4 v, float maxLength)
            => ClampMagnitude(v, 0.0f, maxLength);

        /// <summary>
        /// Clamps magnitude into [<paramref name="minLength"/>, <paramref name="maxLength"/>].
        /// A zero-length input stays zero.
        /// </summary>
        public static Vector4 ClampMagnitude(this Vector4 v, float minLength, float maxLength)
        {
            float sqrMag = v.sqrMagnitude;
            if (sqrMag < SqrEpsilon) return Vector4.zero;

            float mag = Mathf.Sqrt(sqrMag);
            float clamped = Mathf.Clamp(mag, minLength, maxLength);
            return v * (clamped / mag);
        }

        /// <summary>Clamps every component into the same scalar range.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 ClampComponents(this Vector4 v, float min, float max)
            => new(
                Mathf.Clamp(v.x, min, max),
                Mathf.Clamp(v.y, min, max),
                Mathf.Clamp(v.z, min, max),
                Mathf.Clamp(v.w, min, max));

        /// <summary>Clamps every component into the corresponding per-axis range.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 ClampComponents(this Vector4 v, Vector4 min, Vector4 max)
            => new(
                Mathf.Clamp(v.x, min.x, max.x),
                Mathf.Clamp(v.y, min.y, max.y),
                Mathf.Clamp(v.z, min.z, max.z),
                Mathf.Clamp(v.w, min.w, max.w));

        /// <summary>
        /// Clamps all components to [0, 1]. The 4D equivalent of <see cref="Mathf.Clamp01"/>.
        /// Use before uploading color or weight data to a shader to prevent out-of-range artefacts.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 Clamp01(this Vector4 v)
            => new(
                Mathf.Clamp01(v.x),
                Mathf.Clamp01(v.y),
                Mathf.Clamp01(v.z),
                Mathf.Clamp01(v.w));

        #endregion

        #region SNAPPING

        /// <summary>Snaps each component to the nearest multiple of <paramref name="gridSize"/>.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 SnapToGrid(this Vector4 v, float gridSize)
        {
            if (gridSize <= 0f) return v;
            return new Vector4(
                Mathf.Round(v.x / gridSize) * gridSize,
                Mathf.Round(v.y / gridSize) * gridSize,
                Mathf.Round(v.z / gridSize) * gridSize,
                Mathf.Round(v.w / gridSize) * gridSize);
        }

        /// <summary>Snaps each component to its own per-axis grid. Pass 0 on an axis to leave it untouched.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 SnapToGrid(this Vector4 v, Vector4 gridSize)
            => new(
                gridSize.x > 0f ? Mathf.Round(v.x / gridSize.x) * gridSize.x : v.x,
                gridSize.y > 0f ? Mathf.Round(v.y / gridSize.y) * gridSize.y : v.y,
                gridSize.z > 0f ? Mathf.Round(v.z / gridSize.z) * gridSize.z : v.z,
                gridSize.w > 0f ? Mathf.Round(v.w / gridSize.w) * gridSize.w : v.w);

        #endregion

        #region ROUNDING

        /// <summary>Rounds each component to <paramref name="decimals"/> places. For save files and network quantization.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 RoundTo(this Vector4 v, int decimals)
        {
            float factor = Mathf.Pow(10f, decimals);
            return new Vector4(
                Mathf.Round(v.x * factor) / factor,
                Mathf.Round(v.y * factor) / factor,
                Mathf.Round(v.z * factor) / factor,
                Mathf.Round(v.w * factor) / factor);
        }

        /// <summary>Rounds each component to the nearest integer.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 RoundToInt(this Vector4 v)
            => new Vector4(Mathf.RoundToInt(v.x), Mathf.RoundToInt(v.y), Mathf.RoundToInt(v.z), Mathf.RoundToInt(v.w));

        /// <summary>Floors each component.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 Floor(this Vector4 v)
            => new Vector4(Mathf.Floor(v.x), Mathf.Floor(v.y), Mathf.Floor(v.z), Mathf.Floor(v.w));

        /// <summary>Ceils each component.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 Ceil(this Vector4 v)
            => new Vector4(Mathf.Ceil(v.x), Mathf.Ceil(v.y), Mathf.Ceil(v.z), Mathf.Ceil(v.w));

        /// <summary>Returns the fractional part of each component (value − floor(value)).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 Frac(this Vector4 v)
            => new Vector4(
                v.x - Mathf.Floor(v.x),
                v.y - Mathf.Floor(v.y),
                v.z - Mathf.Floor(v.z),
                v.w - Mathf.Floor(v.w));

        #endregion

        #region VALIDATION & COMPARISON

        /// <summary>
        /// True if every component is a finite real number.
        /// <para>
        /// A NaN uploaded to a shader uniform can silently corrupt the entire render pass,
        /// producing black screens or visual artefacts that are extremely difficult to trace.
        /// Validate before <see cref="Material.SetVector"/> whenever the value is computed.
        /// </para>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsValid(this Vector4 v)
            => !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z) && !float.IsNaN(v.w)
            && !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z) && !float.IsInfinity(v.w);

        /// <summary>Returns the vector if valid, otherwise <paramref name="fallback"/>.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 ValidOr(this Vector4 v, Vector4 fallback)
            => v.IsValid() ? v : fallback;

        /// <summary>Component-wise approximate equality with explicit tolerance.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Approximately(this Vector4 a, Vector4 b, float epsilon = DefaultEpsilon)
            => Mathf.Abs(a.x - b.x) < epsilon
            && Mathf.Abs(a.y - b.y) < epsilon
            && Mathf.Abs(a.z - b.z) < epsilon
            && Mathf.Abs(a.w - b.w) < epsilon;

        /// <summary>True if the vector is effectively zero-length.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNearZero(this Vector4 v, float sqrEpsilon = SqrEpsilon)
            => v.sqrMagnitude < sqrEpsilon;

        /// <summary>True if the vector is already unit length within tolerance.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNormalized(this Vector4 v, float epsilon = 1e-4f)
            => Mathf.Abs(v.sqrMagnitude - 1f) < epsilon;

        #endregion

        #region INTERPOLATION

        /// <summary>
        /// Inverse of <see cref="Vector4.Lerp"/>: how far <paramref name="value"/> sits along the
        /// segment A→B, projected onto that segment. Unclamped.
        /// </summary>
        public static float InverseLerp(this Vector4 value, Vector4 a, Vector4 b)
        {
            Vector4 ab = b - a;
            float sqrLen = ab.sqrMagnitude;
            if (sqrLen < SqrEpsilon) return 0f;
            return Vector4.Dot(value - a, ab) / sqrLen;
        }

        /// <summary>Same as <see cref="InverseLerp"/> but clamped to [0, 1].</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float InverseLerpClamped(this Vector4 value, Vector4 a, Vector4 b)
            => Mathf.Clamp01(value.InverseLerp(a, b));

        /// <summary>
        /// Framerate-independent exponential smoothing toward <paramref name="target"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 ExpDecayTo(this Vector4 current, Vector4 target, float sharpness, float deltaTime)
            => Vector4.LerpUnclamped(target, current, Mathf.Exp(-sharpness * deltaTime));

        /// <summary>
        /// Smooth Hermite interpolation between 0 and 1 when <paramref name="t"/> is in [0, 1].
        /// Applied per-component. Useful for easing shader transitions.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 SmoothStep(Vector4 edge0, Vector4 edge1, Vector4 t)
            => new(
                Mathf.SmoothStep(edge0.x, edge1.x, t.x),
                Mathf.SmoothStep(edge0.y, edge1.y, t.y),
                Mathf.SmoothStep(edge0.z, edge1.z, t.z),
                Mathf.SmoothStep(edge0.w, edge1.w, t.w));

        #endregion

        #region COLOR INTEROP

        /// <summary>
        /// Interprets this Vector4 as (R, G, B, A) and returns the corresponding <see cref="Color"/>.
        /// Components are clamped to [0, 1] to prevent HDR blowout when the source data is untrusted.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color ToColor(this Vector4 v)
            => new Color(
                Mathf.Clamp01(v.x),
                Mathf.Clamp01(v.y),
                Mathf.Clamp01(v.z),
                Mathf.Clamp01(v.w));

        /// <summary>
        /// Interprets this Vector4 as (R, G, B, A) and returns the corresponding <see cref="Color"/>
        /// <b>without</b> clamping, preserving HDR values above 1.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color ToColorHDR(this Vector4 v)
            => new(v.x, v.y, v.z, v.w);

        /// <summary>
        /// Converts a <see cref="Color"/> to a <see cref="Vector4"/> (R, G, B, A).
        /// Explicit alternative to the implicit cast for readability in generic or serialized contexts.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 ToVector4(this Color c)
            => new(c.r, c.g, c.b, c.a);

        /// <summary>
        /// Returns a copy with the alpha (W) component replaced.
        /// Convenience for the extremely common "same color, different opacity" pattern.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 WithAlpha(this Vector4 colorVector, float alpha)
            => new Vector4(colorVector.x, colorVector.y, colorVector.z, Mathf.Clamp01(alpha));

        /// <summary>
        /// Linearly interpolates the RGB channels (XYZ) while snapping the alpha (W) to
        /// 0 or 1 at the <paramref name="alphaThreshold"/>. Useful for dissolve effects
        /// where you want smooth color blending but hard opacity cutout.
        /// </summary>
        public static Vector4 LerpRGBWithAlphaCutout(
            Vector4 a, Vector4 b, float t, float alphaThreshold = 0.5f)
        {
            float alpha = Mathf.Lerp(a.w, b.w, t) >= alphaThreshold ? 1f : 0f;
            return new Vector4(
                Mathf.Lerp(a.x, b.x, t),
                Mathf.Lerp(a.y, b.y, t),
                Mathf.Lerp(a.z, b.z, t),
                alpha);
        }

        #endregion

        #region PLANE MATH

        /// <summary>
        /// Interprets this Vector4 as a plane equation (nx, ny, nz, d) and returns
        /// the signed distance from <paramref name="point"/> to the plane.
        /// Positive = in front of the normal, negative = behind.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SignedDistanceToPlane(this Vector4 plane, Vector3 point)
            => plane.x * point.x + plane.y * point.y + plane.z * point.z + plane.w;

        /// <summary>
        /// True if <paramref name="point"/> is on the positive (front) side of the plane.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsInFrontOfPlane(this Vector4 plane, Vector3 point)
            => plane.SignedDistanceToPlane(point) > 0f;

        /// <summary>
        /// True if <paramref name="point"/> is within <paramref name="tolerance"/> of the plane surface.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsOnPlane(this Vector4 plane, Vector3 point, float tolerance = 1e-4f)
            => Mathf.Abs(plane.SignedDistanceToPlane(point)) <= tolerance;

        /// <summary>
        /// Projects <paramref name="point"/> onto the plane defined by this Vector4 (nx, ny, nz, d).
        /// </summary>
        public static Vector3 ProjectPointOntoPlane(this Vector4 plane, Vector3 point)
        {
            Vector3 normal = new Vector3(plane.x, plane.y, plane.z);
            float sqrMag = normal.sqrMagnitude;
            if (sqrMag < SqrEpsilon) return point;
            float dist = plane.SignedDistanceToPlane(point);
            return point - normal * (dist / sqrMag);
        }

        /// <summary>
        /// Constructs a plane Vector4 (nx, ny, nz, d) from a normal and a point on the plane.
        /// The normal is normalized internally.
        /// </summary>
        public static Vector4 PlaneFromNormalAndPoint(Vector3 normal, Vector3 pointOnPlane)
        {
            Vector3 n = normal.normalized;
            float d = -Vector3.Dot(n, pointOnPlane);
            return new Vector4(n.x, n.y, n.z, d);
        }

        /// <summary>
        /// Constructs a plane Vector4 from three counter-clockwise points.
        /// </summary>
        public static Vector4 PlaneFromPoints(Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
            float d = -Vector3.Dot(normal, a);
            return new Vector4(normal.x, normal.y, normal.z, d);
        }

        #endregion

        #region HOMOGENEOUS COORDINATES

        /// <summary>
        /// Performs the perspective divide: returns (x/w, y/w, z/w).
        /// If W is near zero, returns the XYZ components unchanged to avoid infinity.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 PerspectiveDivide(this Vector4 clipSpace)
        {
            if (Mathf.Abs(clipSpace.w) < Mathf.Epsilon) return clipSpace.XYZ();
            float invW = 1f / clipSpace.w;
            return new Vector3(clipSpace.x * invW, clipSpace.y * invW, clipSpace.z * invW);
        }

        /// <summary>
        /// Converts a world-space <see cref="Vector3"/> to homogeneous coordinates with W = 1.
        /// The standard representation for position vectors in a 4×4 matrix multiply.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 ToHomogeneous(this Vector3 position)
            => new Vector4(position.x, position.y, position.z, 1f);

        /// <summary>
        /// Converts a world-space direction to homogeneous coordinates with W = 0.
        /// Directions are unaffected by translation in a 4×4 matrix multiply.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 ToHomogeneousDirection(this Vector3 direction)
            => new Vector4(direction.x, direction.y, direction.z, 0f);

        #endregion

        #region UV TILING & OFFSET

        /// <summary>
        /// Packs a tiling scale and offset into the standard Unity ST format (tiling.xy, offset.xy).
        /// Matches the layout expected by <c>TRANSFORM_TEX</c> in shaders.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 PackTilingOffset(Vector2 tiling, Vector2 offset)
            => new(tiling.x, tiling.y, offset.x, offset.y);

        /// <summary>
        /// Extracts the tiling (XY) from a Unity ST-format Vector4.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 Tiling(this Vector4 st) => st.XY();

        /// <summary>
        /// Extracts the offset (ZW) from a Unity ST-format Vector4.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 Offset(this Vector4 st) => st.ZW();

        /// <summary>
        /// Applies the tiling and offset encoded in this ST Vector4 to a UV coordinate.
        /// Equivalent to the shader <c>TRANSFORM_TEX(uv, _MainTex)</c> macro on the CPU side.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 TransformUV(this Vector4 st, Vector2 uv)
            => new Vector2(uv.x * st.x + st.z, uv.y * st.y + st.w);

        #endregion

        #region RANDOMIZATION

        /// <summary>Random point inside the 4D axis-aligned box spanned by min and max.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 RandomInRange(Vector4 min, Vector4 max)
            => new Vector4(
                Random.Range(min.x, max.x),
                Random.Range(min.y, max.y),
                Random.Range(min.z, max.z),
                Random.Range(min.w, max.w));

        /// <summary>Offsets this vector by a uniform random amount on each axis.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 WithRandomOffset(this Vector4 v, float maxOffset)
            => new Vector4(
                v.x + Random.Range(-maxOffset, maxOffset),
                v.y + Random.Range(-maxOffset, maxOffset),
                v.z + Random.Range(-maxOffset, maxOffset),
                v.w + Random.Range(-maxOffset, maxOffset));

        #endregion

        #region AGGREGATES

        /// <summary>
        /// Arithmetic mean of a Vector4 set. Returns <see cref="Vector4.zero"/> for an empty or null input.
        /// </summary>
        public static Vector4 Centroid(IReadOnlyList<Vector4> items)
        {
            if (items == null || items.Count == 0) return Vector4.zero;

            Vector4 sum = Vector4.zero;
            for (int i = 0; i < items.Count; i++) sum += items[i];
            return sum / items.Count;
        }

        /// <summary>Index of the item nearest to <paramref name="origin"/> in 4D space, or −1 if empty.</summary>
        public static int NearestIndex(this Vector4 origin, IReadOnlyList<Vector4> items)
        {
            if (items == null || items.Count == 0) return -1;

            int best = 0;
            float bestSqr = origin.SqrDistanceTo(items[0]);

            for (int i = 1; i < items.Count; i++)
            {
                float sqr = origin.SqrDistanceTo(items[i]);
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = i;
                }
            }

            return best;
        }

        #endregion

        #region DEBUG

        /// <summary>
        /// Formats with full precision. Unity's default <c>ToString()</c> rounds to one decimal,
        /// hiding the small errors you are usually trying to diagnose in shader data.
        /// </summary>
        public static string ToDebugString(this Vector4 v, int decimals = 4)
        {
            string f = "F" + decimals;
            return $"({v.x.ToString(f)}, {v.y.ToString(f)}, {v.z.ToString(f)}, {v.w.ToString(f)})";
        }

        #endregion
    }
}
