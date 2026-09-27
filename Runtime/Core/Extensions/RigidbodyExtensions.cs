using System.Runtime.CompilerServices;
using UnityEngine;

namespace Nexus.Core.Extensions
{
    /// <summary>
    /// Extension methods for <see cref="Rigidbody"/> that provide safe, allocation-free
    /// helpers for velocity manipulation, impulses, and kinematic state changes.
    /// <para>Design rules:</para>
    /// <list type="bullet">
    /// <item>All velocity comparisons use <c>sqrMagnitude</c> to avoid hidden square roots.</item>
    /// <item>Methods never write <c>NaN</c> or <c>Infinity</c> into physics state; degenerate
    /// inputs are clamped or ignored instead of corrupting the native PhysX body.</item>
    /// <item>Horizontal (XZ-plane) variants are provided for ground-based movement,
    /// AI steering, and vehicle logic where vertical velocity should be ignored.</item>
    /// <item>Zero allocation: no boxing, no LINQ, no temporary collections.</item>
    /// </list>
    /// </summary>
    public static class RigidbodyExtensions
    {
        /// <summary>
        /// Squared velocity threshold below which a <see cref="Rigidbody"/> is considered
        /// at rest. Using the squared value avoids a square root on every rest check,
        /// which matters when polling dozens of bodies per frame for sleep/animation state.
        /// </summary>
        private const float RestSqrEpsilon = 0.0001f; // 0.01 m/s

        #region Velocity Manipulation

        /// <summary>
        /// Clamps <see cref="Rigidbody.velocity"/> to <paramref name="maxSpeed"/> without
        /// altering direction. <br/>
        /// Prevents physics explosions from stacked forces (collisions, explosions, spring
        /// joints) from launching the body to infinity in a single frame.
        /// </summary>
        public static void ClampVelocity(this Rigidbody rb, float maxSpeed)
        {
            Vector3 velocity = rb.velocity;
            float sqrSpeed = velocity.sqrMagnitude;
            float sqrMax = maxSpeed * maxSpeed;
            if (sqrSpeed <= sqrMax) return;

            float speed = Mathf.Sqrt(sqrSpeed);
            rb.velocity = velocity * (maxSpeed / speed);
        }

        /// <summary>
        /// Clamps <see cref="Rigidbody.velocity"/> magnitude to the <paramref name="minSpeed"/>/
        /// <paramref name="maxSpeed"/> range, preserving direction. <br/>
        /// Useful for vehicles or projectiles that must never stall below a minimum speed
        /// nor exceed a maximum one (e.g. arcade racers, homing missiles).
        /// </summary>
        public static void ClampVelocity(this Rigidbody rb, float minSpeed, float maxSpeed)
        {
            Vector3 velocity = rb.velocity;
            float sqrSpeed = velocity.sqrMagnitude;
            if (sqrSpeed < RestSqrEpsilon) return; // no direction to preserve

            float speed = Mathf.Sqrt(sqrSpeed);
            float clamped = Mathf.Clamp(speed, minSpeed, maxSpeed);
            if (Mathf.Approximately(clamped, speed)) return;

            rb.velocity = velocity * (clamped / speed);
        }

        /// <summary>
        /// Returns <see cref="Rigidbody.velocity"/> with the height component removed.
        /// <para>
        /// Essential for ground-speed checks (sprint thresholds, footstep audio, blend
        /// trees) where a falling or jumping body should not read as "running fast".
        /// </para>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 HorizontalVelocity(this Rigidbody rb)
        {
            Vector3 v = rb.velocity;
            v.y = 0f;
            return v;
        }

        /// <summary>
        /// Squared horizontal speed, ignoring vertical velocity. <br/>
        /// Use for threshold comparisons (e.g. <c>IsSprinting</c>) to avoid a square root
        /// every frame across many characters.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SqrHorizontalSpeed(this Rigidbody rb)
            => rb.HorizontalVelocity().sqrMagnitude;

        /// <summary>
        /// Actual horizontal speed (metres/second) ignoring vertical velocity. <br/>
        /// Only call when the real value is needed (UI readouts, animator float
        /// parameters); prefer <see cref="SqrHorizontalSpeed"/> for comparisons.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float HorizontalSpeed(this Rigidbody rb)
            => Mathf.Sqrt(rb.SqrHorizontalSpeed());

        /// <summary>
        /// Replaces the Y component of <see cref="Rigidbody.velocity"/> in place, leaving
        /// horizontal momentum untouched. <br/>
        /// The standard way to implement a jump without killing existing run velocity,
        /// and without the <c>NaN</c> risk of manually rebuilding the vector.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetVelocityY(this Rigidbody rb, float y)
        {
            Vector3 v = rb.velocity;
            v.y = y;
            rb.velocity = v;
        }

        /// <summary>
        /// Immediately zeroes both linear and angular velocity. <br/>
        /// Required when returning a <see cref="Rigidbody"/> to an object pool or
        /// teleporting it; otherwise residual velocity carries over and causes the
        /// object to fly off on reactivation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StopMotion(this Rigidbody rb)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        #endregion

        #region Impulses & Forces

        /// <summary>
        /// Applies an impulse of <paramref name="force"/> along a safely normalized
        /// <paramref name="direction"/>. <br/>
        /// If <paramref name="direction"/> is zero-length the call is skipped instead of
        /// injecting <c>NaN</c> into <see cref="Rigidbody.velocity"/>, which would
        /// otherwise corrupt the entire PhysX simulation island the body belongs to.
        /// </summary>
        public static void AddImpulseSafe(this Rigidbody rb, Vector3 direction, float force)
        {
            float sqrLen = direction.sqrMagnitude;
            if (sqrLen < RestSqrEpsilon) return;

            Vector3 normalized = direction * (1f / Mathf.Sqrt(sqrLen));
            rb.AddForce(normalized * force, ForceMode.Impulse);
        }

        /// <summary>
        /// Applies an instantaneous vertical impulse, ideal for jump mechanics driven
        /// through <see cref="Rigidbody.AddForce(Vector3, ForceMode)"/> rather than
        /// direct velocity assignment, keeping collision/friction resolution consistent
        /// with the physics step that follows.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddJumpImpulse(this Rigidbody rb, float force)
            => rb.AddForce(Vector3.up * force, ForceMode.Impulse);

        /// <summary>
        /// Applies a radial explosion impulse from <paramref name="origin"/> without the
        /// per-collider physics query overhead of <see cref="Rigidbody.AddExplosionForce"/>.
        /// Falloff is linear from full <paramref name="force"/> at the origin to zero at
        /// <paramref name="radius"/>.
        /// <para>
        /// Returns without effect if the body's center of mass sits exactly on
        /// <paramref name="origin"/>, avoiding a divide-by-zero direction, or if it lies
        /// outside <paramref name="radius"/>.
        /// </para>
        /// </summary>
        public static void AddExplosionImpulseSafe(this Rigidbody rb, Vector3 origin, float force, float radius)
        {
            Vector3 offset = rb.worldCenterOfMass - origin;
            float sqrDist = offset.sqrMagnitude;
            if (sqrDist < RestSqrEpsilon) return;

            float dist = Mathf.Sqrt(sqrDist);
            if (dist > radius) return;

            float falloff = 1f - (dist / radius);
            Vector3 direction = offset * (1f / dist);
            rb.AddForce(direction * (force * falloff), ForceMode.Impulse);
        }

        #endregion

        #region Kinematic & Positioning

        /// <summary>
        /// Moves the body to <paramref name="position"/> via <see cref="Rigidbody.MovePosition"/>
        /// only if the target is finite. <br/>
        /// A single <c>NaN</c> or <c>Infinity</c> passed to <c>MovePosition</c> poisons the
        /// rigidbody's internal transform and can silently break physics for the rest of
        /// the session; this guard keeps a bad AI/network value from ever reaching PhysX.
        /// </summary>
        public static void MovePositionSafe(this Rigidbody rb, Vector3 position)
        {
            if (float.IsNaN(position.x) || float.IsNaN(position.y) || float.IsNaN(position.z)) return;
            if (float.IsInfinity(position.x) || float.IsInfinity(position.y) || float.IsInfinity(position.z)) return;

            rb.MovePosition(position);
        }

        /// <summary>
        /// Enables <see cref="Rigidbody.isKinematic"/> and returns the velocity at the
        /// moment of the switch, so it can be restored later via
        /// <see cref="RestoreFromKinematic"/>.
        /// <para>
        /// Use around cutscenes or root-motion segments where a character should stop
        /// reacting to physics forces but must resume with its prior momentum afterward.
        /// </para>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 SetKinematicAndCacheVelocity(this Rigidbody rb)
        {
            Vector3 cached = rb.velocity;
            rb.isKinematic = true;
            return cached;
        }

        /// <summary>
        /// Disables <see cref="Rigidbody.isKinematic"/> and reapplies a previously cached
        /// velocity, completing the pairing started by <see cref="SetKinematicAndCacheVelocity"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RestoreFromKinematic(this Rigidbody rb, Vector3 cachedVelocity)
        {
            rb.isKinematic = false;
            rb.velocity = cachedVelocity;
        }

        /// <summary>
        /// Rotates the body toward the direction of its current velocity using
        /// <see cref="Rigidbody.MoveRotation"/>. <br/>
        /// Standard technique for vehicles, projectiles, and swimming/flying creatures
        /// that must visually face their movement direction. Does nothing while the
        /// body is nearly stationary, preventing rotation jitter at rest.
        /// </summary>
        public static void FaceVelocityDirection(this Rigidbody rb, float rotationSpeedDegrees)
        {
            Vector3 velocity = rb.velocity;
            float sqrSpeed = velocity.sqrMagnitude;
            if (sqrSpeed < RestSqrEpsilon) return;

            Quaternion target = Quaternion.LookRotation(velocity.normalized, Vector3.up);
            Quaternion rotated = Quaternion.RotateTowards(rb.rotation, target, rotationSpeedDegrees * Time.fixedDeltaTime);
            rb.MoveRotation(rotated);
        }

        #endregion

        #region Queries & State

        /// <summary>
        /// True if combined linear and angular motion is below the rest threshold. <br/>
        /// Cheaper and more reliable than checking <see cref="Rigidbody.IsSleeping"/> alone,
        /// since a body can be technically awake yet have negligible velocity for several
        /// frames before PhysX puts it to sleep.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsAtRest(this Rigidbody rb)
            => rb.velocity.sqrMagnitude < RestSqrEpsilon && rb.angularVelocity.sqrMagnitude < RestSqrEpsilon;

        /// <summary>
        /// True if the body's linear velocity exceeds <paramref name="sqrSpeedThreshold"/>. <br/>
        /// Pass a pre-squared threshold (e.g. <c>walkSpeed * walkSpeed</c>) to drive
        /// animator blend trees without a square root per character per frame.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsMoving(this Rigidbody rb, float sqrSpeedThreshold)
            => rb.velocity.sqrMagnitude > sqrSpeedThreshold;

        /// <summary>
        /// True if every component of <see cref="Rigidbody.velocity"/> is finite. <br/>
        /// Diagnostic guard for catching corrupted physics state (e.g. after a bad
        /// impulse or network desync) before it propagates into gameplay logic.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasValidVelocity(this Rigidbody rb)
        {
            Vector3 v = rb.velocity;
            return !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z)
                && !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
        }

        #endregion
    }
}