using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Nexus.Core.Extensions
{
    /// <summary>
    /// Production extension methods for <see cref="Vector2"/>.
    /// <para>
    /// <b>Design rules (same as <see cref="Vector3Extensions"/>):</b>
    /// <list type="bullet">
    ///   <item>Zero allocation — every method returns a struct or primitive.</item>
    ///   <item>No hidden square roots — comparisons use squared magnitude.</item>
    ///   <item>NaN-safe — degenerate input returns zero or a fallback, never NaN.</item>
    ///   <item>Aggressive inlining on all leaf math helpers.</item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>2D-specific additions</b> not present in the 3D counterpart:
    /// rotation by scalar angle, perpendicular vectors, 2D cross product (winding),
    /// polar coordinate conversion, pixel-grid snapping, and axis mirroring.
    /// </para>
    /// </summary>
    public static class Vector2Extensions
    {
        /// <summary>Squared-magnitude threshold below which a vector is treated as degenerate.</summary>
        private const float SqrEpsilon = 1e-10f;

        /// <summary>Default tolerance for component-wise float comparison.</summary>
        private const float DefaultEpsilon = 1e-5f;

        #region Component Replacement

        /// <summary>Returns a copy with X component replaced.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 WithX(this Vector2 v, float x) => new(x, v.y);

        /// <summary>Returns a copy with Y component replaced.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 WithY(this Vector2 v, float y) => new(v.x, y);

        #endregion

        #region Component Offset

        /// <summary>Returns a copy with <paramref name="amount"/> added to X.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 AddX(this Vector2 v, float amount) => new(v.x + amount, v.y);

        /// <summary>Returns a copy with <paramref name="amount"/> added to Y.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 AddY(this Vector2 v, float amount) => new(v.x, v.y + amount);

        /// <summary>Returns a copy offset by the given per-axis amounts.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 Add(this Vector2 v, float x = 0f, float y = 0f)
            => new(v.x + x, v.y + y);

        #endregion

        #region Distance (√-free, use for range checks)

        /// <summary>Squared Euclidean distance. Prefer this over <see cref="DistanceTo"/> for comparisons.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SqrDistanceTo(this Vector2 from, Vector2 to)
        {
            float dx = to.x - from.x;
            float dy = to.y - from.y;
            return dx * dx + dy * dy;
        }

        /// <summary>Euclidean distance.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DistanceTo(this Vector2 from, Vector2 to)
            => Mathf.Sqrt(from.SqrDistanceTo(to));

        /// <summary>Manhattan (L1) distance. The natural metric for grid/tile pathfinding.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ManhattanDistanceTo(this Vector2 from, Vector2 to)
            => Mathf.Abs(to.x - from.x) + Mathf.Abs(to.y - from.y);

        /// <summary>Chebyshev (L∞) distance. The natural metric for 8-way grid movement (king moves in chess).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ChebyshevDistanceTo(this Vector2 from, Vector2 to)
            => Mathf.Max(Mathf.Abs(to.x - from.x), Mathf.Abs(to.y - from.y));

        #endregion

        #region Range Checks (sqrt-free)

        /// <summary>True if <paramref name="to"/> lies within <paramref name="range"/>. No square root.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsInRangeOf(this Vector2 from, Vector2 to, float range)
            => from.SqrDistanceTo(to) <= range * range;

        /// <summary>
        /// True if <paramref name="to"/> is within <paramref name="range"/> <b>and</b> inside a
        /// 2D view cone of <paramref name="halfAngleDegrees"/> around <paramref name="forward"/>.
        /// Standard 2D AI perception test; cheap distance rejection runs first.
        /// </summary>
        public static bool IsInViewCone(
            this Vector2 from,
            Vector2 to,
            Vector2 forward,
            float range,
            float halfAngleDegrees)
        {
            Vector2 delta = to - from;
            float sqrDist = delta.sqrMagnitude;

            if (sqrDist > range * range) return false;
            if (sqrDist < SqrEpsilon) return true;

            Vector2 dir = delta / Mathf.Sqrt(sqrDist);
            float cosHalf = Mathf.Cos(halfAngleDegrees * Mathf.Deg2Rad);
            return Vector2.Dot(forward.normalized, dir) >= cosHalf;
        }

        #endregion

        #region Direction & Normalization (NaN-safe)

        /// <summary>
        /// Normalized direction from <paramref name="from"/> to <paramref name="to"/>.
        /// Returns <see cref="Vector2.zero"/> for coincident points instead of NaN.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 DirectionTo(this Vector2 from, Vector2 to)
            => (to - from).SafeNormalized();

        /// <summary>
        /// Normalizes without producing NaN on a zero-length vector.
        /// Short-circuits before the square root.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 SafeNormalized(this Vector2 v)
        {
            float sqrMag = v.sqrMagnitude;
            if (sqrMag < SqrEpsilon) return Vector2.zero;
            return v / Mathf.Sqrt(sqrMag);
        }

        /// <summary>
        /// Normalizes, or returns <paramref name="fallback"/> if the vector is degenerate.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 NormalizedOr(this Vector2 v, Vector2 fallback)
        {
            float sqrMag = v.sqrMagnitude;
            if (sqrMag < SqrEpsilon) return fallback;
            return v / Mathf.Sqrt(sqrMag);
        }

        /// <summary>Returns the vector scaled to exactly <paramref name="length"/>. Zero-safe.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 WithMagnitude(this Vector2 v, float length)
            => v.SafeNormalized() * length;

        #endregion

        #region Angle & Rotation (2D-specific - no quaternions needed)

        /// <summary>
        /// Angle in degrees from the positive X axis, counter-clockwise.
        /// Range: (−180, 180]. Matches <see cref="Mathf.Atan2"/> convention.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ToAngleDeg(this Vector2 v)
            => Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;

        /// <summary>
        /// Angle in radians from the positive X axis, counter-clockwise.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ToAngleRad(this Vector2 v)
            => Mathf.Atan2(v.y, v.x);

        /// <summary>
        /// Converts an angle in degrees to a unit direction vector.
        /// 0° = right, 90° = up. The inverse of <see cref="ToAngleDeg"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 FromAngleDeg(float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        }

        /// <summary>
        /// Converts an angle in radians to a unit direction vector.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 FromAngleRad(float radians)
            => new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));

        /// <summary>
        /// Rotates this vector by <paramref name="degrees"/> counter-clockwise around the origin.
        /// The 2D equivalent of <c>Quaternion.Euler(0, 0, degrees) * v</c> but without the quaternion overhead.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 Rotate(this Vector2 v, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
        }

        /// <summary>
        /// Rotates this vector by <paramref name="radians"/> counter-clockwise.
        /// Avoids the deg→rad conversion when the caller already has radians.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 RotateRad(this Vector2 v, float radians)
        {
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
        }

        /// <summary>
        /// Rotates this point around an arbitrary <paramref name="pivot"/> by <paramref name="degrees"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 RotateAround(this Vector2 point, Vector2 pivot, float degrees)
            => pivot + (point - pivot).Rotate(degrees);

        /// <summary>
        /// Signed angle in degrees from this vector to <paramref name="to"/>.
        /// Positive = counter-clockwise. Range: (−180, 180].
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SignedAngleTo(this Vector2 from, Vector2 to)
            => Vector2.SignedAngle(from, to);

        /// <summary>Unsigned angle in degrees between two vectors (0–180).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float AngleTo(this Vector2 from, Vector2 to)
            => Vector2.Angle(from, to);

        /// <summary>
        /// Rotates this vector 90° counter-clockwise.
        /// The outward normal of a left-facing edge. Essential for wall normals and side-checks.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 PerpendicularCCW(this Vector2 v) => new Vector2(-v.y, v.x);

        /// <summary>
        /// Rotates this vector 90° clockwise.
        /// The outward normal of a right-facing edge.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 PerpendicularCW(this Vector2 v) => new Vector2(v.y, -v.x);

        /// <summary>
        /// 2D cross product (the Z component of the 3D cross product).
        /// <para>
        /// Positive → <paramref name="b"/> is counter-clockwise from <paramref name="a"/>.<br/>
        /// Negative → <paramref name="b"/> is clockwise from <paramref name="a"/>.<br/>
        /// Zero → the vectors are parallel.
        /// </para>
        /// Use for winding-order tests, left/right side checks, and polygon area computation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Cross(this Vector2 a, Vector2 b)
            => a.x * b.y - a.y * b.x;

        /// <summary>
        /// Returns which side of the directed line A→B the <paramref name="point"/> lies on.
        /// Positive = left, negative = right, zero = on the line.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SideOfLine(this Vector2 point, Vector2 lineA, Vector2 lineB)
            => (lineB - lineA).Cross(point - lineA);

        /// <summary>True if the point is to the left of the directed line A→B.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsLeftOfLine(this Vector2 point, Vector2 lineA, Vector2 lineB)
            => point.SideOfLine(lineA, lineB) > 0f;

        #endregion

        #region Reflection

        /// <summary>
        /// Reflects this vector off a surface with the given <paramref name="normal"/>.
        /// For bouncing projectiles, pong mechanics, and wall-sliding.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 Reflect(this Vector2 v, Vector2 normal)
            => v - 2f * Vector2.Dot(v, normal) * normal;

        /// <summary>
        /// Reflects this vector and scales the component along the normal by <paramref name="bounciness"/> (0–1).
        /// 1 = perfect elastic bounce, 0 = the vector slides along the surface.
        /// </summary>
        public static Vector2 Reflect(this Vector2 v, Vector2 normal, float bounciness)
        {
            float dot = Vector2.Dot(v, normal);
            Vector2 normalComponent = normal * dot;
            Vector2 tangentComponent = v - normalComponent;
            return tangentComponent - normalComponent * bounciness;
        }

        #endregion

        #region Mirroring / Flipping

        /// <summary>Negates the X component. Equivalent to a horizontal sprite flip.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 MirrorX(this Vector2 v) => new Vector2(-v.x, v.y);

        /// <summary>Negates the Y component. Equivalent to a vertical sprite flip.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 MirrorY(this Vector2 v) => new Vector2(v.x, -v.y);

        /// <summary>Negates both components. 180° rotation.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 MirrorBoth(this Vector2 v) => new Vector2(-v.x, -v.y);

        #endregion

        #region Component-Wise Arithmetic

        /// <summary>Component-wise multiply.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 Multiply(this Vector2 a, Vector2 b)
            => new Vector2(a.x * b.x, a.y * b.y);

        /// <summary>
        /// Component-wise divide. Any axis whose divisor is ~0 yields 0 rather than infinity.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 Divide(this Vector2 a, Vector2 b)
            => new Vector2(
                Mathf.Abs(b.x) < Mathf.Epsilon ? 0f : a.x / b.x,
                Mathf.Abs(b.y) < Mathf.Epsilon ? 0f : a.y / b.y);

        /// <summary>Per-component absolute value.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 Abs(this Vector2 v)
            => new Vector2(Mathf.Abs(v.x), Mathf.Abs(v.y));

        /// <summary>Per-component sign (−1, 0, or 1). Exact zero maps to zero.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 Sign(this Vector2 v)
            => new Vector2(
                v.x == 0f ? 0f : Mathf.Sign(v.x),
                v.y == 0f ? 0f : Mathf.Sign(v.y));

        /// <summary>Largest component value.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float MaxComponent(this Vector2 v) => Mathf.Max(v.x, v.y);

        /// <summary>Smallest component value.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float MinComponent(this Vector2 v) => Mathf.Min(v.x, v.y);

        /// <summary>Index of the largest component (0 = X, 1 = Y). Useful for dominant-axis snapping.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int MaxComponentIndex(this Vector2 v) => v.x >= v.y ? 0 : 1;

        /// <summary>Index of the smallest component (0 = X, 1 = Y). Useful for non-dominant-axis snapping.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int MinComponentIndex(this Vector2 v) => v.x < v.y ? 0 : 1;

        /// <summary>
        /// Collapses the vector to its single dominant axis.
        /// Used for four-way directional snapping (cardinal movement, tile adjacency).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 ToDominantAxis(this Vector2 v)
            => Mathf.Abs(v.x) >= Mathf.Abs(v.y) ? new Vector2(v.x, 0f) : new Vector2(0f, v.y);

        #endregion

        #region Clamping

        /// <summary>Clamps magnitude to at most <paramref name="maxLength"/>.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 ClampMagnitude(this Vector2 v, float maxLength)
            => Vector2.ClampMagnitude(v, maxLength);

        /// <summary>
        /// Clamps magnitude into [<paramref name="minLength"/>, <paramref name="maxLength"/>].
        /// A zero-length input stays zero — it has no direction to extend along.
        /// </summary>
        public static Vector2 ClampMagnitude(this Vector2 v, float minLength, float maxLength)
        {
            float sqrMag = v.sqrMagnitude;
            if (sqrMag < SqrEpsilon) return Vector2.zero;

            float mag = Mathf.Sqrt(sqrMag);
            float clamped = Mathf.Clamp(mag, minLength, maxLength);
            return v * (clamped / mag);
        }

        /// <summary>Clamps every component into the same scalar range.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 ClampComponents(this Vector2 v, float min, float max)
            => new Vector2(Mathf.Clamp(v.x, min, max), Mathf.Clamp(v.y, min, max));

        /// <summary>Clamps every component into the corresponding per-axis range.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 ClampComponents(this Vector2 v, Vector2 min, Vector2 max)
            => new Vector2(Mathf.Clamp(v.x, min.x, max.x), Mathf.Clamp(v.y, min.y, max.y));

        /// <summary>Clamps the point inside a <see cref="Rect"/>.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 ClampToRect(this Vector2 v, Rect rect)
            => new Vector2(Mathf.Clamp(v.x, rect.xMin, rect.xMax), Mathf.Clamp(v.y, rect.yMin, rect.yMax));

        #endregion

        #region Snapping

        /// <summary>Snaps each component to the nearest multiple of <paramref name="gridSize"/>.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 SnapToGrid(this Vector2 v, float gridSize)
        {
            if (gridSize <= 0f) return v;
            return new Vector2(
                Mathf.Round(v.x / gridSize) * gridSize,
                Mathf.Round(v.y / gridSize) * gridSize);
        }

        /// <summary>Snaps each component to its own per-axis grid. Pass 0 on an axis to leave it untouched.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 SnapToGrid(this Vector2 v, Vector2 gridSize)
            => new Vector2(
                gridSize.x > 0f ? Mathf.Round(v.x / gridSize.x) * gridSize.x : v.x,
                gridSize.y > 0f ? Mathf.Round(v.y / gridSize.y) * gridSize.y : v.y);

        /// <summary>
        /// Snaps to a grid offset by <paramref name="origin"/>.
        /// Pass a half-cell origin to snap to cell centres rather than corners.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 SnapToGrid(this Vector2 v, float gridSize, Vector2 origin)
            => (v - origin).SnapToGrid(gridSize) + origin;

        /// <summary>
        /// Snaps the vector's direction to the nearest of <paramref name="steps"/> evenly spaced angles.
        /// With <c>steps: 4</c> this produces four-way cardinal movement; <c>8</c> gives eight-way.
        /// </summary>
        public static Vector2 SnapDirection(this Vector2 v, int steps)
        {
            if (steps <= 0) return v;
            if (v.sqrMagnitude < SqrEpsilon) return v;

            float stepDeg = 360f / steps;
            float snappedAngle = Mathf.Round(v.ToAngleDeg() / stepDeg) * stepDeg;
            return FromAngleDeg(snappedAngle) * v.magnitude;
        }

        /// <summary>
        /// Snaps to the nearest pixel boundary for a given <paramref name="pixelsPerUnit"/>.
        /// Eliminates sub-pixel jitter that causes sprite shimmer on low-resolution cameras.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 SnapToPixel(this Vector2 v, float pixelsPerUnit)
        {
            if (pixelsPerUnit <= 0f) return v;
            return new Vector2(
                Mathf.Round(v.x * pixelsPerUnit) / pixelsPerUnit,
                Mathf.Round(v.y * pixelsPerUnit) / pixelsPerUnit);
        }

        #endregion

        #region Rounding

        /// <summary>Rounds each component to <paramref name="decimals"/> places. For save files and network quantization.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 RoundTo(this Vector2 v, int decimals)
        {
            float factor = Mathf.Pow(10f, decimals);
            return new Vector2(
                Mathf.Round(v.x * factor) / factor,
                Mathf.Round(v.y * factor) / factor);
        }

        #endregion

        #region Validation & Comparison

        /// <summary>True if every component is a finite real number.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsValid(this Vector2 v)
            => !float.IsNaN(v.x) && !float.IsNaN(v.y)
            && !float.IsInfinity(v.x) && !float.IsInfinity(v.y);

        /// <summary>Returns the vector if valid, otherwise <paramref name="fallback"/>.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 ValidOr(this Vector2 v, Vector2 fallback)
            => v.IsValid() ? v : fallback;

        /// <summary>Component-wise approximate equality with explicit tolerance.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Approximately(this Vector2 a, Vector2 b, float epsilon = DefaultEpsilon)
            => Mathf.Abs(a.x - b.x) < epsilon
            && Mathf.Abs(a.y - b.y) < epsilon;

        /// <summary>True if the vector is effectively zero-length.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNearZero(this Vector2 v, float sqrEpsilon = SqrEpsilon)
            => v.sqrMagnitude < sqrEpsilon;

        /// <summary>True if the vector is already unit length within tolerance.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNormalized(this Vector2 v, float epsilon = 1e-4f)
            => Mathf.Abs(v.sqrMagnitude - 1f) < epsilon;

        #endregion

        #region Interpolation

        /// <summary>
        /// Inverse of <see cref="Vector2.Lerp"/>: how far <paramref name="value"/> sits along the
        /// segment A→B, projected onto that segment. Unclamped.
        /// </summary>
        public static float InverseLerp(this Vector2 value, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float sqrLen = ab.sqrMagnitude;
            if (sqrLen < SqrEpsilon) return 0f;
            return Vector2.Dot(value - a, ab) / sqrLen;
        }

        /// <summary>Same as <see cref="InverseLerp"/> but clamped to [0, 1].</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float InverseLerpClamped(this Vector2 value, Vector2 a, Vector2 b)
            => Mathf.Clamp01(value.InverseLerp(a, b));

        /// <summary>
        /// Framerate-independent exponential smoothing toward <paramref name="target"/>.
        /// Unlike <c>Lerp(current, target, speed * dt)</c>, this converges at the same visual rate
        /// regardless of framerate.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 ExpDecayTo(this Vector2 current, Vector2 target, float sharpness, float deltaTime)
            => Vector2.LerpUnclamped(target, current, Mathf.Exp(-sharpness * deltaTime));

        /// <summary>Quadratic Bézier evaluation. For simple arcs and UI fly-to animations.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 BezierTo(this Vector2 start, Vector2 control, Vector2 end, float t)
        {
            float u = 1f - t;
            return u * u * start + 2f * u * t * control + t * t * end;
        }

        /// <summary>Cubic Bézier evaluation.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 BezierTo(this Vector2 start, Vector2 c1, Vector2 c2, Vector2 end, float t)
        {
            float u = 1f - t;
            float uu = u * u;
            float tt = t * t;
            return uu * u * start + 3f * uu * t * c1 + 3f * u * tt * c2 + tt * t * end;
        }

        /// <summary>
        /// Remaps <paramref name="value"/> from range [<paramref name="inMin"/>, <paramref name="inMax"/>]
        /// to [<paramref name="outMin"/>, <paramref name="outMax"/>].
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 Remap(this Vector2 value, Vector2 inMin, Vector2 inMax, Vector2 outMin, Vector2 outMax)
        {
            Vector2 t = new(
                Mathf.InverseLerp(inMin.x, inMax.x, value.x),
                Mathf.InverseLerp(inMin.y, inMax.y, value.y));

            return new Vector2(
                Mathf.LerpUnclamped(outMin.x, outMax.x, t.x),
                Mathf.LerpUnclamped(outMin.y, outMax.y, t.y));
        }

        #endregion

        #region Geometry Queries

        /// <summary>Closest point to this position on the infinite line through A and B.</summary>
        public static Vector2 ClosestPointOnLine(this Vector2 point, Vector2 lineA, Vector2 lineB)
        {
            Vector2 ab = lineB - lineA;
            float sqrLen = ab.sqrMagnitude;
            if (sqrLen < SqrEpsilon) return lineA;
            return lineA + ab * (Vector2.Dot(point - lineA, ab) / sqrLen);
        }

        /// <summary>Closest point to this position on the finite segment A→B.</summary>
        public static Vector2 ClosestPointOnSegment(this Vector2 point, Vector2 segmentA, Vector2 segmentB)
        {
            Vector2 ab = segmentB - segmentA;
            float sqrLen = ab.sqrMagnitude;
            if (sqrLen < SqrEpsilon) return segmentA;
            float t = Mathf.Clamp01(Vector2.Dot(point - segmentA, ab) / sqrLen);
            return segmentA + ab * t;
        }

        /// <summary>Shortest distance from this position to the segment A→B.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DistanceToSegment(this Vector2 point, Vector2 segmentA, Vector2 segmentB)
            => point.DistanceTo(point.ClosestPointOnSegment(segmentA, segmentB));

        /// <summary>
        /// True if <paramref name="target"/> lies in front of this position relative to <paramref name="forward"/>.
        /// A cheap dot-product gate before any expensive raycast.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsInFrontOf(this Vector2 from, Vector2 target, Vector2 forward)
            => Vector2.Dot(forward, target - from) > 0f;

        /// <summary>
        /// True if the point lies inside the triangle defined by vertices A, B, C.
        /// Uses the cross-product winding test — no division, no square root.
        /// </summary>
        public static bool IsInsideTriangle(this Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = p.SideOfLine(a, b);
            float d2 = p.SideOfLine(b, c);
            float d3 = p.SideOfLine(c, a);

            bool hasNeg = (d1 < 0f) || (d2 < 0f) || (d3 < 0f);
            bool hasPos = (d1 > 0f) || (d2 > 0f) || (d3 > 0f);

            return !(hasNeg && hasPos);
        }

        /// <summary>
        /// True if the point lies inside the convex polygon defined by <paramref name="vertices"/>
        /// (ordered either CW or CCW). Uses the cross-product sign consistency test.
        /// </summary>
        public static bool IsInsideConvexPolygon(this Vector2 p, IReadOnlyList<Vector2> vertices)
        {
            if (vertices == null || vertices.Count < 3) return false;

            int count = vertices.Count;
            bool? positive = null;

            for (int i = 0; i < count; i++)
            {
                Vector2 a = vertices[i];
                Vector2 b = vertices[(i + 1) % count];
                float cross = (b - a).Cross(p - a);

                if (Mathf.Abs(cross) < SqrEpsilon) continue; // On the edge — skip.

                bool isPositive = cross > 0f;
                if (positive == null)
                    positive = isPositive;
                else if (positive.Value != isPositive)
                    return false;
            }

            return true;
        }

        #endregion

        #region Polar Coordinates (2D-specific)

        /// <summary>
        /// Converts this Cartesian vector to polar coordinates.
        /// Returns (radius, angle in degrees).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (float radius, float angleDeg) ToPolar(this Vector2 v)
            => (v.magnitude, v.ToAngleDeg());

        /// <summary>
        /// Converts polar coordinates to a Cartesian <see cref="Vector2"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 FromPolar(float radius, float angleDeg)
            => FromAngleDeg(angleDeg) * radius;

        /// <summary>
        /// Returns a copy of this vector with its angle replaced, preserving magnitude.
        /// Useful for rotating a velocity vector without changing speed.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 WithAngle(this Vector2 v, float degrees)
            => FromAngleDeg(degrees) * v.magnitude;

        /// <summary>
        /// Returns a copy of this vector with its magnitude replaced, preserving direction.
        /// Alias for <see cref="WithMagnitude"/> with polar naming for symmetry.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 WithRadius(this Vector2 v, float radius)
            => v.WithMagnitude(radius);

        #endregion

        #region Randomization

        /// <summary>Random point inside the axis-aligned rectangle spanned by min and max.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 RandomInRange(Vector2 min, Vector2 max)
            => new Vector2(Random.Range(min.x, max.x), Random.Range(min.y, max.y));

        /// <summary>Random point inside the unit circle (uniform area distribution).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 RandomInCircle() => Random.insideUnitCircle;

        /// <summary>Random point on the unit circle boundary (uniform angular distribution).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 RandomOnCircle() => FromAngleDeg(Random.Range(0f, 360f));

        /// <summary>Random point inside a circle of <paramref name="radius"/> centred on this point.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 WithRandomOffset(this Vector2 v, float radius)
            => v + Random.insideUnitCircle * radius;

        /// <summary>
        /// Perturbs this direction by up to <paramref name="maxAngleDegrees"/> in either direction.
        /// The correct way to model 2D weapon spread: angular, not positional.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 RandomizeDirection(this Vector2 direction, float maxAngleDegrees)
        {
            if (direction.sqrMagnitude < SqrEpsilon || maxAngleDegrees <= 0f) return direction;
            return direction.Rotate(Random.Range(-maxAngleDegrees, maxAngleDegrees));
        }

        #endregion

        #region Aggregates

        /// <summary>
        /// Arithmetic mean of a point set. Returns <see cref="Vector2.zero"/> for an empty or null input.
        /// Enumerates by index, so no iterator is allocated.
        /// </summary>
        public static Vector2 Centroid(IReadOnlyList<Vector2> points)
        {
            if (points == null || points.Count == 0) return Vector2.zero;

            Vector2 sum = Vector2.zero;
            for (int i = 0; i < points.Count; i++) sum += points[i];
            return sum / points.Count;
        }

        /// <summary>Index of the point nearest to <paramref name="origin"/>, or −1 if the list is empty.</summary>
        public static int NearestIndex(this Vector2 origin, IReadOnlyList<Vector2> points)
        {
            if (points == null || points.Count == 0) return -1;

            int best = 0;
            float bestSqr = origin.SqrDistanceTo(points[0]);

            for (int i = 1; i < points.Count; i++)
            {
                float sqr = origin.SqrDistanceTo(points[i]);
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = i;
                }
            }

            return best;
        }

        #endregion

        #region Debug

        /// <summary>
        /// Formats with full precision. Unity's default <c>ToString()</c> rounds to one decimal,
        /// hiding the sub-pixel drift you are usually trying to diagnose.
        /// </summary>
        public static string ToDebugString(this Vector2 v, int decimals = 4)
        {
            string format = "F" + decimals;
            return $"({v.x.ToString(format)}, {v.y.ToString(format)})";
        }

        #endregion
    }
}
