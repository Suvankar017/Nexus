using System.Runtime.CompilerServices;
using UnityEngine;

namespace Nexus.Core.Extensions
{
    /// <summary>
    /// Production-ready extension methods for <see cref="Quaternion"/> covering validation,
    /// safe normalization, yaw/pitch/roll extraction, direction vectors, plane-flattened
    /// rotations and interpolation helpers.
    /// <para>Design rules:</para>
    /// <list type="bullet">
    ///     <item>Every comparison that can be expressed via <see cref="Quaternion.Dot(Quaternion, Quaternion)"/> avoids the trigonometric cost of <see cref="Quaternion.Angle(Quaternion, Quaternion)"/>, mirroring the sqrMagnitude-over-magnitude rule for vectors.</item>
    ///     <item>Degenerate quaternions (zero, NaN, Infinity) never silently propagate; every unsafe operation has a guarded and a fallback-accepting variant.</item>
    ///     <item>Yaw/pitch/roll extraction assumes Unity's Y-up, left-handed convention and is intended for character facing, camera rigs and AI steering — not general-purpose 3D math.</item>
    ///     <item>Angles are exposed in degrees at the public API per Unity convention; radians are used only internally for trigonometric calls.</item>
    ///     <item>Zero allocation across the entire class. No boxing, no LINQ.</item>
    /// </list>
    /// </summary>
    public static class QuaternionExtensions
    {
        #region Constants

        /// <summary>
        /// Default tolerance used by approximate-equality and identity checks to absorb
        /// floating-point noise accumulated from repeated slerps, animation blending and
        /// physics-driven rotation.
        /// </summary>
        private const float DefaultEpsilon = 1e-5f;

        /// <summary>
        /// Squared-magnitude tolerance used to detect degenerate (near-zero) quaternions before
        /// normalization or inversion, avoiding a division that would otherwise produce
        /// <c>NaN</c> or <c>Infinity</c>.
        /// </summary>
        private const float SqrEpsilon = 1e-8f;

        #endregion

        #region Validation & Safety

        /// <summary>
        /// Returns <c>true</c> if every component of <paramref name="q"/> is finite.
        /// <br/>
        /// Explicitly rejects <c>NaN</c> and <c>Infinity</c>. A single invalid quaternion applied
        /// to <see cref="Transform.rotation"/> permanently corrupts the transform's rendering and
        /// physics state, requiring a full reset of the affected object.
        /// </summary>
        /// <param name="q">Quaternion to validate.</param>
        /// <returns><c>true</c> if x, y, z and w are all finite.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsValid(this Quaternion q)
            => float.IsFinite(q.x) && float.IsFinite(q.y) && float.IsFinite(q.z) && float.IsFinite(q.w);

        /// <summary>
        /// Returns <c>true</c> if <paramref name="q"/> is already unit-length within
        /// <paramref name="epsilon"/>.
        /// <br/>
        /// Cheap guard to skip redundant <see cref="Quaternion.Normalize(Quaternion)"/> calls in
        /// hot paths such as per-frame bone or IK rotation blending.
        /// </summary>
        /// <param name="q">Quaternion to test.</param>
        /// <param name="epsilon">Tolerance around a squared magnitude of <c>1</c>. Defaults to <c>DefaultEpsilon</c>.</param>
        /// <returns><c>true</c> if <paramref name="q"/> is approximately normalized.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNormalized(this Quaternion q, float epsilon = DefaultEpsilon)
        {
            float sqrMag = q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;
            return Mathf.Abs(sqrMag - 1f) <= epsilon;
        }

        /// <summary>
        /// Returns <paramref name="q"/> if <see cref="IsValid(Quaternion)"/> is <c>true</c>,
        /// otherwise returns <paramref name="fallback"/>.
        /// <br/>
        /// Ideal for sanitizing rotations deserialized from save data, network packets or
        /// procedural generation before assigning them to a <see cref="Transform"/>.
        /// </summary>
        /// <param name="q">Candidate quaternion.</param>
        /// <param name="fallback">Safe rotation returned when <paramref name="q"/> is invalid.</param>
        /// <returns>Validated quaternion or <paramref name="fallback"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Quaternion ValidOr(this Quaternion q, Quaternion fallback)
            => q.IsValid() ? q : fallback;

        /// <summary>
        /// Returns <paramref name="q"/> normalized, or <see cref="Quaternion.identity"/> if
        /// <paramref name="q"/> is invalid or effectively zero-length.
        /// <br/>
        /// Normalizing a zero or near-zero quaternion divides by (near) zero and produces
        /// <c>NaN</c> components. This is the safe default for any runtime-constructed rotation
        /// (e.g. accumulated angular velocity integration) before assigning it to a transform.
        /// </summary>
        /// <param name="q">Quaternion to normalize.</param>
        /// <returns>A normalized quaternion, or <see cref="Quaternion.identity"/> on degenerate input.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Quaternion NormalizedSafe(this Quaternion q)
            => q.NormalizedOr(Quaternion.identity);

        /// <summary>
        /// Returns <paramref name="q"/> normalized, or <paramref name="fallback"/> if
        /// <paramref name="q"/> is invalid or effectively zero-length.
        /// <br/>
        /// Use when identity is not an appropriate default — e.g. falling back to a character's
        /// last known good facing rotation instead of snapping to world-forward.
        /// </summary>
        /// <param name="q">Quaternion to normalize.</param>
        /// <param name="fallback">Rotation returned when <paramref name="q"/> cannot be safely normalized.</param>
        /// <returns>A normalized quaternion, or <paramref name="fallback"/> on degenerate input.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Quaternion NormalizedOr(this Quaternion q, Quaternion fallback)
        {
            if (!q.IsValid()) return fallback;

            float sqrMag = q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;
            if (sqrMag < SqrEpsilon) return fallback;

            return Quaternion.Normalize(q);
        }

        #endregion

        #region Comparison

        /// <summary>
        /// Returns <c>true</c> if <paramref name="a"/> and <paramref name="b"/> represent
        /// approximately the same rotation.
        /// <br/>
        /// Uses the absolute dot product rather than component-wise comparison because a unit
        /// quaternion and its negation (<c>q</c> and <c>-q</c>) represent an identical rotation —
        /// a naive per-component equality check would incorrectly report these as different.
        /// </summary>
        /// <param name="a">First rotation.</param>
        /// <param name="b">Second rotation.</param>
        /// <param name="epsilon">Tolerance below the maximum dot product of <c>1</c>. Defaults to <c>DefaultEpsilon</c>.</param>
        /// <returns><c>true</c> if the rotations are approximately equivalent.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Approximately(this Quaternion a, Quaternion b, float epsilon = DefaultEpsilon)
            => Mathf.Abs(Quaternion.Dot(a, b)) >= 1f - epsilon;

        /// <summary>
        /// Returns <c>true</c> if <paramref name="q"/> is approximately <see cref="Quaternion.identity"/>.
        /// <br/>
        /// Ideal for detecting "no rotation applied" states in procedural animation rigs or
        /// verifying a reset operation completed correctly.
        /// </summary>
        /// <param name="q">Rotation to test.</param>
        /// <param name="epsilon">Tolerance passed to <see cref="Approximately(Quaternion, Quaternion, float)"/>. Defaults to <c>DefaultEpsilon</c>.</param>
        /// <returns><c>true</c> if <paramref name="q"/> is approximately identity.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsIdentity(this Quaternion q, float epsilon = DefaultEpsilon)
            => q.Approximately(Quaternion.identity, epsilon);

        /// <summary>
        /// Returns <c>true</c> if the angular difference between <paramref name="a"/> and
        /// <paramref name="b"/> is within <paramref name="maxDegrees"/>.
        /// <br/>
        /// <para>
        /// Uses the identity <c>dot(a,b) = cos(theta/2)</c> for unit quaternions to compare
        /// directly against a precomputed cosine threshold, entirely avoiding the
        /// <see cref="Mathf.Acos(float)"/> call hidden inside <see cref="Quaternion.Angle(Quaternion, Quaternion)"/>.
        /// </para>
        /// <para>
        /// This is the quaternion equivalent of preferring <c>sqrMagnitude</c> over
        /// <c>magnitude</c> for vectors — use this for threshold checks (has the turret aimed
        /// closely enough?) and reserve <see cref="AngleTo(Quaternion, Quaternion)"/> for cases
        /// that need the actual angle value.
        /// </para>
        /// </summary>
        /// <param name="a">First rotation.</param>
        /// <param name="b">Second rotation.</param>
        /// <param name="maxDegrees">Maximum allowed angular difference, in degrees.</param>
        /// <returns><c>true</c> if the angle between <paramref name="a"/> and <paramref name="b"/> is within <paramref name="maxDegrees"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsWithinAngleOf(this Quaternion a, Quaternion b, float maxDegrees)
        {
            float dot = Mathf.Abs(Quaternion.Dot(a, b));
            float cosHalfMax = Mathf.Cos(maxDegrees * 0.5f * Mathf.Deg2Rad);
            return dot >= cosHalfMax;
        }

        #endregion

        #region Angle Extraction

        /// <summary>
        /// Returns the real angular difference between <paramref name="a"/> and <paramref name="b"/>,
        /// in degrees.
        /// <br/>
        /// Named explicitly (not <c>SqrAngleTo</c>) because this performs the trigonometric
        /// evaluation hidden inside <see cref="Quaternion.Angle(Quaternion, Quaternion)"/>. Use
        /// this only when the actual angle value is needed for display or a PID controller; use
        /// <see cref="IsWithinAngleOf(Quaternion, Quaternion, float)"/> for pure threshold checks.
        /// </summary>
        /// <param name="a">Starting rotation.</param>
        /// <param name="b">Target rotation.</param>
        /// <returns>The angle between the two rotations, in degrees, in [0, 180].</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float AngleTo(this Quaternion a, Quaternion b)
            => Quaternion.Angle(a, b);

        /// <summary>
        /// Returns the yaw component (rotation around the world Y axis) of <paramref name="q"/>,
        /// wrapped into (-180, 180] degrees.
        /// <br/>
        /// Standard extraction for character facing, camera horizontal orbit and top-down
        /// steering logic where only left/right heading matters.
        /// </summary>
        /// <param name="q">Rotation to extract yaw from.</param>
        /// <returns>Yaw angle in degrees, wrapped to (-180, 180].</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ToYaw(this Quaternion q)
            => q.eulerAngles.y.WrapAngle();

        /// <summary>
        /// Returns the pitch component (rotation around the local X axis) of <paramref name="q"/>,
        /// wrapped into (-180, 180] degrees.
        /// <br/>
        /// Common for clamping camera look-up/look-down limits or detecting weapon aim elevation.
        /// </summary>
        /// <param name="q">Rotation to extract pitch from.</param>
        /// <returns>Pitch angle in degrees, wrapped to (-180, 180].</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ToPitch(this Quaternion q)
            => q.eulerAngles.x.WrapAngle();

        /// <summary>
        /// Returns the roll component (rotation around the local Z axis) of <paramref name="q"/>,
        /// wrapped into (-180, 180] degrees.
        /// <br/>
        /// Useful for banking calculations in flight/vehicle controllers or detecting an
        /// unintended tilt on a character that should always remain upright.
        /// </summary>
        /// <param name="q">Rotation to extract roll from.</param>
        /// <returns>Roll angle in degrees, wrapped to (-180, 180].</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ToRoll(this Quaternion q)
            => q.eulerAngles.z.WrapAngle();

        #endregion

        #region Direction Vectors

        /// <summary>
        /// Returns the forward direction (local +Z) of <paramref name="q"/> in world space.
        /// <br/>
        /// Equivalent to but more explicit than <c>q * Vector3.forward</c> at call sites, and
        /// mirrors <see cref="Transform.forward"/> for rotations not yet assigned to a transform.
        /// </summary>
        /// <param name="q">Rotation to extract the forward direction from.</param>
        /// <returns>Unit-length forward direction vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 GetForward(this Quaternion q)
            => q * Vector3.forward;

        /// <summary>
        /// Returns the backward direction (local -Z) of <paramref name="q"/> in world space.
        /// <br/>
        /// Useful for retreat steering, recoil kickback direction and reverse-facing checks.
        /// </summary>
        /// <param name="q">Rotation to extract the backward direction from.</param>
        /// <returns>Unit-length backward direction vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 GetBack(this Quaternion q)
            => q * Vector3.back;

        /// <summary>
        /// Returns the right direction (local +X) of <paramref name="q"/> in world space.
        /// <br/>
        /// Common for strafing input transformation and weapon muzzle offset calculations.
        /// </summary>
        /// <param name="q">Rotation to extract the right direction from.</param>
        /// <returns>Unit-length right direction vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 GetRight(this Quaternion q)
            => q * Vector3.right;

        /// <summary>
        /// Returns the left direction (local -X) of <paramref name="q"/> in world space.
        /// <br/>
        /// Positive-phrased counterpart to <see cref="GetRight(Quaternion)"/> for readability in
        /// strafe-left branches of movement code.
        /// </summary>
        /// <param name="q">Rotation to extract the left direction from.</param>
        /// <returns>Unit-length left direction vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 GetLeft(this Quaternion q)
            => q * Vector3.left;

        /// <summary>
        /// Returns the up direction (local +Y) of <paramref name="q"/> in world space.
        /// <br/>
        /// Used for orienting particle emitters, ledge-grab checks and camera up-vector alignment
        /// against a rotated character or vehicle.
        /// </summary>
        /// <param name="q">Rotation to extract the up direction from.</param>
        /// <returns>Unit-length up direction vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 GetUp(this Quaternion q)
            => q * Vector3.up;

        /// <summary>
        /// Returns the down direction (local -Y) of <paramref name="q"/> in world space.
        /// <br/>
        /// Useful for ground-check raycasts that must follow a tilted character or vehicle's
        /// local orientation rather than always casting along world-down.
        /// </summary>
        /// <param name="q">Rotation to extract the down direction from.</param>
        /// <returns>Unit-length down direction vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 GetDown(this Quaternion q)
            => q * Vector3.down;

        #endregion

        #region Flatten & Horizontal

        /// <summary>
        /// Returns a copy of <paramref name="q"/> with pitch and roll removed, keeping only the
        /// yaw (Y-axis) rotation. Falls back to <see cref="Quaternion.identity"/> if
        /// <paramref name="q"/> points straight up or down, where yaw is undefined.
        /// <br/>
        /// Essential for humanoid character bodies that must stay upright while a separate
        /// camera or aim rig freely pitches — applying an unflattened rotation to the body mesh
        /// would visibly tilt the character with the camera.
        /// </summary>
        /// <param name="q">Rotation to flatten.</param>
        /// <returns>A yaw-only rotation, or <see cref="Quaternion.identity"/> on a degenerate (straight up/down) input.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Quaternion FlattenToYaw(this Quaternion q)
            => q.FlattenToYawOr(Quaternion.identity);

        /// <summary>
        /// Returns a copy of <paramref name="q"/> with pitch and roll removed, keeping only the
        /// yaw (Y-axis) rotation. Falls back to <paramref name="fallback"/> if <paramref name="q"/>
        /// points straight up or down, where yaw is undefined.
        /// <br/>
        /// Use when identity is not an appropriate default — e.g. falling back to a character's
        /// last valid facing rotation instead of snapping to world-forward when looking
        /// vertically.
        /// </summary>
        /// <param name="q">Rotation to flatten.</param>
        /// <param name="fallback">Rotation returned when the forward vector's horizontal projection is degenerate.</param>
        /// <returns>A yaw-only rotation, or <paramref name="fallback"/> on degenerate input.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Quaternion FlattenToYawOr(this Quaternion q, Quaternion fallback)
        {
            Vector3 forward = q.GetForward();
            forward.y = 0f;

            if (forward.sqrMagnitude < SqrEpsilon)
                return fallback;

            return Quaternion.LookRotation(forward.normalized, Vector3.up);
        }

        #endregion

        #region Interpolation

        /// <summary>
        /// Returns the spherical interpolation from <paramref name="q"/> towards
        /// <paramref name="target"/> by <paramref name="t"/>, clamped to [0,1].
        /// <br/>
        /// The standard, constant-angular-velocity choice for camera reorientation, weapon aim
        /// smoothing and cutscene rotation blending.
        /// </summary>
        /// <param name="q">Starting rotation.</param>
        /// <param name="target">Target rotation.</param>
        /// <param name="t">Interpolation factor in [0,1].</param>
        /// <returns>The interpolated rotation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Quaternion SlerpTo(this Quaternion q, Quaternion target, float t)
            => Quaternion.Slerp(q, target, t);

        /// <summary>
        /// Returns the unclamped spherical interpolation from <paramref name="q"/> towards
        /// <paramref name="target"/> by <paramref name="t"/>.
        /// <br/>
        /// Intended for exaggerated overshoot effects (recoil snap-back, anticipation poses)
        /// where <paramref name="t"/> is intentionally driven outside [0,1] by an easing curve.
        /// </summary>
        /// <param name="q">Starting rotation.</param>
        /// <param name="target">Target rotation.</param>
        /// <param name="t">Unclamped interpolation factor.</param>
        /// <returns>The unclamped interpolated rotation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Quaternion SlerpToUnclamped(this Quaternion q, Quaternion target, float t)
            => Quaternion.SlerpUnclamped(q, target, t);

        /// <summary>
        /// Returns the normalized linear interpolation from <paramref name="q"/> towards
        /// <paramref name="target"/> by <paramref name="t"/>, clamped to [0,1].
        /// <br/>
        /// Cheaper than <see cref="SlerpTo(Quaternion, Quaternion, float)"/> and visually
        /// indistinguishable for small angular deltas — prefer this for per-frame bone blending
        /// in animation rigs where the rotation change between frames is small.
        /// </summary>
        /// <param name="q">Starting rotation.</param>
        /// <param name="target">Target rotation.</param>
        /// <param name="t">Interpolation factor in [0,1].</param>
        /// <returns>The interpolated rotation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Quaternion LerpTo(this Quaternion q, Quaternion target, float t)
            => Quaternion.Lerp(q, target, t);

        /// <summary>
        /// Rotates from <paramref name="q"/> towards <paramref name="target"/> by at most
        /// <paramref name="maxDegreesDelta"/> degrees.
        /// <br/>
        /// The correct choice for angularly-limited motion — turret traverse speed, vehicle
        /// steering rate — where the rotation must respect a maximum degrees-per-call budget
        /// rather than a normalized [0,1] blend factor.
        /// </summary>
        /// <param name="q">Starting rotation.</param>
        /// <param name="target">Target rotation.</param>
        /// <param name="maxDegreesDelta">Maximum rotation step, in degrees.</param>
        /// <returns>The rotation stepped towards <paramref name="target"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Quaternion RotateTowardsAngle(this Quaternion q, Quaternion target, float maxDegreesDelta)
            => Quaternion.RotateTowards(q, target, maxDegreesDelta);

        #endregion

        #region Conjugate & Inverse

        /// <summary>
        /// Returns the conjugate of <paramref name="q"/> (negated x, y, z; unchanged w).
        /// <br/>
        /// For a <b>unit</b> quaternion the conjugate is mathematically equal to
        /// <see cref="Quaternion.Inverse(Quaternion)"/> but is computed without the division used
        /// internally by <c>Inverse</c>, making it the cheaper choice in hot paths (IK solvers,
        /// per-frame bone correction) where the input is already known to be normalized.
        /// </summary>
        /// <param name="q">Unit quaternion to conjugate.</param>
        /// <returns>The conjugated quaternion.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Quaternion GetConjugate(this Quaternion q)
            => new Quaternion(-q.x, -q.y, -q.z, q.w);

        /// <summary>
        /// Returns the inverse of <paramref name="q"/>, or <see cref="Quaternion.identity"/> if
        /// <paramref name="q"/> is invalid or effectively zero-length.
        /// <br/>
        /// Guards <see cref="Quaternion.Inverse(Quaternion)"/> against dividing by a near-zero
        /// squared magnitude, which would otherwise produce a <c>NaN</c> rotation and silently
        /// corrupt any transform it is subsequently applied to.
        /// </summary>
        /// <param name="q">Quaternion to invert.</param>
        /// <returns>The inverse rotation, or <see cref="Quaternion.identity"/> on degenerate input.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Quaternion InverseSafe(this Quaternion q)
        {
            if (!q.IsValid()) return Quaternion.identity;

            float sqrMag = q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;
            if (sqrMag < SqrEpsilon) return Quaternion.identity;

            return Quaternion.Inverse(q);
        }

        #endregion

        #region Conversion & Debug

        /// <summary>
        /// Returns a compact debug string showing both the raw quaternion components and the
        /// equivalent Euler angles.
        /// <br/>
        /// Raw <c>(x,y,z,w)</c> values are unintuitive to read in logs; pairing them with Euler
        /// angles makes rotation bugs (unexpected tilt, wrong facing direction) far faster to spot
        /// during debugging. Allocates a string and is intended for logs/inspectors, not hot paths.
        /// </summary>
        /// <param name="q">Rotation to format.</param>
        /// <returns>A formatted debug string.</returns>
        public static string ToDebugString(this Quaternion q)
        {
            Vector3 euler = q.eulerAngles;
            return $"Quaternion(x:{q.x:F3}, y:{q.y:F3}, z:{q.z:F3}, w:{q.w:F3}) Euler(x:{euler.x:F1}, y:{euler.y:F1}, z:{euler.z:F1})";
        }

        #endregion

        #region Safe Construction

        /// <summary>
        /// Builds a world-space rotation facing <paramref name="direction"/> with world-up as the
        /// up reference, falling back to <see cref="Quaternion.identity"/> if
        /// <paramref name="direction"/> is too close to zero-length to determine an orientation.
        /// <br/>
        /// <see cref="Quaternion.LookRotation(Vector3)"/> throws no exception on a zero vector but
        /// silently returns an invalid rotation; this guard is essential when
        /// <paramref name="direction"/> comes from a runtime calculation (e.g. velocity, or a
        /// target-minus-origin vector) that can legitimately be zero for a single frame.
        /// </summary>
        /// <param name="direction">Desired forward direction.</param>
        /// <returns>A valid look rotation, or <see cref="Quaternion.identity"/> on degenerate input.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Quaternion LookRotationSafe(Vector3 direction)
            => LookRotationSafe(direction, Vector3.up, Quaternion.identity);

        /// <summary>
        /// Builds a world-space rotation facing <paramref name="direction"/> using
        /// <paramref name="up"/> as the up reference, falling back to
        /// <see cref="Quaternion.identity"/> if <paramref name="direction"/> is too close to
        /// zero-length to determine an orientation.
        /// </summary>
        /// <param name="direction">Desired forward direction.</param>
        /// <param name="up">Reference up vector, typically <see cref="Vector3.up"/>.</param>
        /// <returns>A valid look rotation, or <see cref="Quaternion.identity"/> on degenerate input.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Quaternion LookRotationSafe(Vector3 direction, Vector3 up)
            => LookRotationSafe(direction, up, Quaternion.identity);

        /// <summary>
        /// Builds a world-space rotation facing <paramref name="direction"/> using
        /// <paramref name="up"/> as the up reference, falling back to <paramref name="fallback"/>
        /// if <paramref name="direction"/> is too close to zero-length to determine an
        /// orientation.
        /// <br/>
        /// Standard entry point for AI steering ("face the target") and projectile orientation
        /// ("align to velocity"), where the source vector may momentarily collapse to zero
        /// (e.g. a target exactly overlapping the shooter's position).
        /// </summary>
        /// <param name="direction">Desired forward direction.</param>
        /// <param name="up">Reference up vector, typically <see cref="Vector3.up"/>.</param>
        /// <param name="fallback">Rotation returned when <paramref name="direction"/> is degenerate.</param>
        /// <returns>A valid look rotation, or <paramref name="fallback"/> on degenerate input.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Quaternion LookRotationSafe(Vector3 direction, Vector3 up, Quaternion fallback)
        {
            if (direction.sqrMagnitude < SqrEpsilon)
                return fallback;

            return Quaternion.LookRotation(direction, up);
        }

        #endregion
    }
}