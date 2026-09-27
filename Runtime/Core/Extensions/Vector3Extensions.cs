using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Nexus.Core.Extensions
{
    /// <summary>
    /// Production extension methods for <see cref="Vector3"/>.
    /// <para>
    /// <b>Design rules enforced throughout:</b>
    /// <list type="bullet">
    ///   <item><b>Zero allocation.</b> Every method returns a struct or a primitive; nothing boxes.</item>
    ///   <item><b>No hidden square roots.</b> Anything that only needs a comparison uses squared magnitude.
    ///         Methods that genuinely compute a length say so in the name (<c>Distance</c>, not <c>SqrDistance</c>).</item>
    ///   <item><b>NaN-safe by default.</b> Normalization and division guard against degenerate input
    ///         rather than silently poisoning a transform. A single NaN written to
    ///         <see cref="Transform.position"/> corrupts the native physics state.</item>
    ///   <item><b>XZ-plane first-class.</b> Most 3D games treat Y as height; horizontal variants are provided
    ///         explicitly instead of forcing callers to hand-roll <c>FlattenY</c> chains.</item>
    ///   <item><b>Aggressive inlining.</b> These are leaf math helpers; the JIT/Burst should erase the call.</item>
    /// </list>
    /// </para>
    /// </summary>
    public static class Vector3Extensions
    {
        /// <summary>Squared-magnitude threshold below which a vector is treated as degenerate.</summary>
        private const float SqrEpsilon = 1e-10f;

        /// <summary>Default tolerance for component-wise float comparison.</summary>
        private const float DefaultEpsilon = 1e-5f;

        #region Component Replacement

        /// <summary>Returns a copy with the X component replaced.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 WithX(this Vector3 v, float x) => new(x, v.y, v.z);

        /// <summary>Returns a copy with the Y component replaced.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 WithY(this Vector3 v, float y) => new(v.x, y, v.z);

        /// <summary>Returns a copy with the Z component replaced.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 WithZ(this Vector3 v, float z) => new(v.x, v.y, z);

        /// <summary>Returns a copy with the X and Y components replaced.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 WithXY(this Vector3 v, float x, float y) => new(x, y, v.z);

        /// <summary>Returns a copy with the X and Z components replaced.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 WithXZ(this Vector3 v, float x, float z) => new(x, v.y, z);

        /// <summary>Returns a copy with the Y and Z components replaced.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 WithYZ(this Vector3 v, float y, float z) => new(v.x, y, z);

        #endregion

        #region Component Offset

        /// <summary>Returns a copy with <paramref name="amount"/> added to X.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 AddX(this Vector3 v, float amount)
            => new(v.x + amount, v.y, v.z);

        /// <summary>Returns a copy with <paramref name="amount"/> added to Y.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 AddY(this Vector3 v, float amount)
            => new(v.x, v.y + amount, v.z);

        /// <summary>Returns a copy with <paramref name="amount"/> added to Z.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 AddZ(this Vector3 v, float amount)
            => new(v.x, v.y, v.z + amount);

        /// <summary>Returns a copy offset by the given per-axis amounts.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 Add(this Vector3 v, float x = 0f, float y = 0f, float z = 0f)
            => new(v.x + x, v.y + y, v.z + z);

        #endregion

        #region Projection

        /// <summary>Zeroes the Y component. Equivalent to projecting onto the XZ ground plane.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 FlattenY(this Vector3 v) => new(v.x, 0f, v.z);

        /// <summary>Zeroes the X component. Equivalent to projecting onto the YZ ground plane.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 FlattenX(this Vector3 v) => new(0f, v.y, v.z);

        /// <summary>Zeroes the Z component. Equivalent to projecting onto the XY ground plane.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 FlattenZ(this Vector3 v) => new(v.x, v.y, 0f);

        /// <summary>
        /// Projects the vector onto the plane defined by <paramref name="planeNormal"/>.
        /// Use with a ground raycast normal to make movement follow slopes without losing speed.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 ProjectOntoPlane(this Vector3 v, Vector3 planeNormal)
            => Vector3.ProjectOnPlane(v, planeNormal);

        /// <summary>Reflects the vector off a surface with the given normal (bouncing projectiles, ricochets).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 Reflect(this Vector3 v, Vector3 surfaceNormal)
            => Vector3.Reflect(v, surfaceNormal);

        #endregion

        #region Distance (√-free, use for range checks)

        /// <summary>Squared Euclidean distance. Use for comparisons; avoids <see cref="Mathf.Sqrt"/>.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SqrDistanceTo(this Vector3 from, Vector3 to)
            => (to - from).sqrMagnitude;

        /// <summary>Euclidean distance between two points.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DistanceTo(this Vector3 from, Vector3 to)
            => Vector3.Distance(from, to);

        /// <summary>
        /// Squared distance on the XZ ground plane only (ignores height).
        /// Ideal for aggro-range / proximity checks in 3D games where altitude is irrelevant.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SqrHorizontalDistanceTo(this Vector3 from, Vector3 to)
        {
            float dx = to.x - from.x;
            float dz = to.z - from.z;
            return dx * dx + dz * dz;
        }

        /// <summary>Euclidean distance on the XZ ground plane (ignores height).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float HorizontalDistanceTo(this Vector3 from, Vector3 to)
            => Mathf.Sqrt(from.SqrHorizontalDistanceTo(to));

        /// <summary>Absolute height difference between two points.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float VerticalDistanceTo(this Vector3 from, Vector3 to)
            => Mathf.Abs(to.y - from.y);

        #endregion

        #region Range / Proximity (squared, no Sqrt)

        /// <summary>
        /// Returns <c>true</c> if <paramref name="from"/> is within <paramref name="range"/> of
        /// <paramref name="to"/>. Uses squared comparison internally — no <see cref="Mathf.Sqrt"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsInRangeOf(this Vector3 from, Vector3 to, float range)
            => from.SqrDistanceTo(to) <= range * range;

        /// <summary>
        /// Range check on the XZ ground plane only. Ignores altitude difference.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsInHorizontalRangeOf(this Vector3 from, Vector3 to, float range)
            => from.SqrHorizontalDistanceTo(to) <= range * range;

        /// <summary>
        /// True if <paramref name="to"/> is inside a cylinder centred on <paramref name="from"/>.
        /// Models melee reach, interaction volumes, and AoE far better than a sphere in a game with floors.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsInCylinderOf(this Vector3 from, Vector3 to, float radius, float halfHeight)
            => from.SqrHorizontalDistanceTo(to) <= radius * radius
            && Mathf.Abs(to.y - from.y) <= halfHeight;

        /// <summary>
        /// True if <paramref name="to"/> is within <paramref name="range"/> <b>and</b> inside a
        /// view cone of <paramref name="halfAngleDegrees"/> around <paramref name="forward"/>.
        /// Standard AI perception test; performs the cheap distance rejection first.
        /// </summary>
        public static bool IsInViewCone(
            this Vector3 from,
            Vector3 to,
            Vector3 forward,
            float range,
            float halfAngleDegrees)
        {
            Vector3 delta = to - from;
            float sqrDist = delta.sqrMagnitude;

            if (sqrDist > range * range) return false;
            if (sqrDist < SqrEpsilon) return true; // Coincident: treat as visible.

            Vector3 dir = delta / Mathf.Sqrt(sqrDist);
            float cosHalf = Mathf.Cos(halfAngleDegrees * Mathf.Deg2Rad);
            return Vector3.Dot(forward.normalized, dir) >= cosHalf;
        }

        #endregion

        #region Direction (NaN-safe)

        /// <summary>
        /// Normalizes without producing NaN on a zero-length vector.
        /// <para>
        /// Unity's built-in <see cref="Vector3.normalized"/> already returns zero below its internal
        /// epsilon, but that epsilon is applied to the magnitude, not the squared magnitude, and it
        /// costs a square root regardless. This variant short-circuits before the sqrt.
        /// </para>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 SafeNormalized(this Vector3 v)
        {
            float sqrMag = v.sqrMagnitude;
            if (sqrMag < SqrEpsilon) return Vector3.zero;
            return v / Mathf.Sqrt(sqrMag);
        }

        /// <summary>
        /// Normalized direction vector from <paramref name="from"/> to <paramref name="to"/>.
        /// Returns <see cref="Vector3.zero"/> if the points are coincident (avoids NaN).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 DirectionTo(this Vector3 from, Vector3 to)
            => (to - from).SafeNormalized();

        /// <summary>
        /// Normalized direction from <paramref name="from"/> to <paramref name="to"/>, flattened to the XZ plane.
        /// The canonical steering vector for a character that should not tilt toward a target above or below it.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 HorizontalDirectionTo(this Vector3 from, Vector3 to)
            => (to - from).FlattenY().SafeNormalized();

        /// <summary>
        /// Normalizes, or returns <paramref name="fallback"/> if the vector is degenerate.
        /// Useful when a zero direction must still produce a valid facing (e.g. default to <c>Vector3.forward</c>).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 NormalizedOr(this Vector3 v, Vector3 fallback)
        {
            float sqrMag = v.sqrMagnitude;
            if (sqrMag < SqrEpsilon) return fallback;
            return v / Mathf.Sqrt(sqrMag);
        }

        /// <summary>Returns the vector scaled to exactly <paramref name="length"/>. Zero-safe.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 WithMagnitude(this Vector3 v, float length)
            => v.SafeNormalized() * length;

        #endregion

        #region Angle

        /// <summary>Unsigned angle (0–180°) in degrees between two vectors.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float AngleTo(this Vector3 from, Vector3 to)
            => Vector3.Angle(from, to);

        /// <summary>
        /// Unsigned angle (0–180°) in degrees between <paramref name="from"/>→<paramref name="to"/>
        /// and world <see cref="Vector3.forward"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float AngleToForward(this Vector3 from, Vector3 to)
            => Vector3.Angle(Vector3.forward, to - from);

        /// <summary>
        /// Signed angle (-180° to +180°) in degrees around the Y axis.
        /// Positive means <paramref name="to"/> is clockwise from <paramref name="from"/> when viewed from above.
        /// Feed this straight into turn-rate logic and blend-tree parameters.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SignedAngleToY(this Vector3 from, Vector3 to)
            => Vector3.SignedAngle(from.FlattenY(), to.FlattenY(), Vector3.up);

        /// <summary>
        /// Signed angle (-180° to +180°) in degrees around world Y
        /// from <see cref="Vector3.forward"/> to the XZ direction.<br/>
        /// Positive = clockwise when viewed from above. Useful for AI steering.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SignedAngleToXZ(this Vector3 from, Vector3 to)
            => Vector3.SignedAngle(Vector3.forward, (to - from).FlattenY(), Vector3.up);

        /// <summary>
        /// Compass heading of this direction in degrees, where 0 = +Z (forward) and 90 = +X (right).
        /// Matches the convention of <see cref="Transform.eulerAngles"/>.y.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ToYaw(this Vector3 direction)
            => Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;

        /// <summary>
        /// Elevation of this direction in degrees, where positive means downward
        /// (matching Unity's inverted X-rotation convention).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ToPitch(this Vector3 direction)
        {
            float horizontal = new Vector2(direction.x, direction.z).magnitude;
            return -Mathf.Atan2(direction.y, horizontal) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// Builds the rotation that looks along this vector, falling back to
        /// <see cref="Quaternion.identity"/> for a degenerate direction.
        /// Avoids the "Look rotation viewing vector is zero" console spam.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Quaternion ToLookRotation(this Vector3 direction, Vector3 up = default)
        {
            if (direction.sqrMagnitude < SqrEpsilon) return Quaternion.identity;
            return Quaternion.LookRotation(direction, up == default ? Vector3.up : up);
        }

        /// <summary>
        /// Builds a Y-only look rotation from this direction, so an upright character
        /// never pitches or rolls toward its target.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Quaternion ToUprightLookRotation(this Vector3 direction)
            => direction.FlattenY().ToLookRotation();

        #endregion

        #region Rotation

        /// <summary>Rotates this point around <paramref name="pivot"/> by <paramref name="rotation"/>.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 RotateAround(this Vector3 point, Vector3 pivot, Quaternion rotation)
            => pivot + rotation * (point - pivot);

        /// <summary>Rotates this point around <paramref name="pivot"/> on the given axis.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 RotateAround(this Vector3 point, Vector3 pivot, Vector3 axis, float degrees)
            => point.RotateAround(pivot, Quaternion.AngleAxis(degrees, axis));

        /// <summary>
        /// Rotates this vector about the world Y axis. Cheaper than a quaternion multiply;
        /// ideal for spread patterns, orbit offsets, and shotgun cones.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 RotateAroundY(this Vector3 v, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            float sin = Mathf.Sin(rad);
            float cos = Mathf.Cos(rad);
            return new Vector3(v.x * cos + v.z * sin, v.y, -v.x * sin + v.z * cos);
        }

        /// <summary>
        /// Reinterprets this vector as a local-space input (x = strafe, z = forward)
        /// relative to <paramref name="reference"/>, typically the main camera.
        /// <para>
        /// With <paramref name="flatten"/> left true, a downward-pitched camera still produces
        /// horizontal movement — the fix for "character walks into the floor when the camera looks down".
        /// </para>
        /// </summary>
        public static Vector3 RelativeTo(this Vector3 v, Transform reference, bool flatten = true)
        {
            if (reference == null) return v;

            Vector3 forward = reference.forward;
            Vector3 right = reference.right;

            if (flatten)
            {
                forward = forward.FlattenY().NormalizedOr(Vector3.forward);
                right = right.FlattenY().NormalizedOr(Vector3.right);
            }

            return right * v.x + Vector3.up * v.y + forward * v.z;
        }

        #endregion

        #region Component-Wise Arithemetic

        /// <summary>Component-wise multiply. Non-mutating alternative to <see cref="Vector3.Scale(Vector3)"/>.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 Multiply(this Vector3 a, Vector3 b)
            => new(a.x * b.x, a.y * b.y, a.z * b.z);

        /// <summary>
        /// Component-wise divide. Any axis whose divisor is ~0 yields 0 rather than infinity,
        /// so a degenerate <see cref="Bounds.size"/> cannot corrupt downstream math.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 Divide(this Vector3 a, Vector3 b)
            => new(
                Mathf.Abs(b.x) < Mathf.Epsilon ? 0f : a.x / b.x,
                Mathf.Abs(b.y) < Mathf.Epsilon ? 0f : a.y / b.y,
                Mathf.Abs(b.z) < Mathf.Epsilon ? 0f : a.z / b.z);

        /// <summary>Returns a vector with the absolute value of each component.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 Abs(this Vector3 v) =>
            new(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        /// <summary>Returns a vector with the sign (-1, 0, or 1) of each component.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 Sign(this Vector3 v) =>
            new(Mathf.Sign(v.x), Mathf.Sign(v.y), Mathf.Sign(v.z));

        /// <summary>Largest component value.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float MaxComponent(this Vector3 v) => Mathf.Max(v.x, Mathf.Max(v.y, v.z));

        /// <summary>Smallest component value.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float MinComponent(this Vector3 v) => Mathf.Min(v.x, Mathf.Min(v.y, v.z));

        /// <summary>Index of the largest component (0 = X, 1 = Y, 2 = Z). Useful for dominant-axis snapping.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int MaxComponentIndex(this Vector3 v)
        {
            if (v.x >= v.y) return v.x >= v.z ? 0 : 2;
            return v.y >= v.z ? 1 : 2;
        }

        /// <summary>Index of the smallest component (0 = X, 1 = Y, 2 = Z). Useful for non-dominant-axis snapping.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int MinComponentIndex(this Vector3 v)
        {
            if (v.x < v.y) return v.x < v.z ? 0 : 2;
            return v.y < v.z ? 1 : 2;
        }

        /// <summary>
        /// Collapses the vector to its single dominant axis, preserving that axis's value.
        /// Used for six-way directional snapping (wall alignment, cardinal facing, cube-face selection).
        /// </summary>
        public static Vector3 ToDominantAxis(this Vector3 v)
        {
            Vector3 abs = v.Abs();
            if (abs.x >= abs.y && abs.x >= abs.z) return new Vector3(v.x, 0f, 0f);
            if (abs.y >= abs.z) return new Vector3(0f, v.y, 0f);
            return new Vector3(0f, 0f, v.z);
        }

        #endregion

        #region Clamping / Constraints

        /// <summary>Clamps the magnitude of a vector to <paramref name="maxLength"/>.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 ClampMagnitude(this Vector3 v, float maxLength)
            => Vector3.ClampMagnitude(v, maxLength);

        /// <summary>
        /// Clamps magnitude into [<paramref name="minLength"/>, <paramref name="maxLength"/>].
        /// A zero-length input stays zero — it has no direction to extend along.
        /// </summary>
        public static Vector3 ClampMagnitude(this Vector3 v, float minLength, float maxLength)
        {
            float sqrMag = v.sqrMagnitude;
            if (sqrMag < SqrEpsilon) return Vector3.zero;

            float mag = Mathf.Sqrt(sqrMag);
            float clamped = Mathf.Clamp(mag, minLength, maxLength);
            return v * (clamped / mag);
        }

        /// <summary>Clamps each component independently to [<paramref name="min"/>, <paramref name="max"/>].</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 ClampComponents(this Vector3 v, float min, float max) =>
            new(Mathf.Clamp(v.x, min, max), Mathf.Clamp(v.y, min, max), Mathf.Clamp(v.z, min, max));

        /// <summary>Clamps every component into the corresponding per-axis range.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 ClampComponents(this Vector3 v, Vector3 min, Vector3 max)
            => new(Mathf.Clamp(v.x, min.x, max.x), Mathf.Clamp(v.y, min.y, max.y), Mathf.Clamp(v.z, min.z, max.z));

        /// <summary>Clamps the point inside <paramref name="bounds"/>.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 ClampToBounds(this Vector3 v, Bounds bounds)
            => v.ClampComponents(bounds.min, bounds.max);

        #endregion

        #region Snapping (Grid / Tile)

        /// <summary>
        /// Snaps each component to the nearest multiple of <paramref name="gridSize"/>.
        /// Essential for tile-based building, placement systems, and voxel grids.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 SnapToGrid(this Vector3 v, float gridSize)
        {
            float inv = 1f / gridSize;
            return new Vector3(
                Mathf.Round(v.x * inv) * gridSize,
                Mathf.Round(v.y * inv) * gridSize,
                Mathf.Round(v.z * inv) * gridSize);
        }

        /// <summary>Snaps each component to its own per-axis grid. Pass 0 on an axis to leave it untouched.</summary>
        public static Vector3 SnapToGrid(this Vector3 v, Vector3 gridSize)
            => new(
                gridSize.x > 0f ? Mathf.Round(v.x / gridSize.x) * gridSize.x : v.x,
                gridSize.y > 0f ? Mathf.Round(v.y / gridSize.y) * gridSize.y : v.y,
                gridSize.z > 0f ? Mathf.Round(v.z / gridSize.z) * gridSize.z : v.z);

        /// <summary>
        /// Snaps each component to the nearest multiple of <paramref name="gridSize"/>,
        /// with an optional <paramref name="offset"/> (e.g., half-cell centering).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 SnapToGrid(this Vector3 v, float gridSize, Vector3 offset)
            => (v - offset).SnapToGrid(gridSize) + offset;

        /// <summary>
        /// Snaps the vector's XZ direction to the nearest of <paramref name="steps"/> evenly spaced headings.
        /// With <c>steps: 8</c> this produces classic eight-way movement.
        /// </summary>
        public static Vector3 SnapDirectionY(this Vector3 v, int steps)
        {
            if (steps <= 0) return v;

            Vector3 flat = v.FlattenY();
            if (flat.sqrMagnitude < SqrEpsilon) return v;

            float stepDeg = 360f / steps;
            float snappedYaw = Mathf.Round(flat.ToYaw() / stepDeg) * stepDeg;
            float rad = snappedYaw * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)) * flat.magnitude;
        }

        #endregion

        #region Safety / Comparison

        /// <summary>
        /// True if every component is a finite real number.
        /// <para>
        /// Assigning a NaN position or velocity corrupts Unity's native transform hierarchy and physics
        /// solver, producing errors far from the actual cause. Validate before writing to a
        /// <see cref="Transform"/> or <see cref="Rigidbody"/> whenever the value came from division,
        /// normalization of unknown input, or deserialized data.
        /// </para>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsValid(this Vector3 v)
            => !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z)
            && !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);

        /// <summary>Returns the vector if valid, otherwise <paramref name="fallback"/>.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 ValidOr(this Vector3 v, Vector3 fallback)
            => v.IsValid() ? v : fallback;

        /// <summary>
        /// Returns <c>true</c> if all components are approximately equal within
        /// <paramref name="epsilon"/> (default ≈ 1e-5).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Approximately(this Vector3 a, Vector3 b, float epsilon = DefaultEpsilon)
            => Mathf.Abs(a.x - b.x) < epsilon
            && Mathf.Abs(a.y - b.y) < epsilon
            && Mathf.Abs(a.z - b.z) < epsilon;

        /// <summary>Returns <c>true</c> if the vector's squared magnitude is below <paramref name="epsilon"/>.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNearZero(this Vector3 v, float epsilon = SqrEpsilon)
            => v.sqrMagnitude < epsilon;

        /// <summary>True if the vector is already unit length within tolerance.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNormalized(this Vector3 v, float epsilon = 1e-4f)
            => Mathf.Abs(v.sqrMagnitude - 1f) < epsilon;

        #endregion

        #region Interpolation / Inverse

        /// <summary>
        /// Returns how far <paramref name="value"/> is between <paramref name="a"/> and <paramref name="b"/>
        /// as a 0-1 ratio. Unclamped. The inverse of <see cref="Vector3.Lerp"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float InverseLerp(this Vector3 value, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float denom = Vector3.Dot(ab, ab);
            return (denom < SqrEpsilon) ? 0f : Vector3.Dot(value - a, ab) / denom;
        }

        /// <summary>Same as <see cref="InverseLerp"/> but clamped to [0, 1].</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float InverseLerpClamped(this Vector3 value, Vector3 a, Vector3 b)
            => Mathf.Clamp01(value.InverseLerp(a, b));

        /// <summary>
        /// Framerate-independent exponential smoothing toward <paramref name="target"/>.
        /// <para>
        /// Unlike <c>Lerp(current, target, speed * Time.deltaTime)</c>, this converges at the same rate
        /// regardless of framerate. <paramref name="sharpness"/> is the decay constant: higher is snappier.
        /// </para>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 ExpDecayTo(this Vector3 current, Vector3 target, float sharpness, float deltaTime)
        {
            if ((target - current).IsNearZero()) return target;
            return Vector3.LerpUnclamped(target, current, Mathf.Exp(-sharpness * deltaTime));
        }

        /// <summary>Quadratic Bézier evaluation. For simple arcs, lobbed projectiles, and UI fly-to animations.</summary>
        public static Vector3 BezierTo(this Vector3 start, Vector3 control, Vector3 end, float t)
        {
            float u = 1f - t;
            return u * u * start + 2f * u * t * control + t * t * end;
        }

        /// <summary>Cubic Bézier evaluation.</summary>
        public static Vector3 BezierTo(this Vector3 start, Vector3 control1, Vector3 control2, Vector3 end, float t)
        {
            float u = 1f - t;
            float uu = u * u;
            float tt = t * t;
            return uu * u * start
                 + 3f * uu * t * control1
                 + 3f * u * tt * control2
                 + tt * t * end;
        }

        /// <summary>
        /// Remaps <paramref name="value"/> from range [<paramref name="inMin"/>, <paramref name="inMax"/>]
        /// to [<paramref name="outMin"/>, <paramref name="outMax"/>].
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 Remap(this Vector3 value, Vector3 inMin, Vector3 inMax, Vector3 outMin, Vector3 outMax)
        {
            Vector3 t = new(
                Mathf.InverseLerp(inMin.x, inMax.x, value.x),
                Mathf.InverseLerp(inMin.y, inMax.y, value.y),
                Mathf.InverseLerp(inMin.z, inMax.z, value.z));

            return new Vector3(
                Mathf.LerpUnclamped(outMin.x, outMax.x, t.x),
                Mathf.LerpUnclamped(outMin.y, outMax.y, t.y),
                Mathf.LerpUnclamped(outMin.z, outMax.z, t.z));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 Approach(this Vector3 current, Vector3 target, float maxDelta)
            => Vector3.MoveTowards(current, target, maxDelta);

        #endregion

        #region Geometry Queries

        /// <summary>Closest point to this position on the infinite line through A and B.</summary>
        public static Vector3 ClosestPointOnLine(this Vector3 point, Vector3 lineA, Vector3 lineB)
        {
            Vector3 ab = lineB - lineA;
            float sqrLen = ab.sqrMagnitude;
            if (sqrLen < SqrEpsilon) return lineA;
            return lineA + ab * (Vector3.Dot(point - lineA, ab) / sqrLen);
        }

        /// <summary>Closest point to this position on the finite segment A→B.</summary>
        public static Vector3 ClosestPointOnSegment(this Vector3 point, Vector3 segmentA, Vector3 segmentB)
        {
            Vector3 ab = segmentB - segmentA;
            float sqrLen = ab.sqrMagnitude;
            if (sqrLen < SqrEpsilon) return segmentA;
            float t = Mathf.Clamp01(Vector3.Dot(point - segmentA, ab) / sqrLen);
            return segmentA + ab * t;
        }

        /// <summary>Shortest distance from this position to the segment A→B.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DistanceToSegment(this Vector3 point, Vector3 segmentA, Vector3 segmentB)
            => point.DistanceTo(point.ClosestPointOnSegment(segmentA, segmentB));

        /// <summary>
        /// True if <paramref name="target"/> lies in front of this position relative to <paramref name="forward"/>.
        /// A cheap dot-product gate to run before any expensive line-of-sight raycast.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsInFrontOf(this Vector3 from, Vector3 target, Vector3 forward)
            => Vector3.Dot(forward, target - from) > 0f;

        #endregion

        #region Random

        /// <summary>
        /// Returns a random point inside the axis-aligned bounding box defined by
        /// <paramref name="min"/> and <paramref name="max"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 RandomInRange(Vector3 min, Vector3 max) =>
            new(
                Random.Range(min.x, max.x),
                Random.Range(min.y, max.y),
                Random.Range(min.z, max.z));

        /// <summary>Offsets this point by a uniform random amount on each axis. For muzzle spread and spawn jitter.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 WithRandomOffset(this Vector3 v, float maxOffset)
            => v + Random.insideUnitSphere * maxOffset;

        /// <summary>Offsets this point by a uniform random amount on the XZ plane only, preserving height.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 WithRandomHorizontalOffset(this Vector3 v, float maxOffset)
        {
            Vector2 circle = Random.insideUnitCircle * maxOffset;
            return new Vector3(v.x + circle.x, v.y, v.z + circle.y);
        }

        /// <summary>
        /// Perturbs this direction by up to <paramref name="maxAngleDegrees"/> in a random cone.
        /// The correct way to model weapon spread: angular, not positional, so error scales with range.
        /// </summary>
        public static Vector3 RandomizeDirection(this Vector3 direction, float maxAngleDegrees)
        {
            if (direction.sqrMagnitude < SqrEpsilon || maxAngleDegrees <= 0f) return direction;

            Vector3 axis = Vector3.Cross(direction, Random.onUnitSphere);
            if (axis.sqrMagnitude < SqrEpsilon) axis = Vector3.up;

            float angle = Random.Range(0f, maxAngleDegrees);
            return Quaternion.AngleAxis(angle, axis.normalized) * direction;
        }

        #endregion

        #region Aggregates

        /// <summary>
        /// Arithmetic mean of a point set. Returns <see cref="Vector3.zero"/> for an empty or null input.
        /// Enumerates by index, so no iterator is allocated.
        /// </summary>
        public static Vector3 Centroid(IReadOnlyList<Vector3> points)
        {
            if (points == null || points.Count == 0) return Vector3.zero;

            Vector3 sum = Vector3.zero;
            for (int i = 0; i < points.Count; i++) sum += points[i];
            return sum / points.Count;
        }

        /// <summary>Index of the point nearest to <paramref name="origin"/>, or −1 if the list is empty.</summary>
        public static int NearestIndex(this Vector3 origin, IReadOnlyList<Vector3> points)
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
        /// Formats with full precision. Unity's default <c>ToString()</c> rounds to one decimal place,
        /// which hides the sub-millimetre drift you are usually trying to diagnose.
        /// </summary>
        public static string ToDebugString(this Vector3 v, int decimals = 4)
        {
            string format = "F" + decimals;
            return $"({v.x.ToString(format)}, {v.y.ToString(format)}, {v.z.ToString(format)})";
        }

        #endregion
    }
}
