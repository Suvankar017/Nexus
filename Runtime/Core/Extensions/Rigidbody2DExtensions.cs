using System.Runtime.CompilerServices;
using UnityEngine;

namespace Nexus.Core.Extensions
{
    /// <summary>
    /// Extension methods for <see cref="Rigidbody2D"/> that provide safe, allocation-free
    /// helpers for velocity manipulation, impulses, and kinematic state changes.
    /// <para>Design rules:</para>
    /// <list type="bullet">
    /// <item>All velocity comparisons use <c>sqrMagnitude</c> to avoid hidden square roots.</item>
    /// <item>Methods never write <c>NaN</c> or <c>Infinity</c> into physics state; degenerate
    /// inputs are clamped or ignored instead of corrupting the native Box2D body.</item>
    /// <item>Rotation helpers assume a rotation of <c>0</c> faces <c>+X</c>, matching
    /// <see cref="Mathf.Atan2(float, float)"/> conventions; flip the sprite or offset the
    /// angle if your art faces <c>+Y</c> at rest.</item>
    /// <item>Zero allocation: no boxing, no LINQ, no temporary collections.</item>
    /// </list>
    /// </summary>
    public static class Rigidbody2DExtensions
    {
        /// <summary>
        /// Squared linear velocity threshold below which a <see cref="Rigidbody2D"/> is
        /// considered at rest. Squared to avoid a square root on every rest check when
        /// polling many bodies per frame for sleep/animation state.
        /// </summary>
        private const float LinearRestSqrEpsilon = 0.0001f; // 0.01 m/s

        /// <summary>
        /// Squared angular velocity threshold (degrees/second) below which a
        /// <see cref="Rigidbody2D"/> is considered rotationally at rest.
        /// </summary>
        private const float AngularRestSqrEpsilon = 1f; // 1 deg/s

        #region Velocity Manipulation

        /// <summary>
        /// Clamps <see cref="Rigidbody2D.velocity"/> to <paramref name="maxSpeed"/> without
        /// altering direction. <br/>
        /// Prevents chained collisions or repeated force application from launching a 2D
        /// body off-screen in a single fixed update.
        /// </summary>
        public static void ClampVelocity(this Rigidbody2D rb, float maxSpeed)
        {
            Vector2 velocity = rb.velocity;
            float sqrSpeed = velocity.sqrMagnitude;
            float sqrMax = maxSpeed * maxSpeed;
            if (sqrSpeed <= sqrMax) return;

            float speed = Mathf.Sqrt(sqrSpeed);
            rb.velocity = velocity * (maxSpeed / speed);
        }

        /// <summary>
        /// Clamps <see cref="Rigidbody2D.velocity"/> magnitude to the <paramref name="minSpeed"/>/
        /// <paramref name="maxSpeed"/> range, preserving direction. <br/>
        /// Useful for top-down vehicles or auto-scrollers that must never fall below a
        /// minimum speed nor exceed a maximum one.
        /// </summary>
        public static void ClampVelocity(this Rigidbody2D rb, float minSpeed, float maxSpeed)
        {
            Vector2 velocity = rb.velocity;
            float sqrSpeed = velocity.sqrMagnitude;
            if (sqrSpeed < LinearRestSqrEpsilon) return; // no direction to preserve

            float speed = Mathf.Sqrt(sqrSpeed);
            float clamped = Mathf.Clamp(speed, minSpeed, maxSpeed);
            if (Mathf.Approximately(clamped, speed)) return;

            rb.velocity = velocity * (clamped / speed);
        }

        /// <summary>
        /// Replaces the X component of <see cref="Rigidbody2D.velocity"/> in place,
        /// leaving vertical velocity untouched. <br/>
        /// Used for platformer air control and dash mechanics where horizontal input
        /// should override momentum without cancelling a jump or fall in progress.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetVelocityX(this Rigidbody2D rb, float x)
        {
            Vector2 v = rb.velocity;
            v.x = x;
            rb.velocity = v;
        }

        /// <summary>
        /// Replaces the Y component of <see cref="Rigidbody2D.velocity"/> in place,
        /// leaving horizontal velocity untouched. <br/>
        /// The standard way to implement a platformer jump without killing existing
        /// run speed, and without the <c>NaN</c> risk of manually rebuilding the vector.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetVelocityY(this Rigidbody2D rb, float y)
        {
            Vector2 v = rb.velocity;
            v.y = y;
            rb.velocity = v;
        }

        /// <summary>
        /// Immediately zeroes both linear and angular velocity. <br/>
        /// Required when returning a <see cref="Rigidbody2D"/> to an object pool or
        /// teleporting it; otherwise residual velocity carries over and causes the
        /// object to slide or spin on reactivation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StopMotion(this Rigidbody2D rb)
        {
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        #endregion

        #region Impulses & Forces

        /// <summary>
        /// Applies an impulse of <paramref name="force"/> along a safely normalized
        /// <paramref name="direction"/>. <br/>
        /// If <paramref name="direction"/> is zero-length the call is skipped instead of
        /// injecting <c>NaN</c> into <see cref="Rigidbody2D.velocity"/>, which would
        /// otherwise corrupt the Box2D simulation island the body belongs to.
        /// </summary>
        public static void AddImpulseSafe(this Rigidbody2D rb, Vector2 direction, float force)
        {
            float sqrLen = direction.sqrMagnitude;
            if (sqrLen < LinearRestSqrEpsilon) return;

            Vector2 normalized = direction * (1f / Mathf.Sqrt(sqrLen));
            rb.AddForce(normalized * force, ForceMode2D.Impulse);
        }

        /// <summary>
        /// Applies an instantaneous vertical impulse, ideal for platformer jump mechanics
        /// driven through <see cref="Rigidbody2D.AddForce(Vector2, ForceMode2D)"/> rather
        /// than direct velocity assignment, keeping collision resolution consistent with
        /// the physics step that follows.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddJumpImpulse(this Rigidbody2D rb, float force)
            => rb.AddForce(Vector2.up * force, ForceMode2D.Impulse);

        /// <summary>
        /// Applies a radial explosion impulse from <paramref name="origin"/> without the
        /// per-collider physics query overhead of a manual <see cref="Physics2D.OverlapCircleAll"/>
        /// scan. Falloff is linear from full <paramref name="force"/> at the origin to
        /// zero at <paramref name="radius"/>.
        /// <para>
        /// Returns without effect if the body's center of mass sits exactly on
        /// <paramref name="origin"/>, avoiding a divide-by-zero direction, or if it lies
        /// outside <paramref name="radius"/>.
        /// </para>
        /// </summary>
        public static void AddExplosionImpulseSafe(this Rigidbody2D rb, Vector2 origin, float force, float radius)
        {
            Vector2 offset = rb.worldCenterOfMass - origin;
            float sqrDist = offset.sqrMagnitude;
            if (sqrDist < LinearRestSqrEpsilon) return;

            float dist = Mathf.Sqrt(sqrDist);
            if (dist > radius) return;

            float falloff = 1f - (dist / radius);
            Vector2 direction = offset * (1f / dist);
            rb.AddForce(direction * (force * falloff), ForceMode2D.Impulse);
        }

        #endregion

        #region Kinematic & Positioning

        /// <summary>
        /// Moves the body to <paramref name="position"/> via <see cref="Rigidbody2D.MovePosition"/>
        /// only if the target is finite. <br/>
        /// A single <c>NaN</c> or <c>Infinity</c> passed to <c>MovePosition</c> poisons the
        /// rigidbody's internal transform and can silently break physics for the rest of
        /// the session; this guard keeps a bad AI/network value from ever reaching Box2D.
        /// </summary>
        public static void MovePositionSafe(this Rigidbody2D rb, Vector2 position)
        {
            if (float.IsNaN(position.x) || float.IsNaN(position.y)) return;
            if (float.IsInfinity(position.x) || float.IsInfinity(position.y)) return;

            rb.MovePosition(position);
        }

        /// <summary>
        /// Enables <see cref="Rigidbody2D.isKinematic"/> and returns the velocity at the
        /// moment of the switch, so it can be restored later via
        /// <see cref="RestoreFromKinematic"/>.
        /// <para>
        /// Use around cutscenes or scripted movement segments where a body should stop
        /// reacting to physics forces but must resume with its prior momentum afterward.
        /// </para>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 SetKinematicAndCacheVelocity(this Rigidbody2D rb)
        {
            Vector2 cached = rb.velocity;
            rb.isKinematic = true;
            return cached;
        }

        /// <summary>
        /// Disables <see cref="Rigidbody2D.isKinematic"/> and reapplies a previously cached
        /// velocity, completing the pairing started by <see cref="SetKinematicAndCacheVelocity"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RestoreFromKinematic(this Rigidbody2D rb, Vector2 cachedVelocity)
        {
            rb.isKinematic = false;
            rb.velocity = cachedVelocity;
        }

        /// <summary>
        /// Rotates the body toward the direction of its current velocity using
        /// <see cref="Rigidbody2D.MoveRotation(float)"/>. <br/>
        /// Standard technique for top-down vehicles and projectiles that must visually
        /// face their movement direction. Does nothing while the body is nearly
        /// stationary, preventing rotation jitter at rest.
        /// <para>
        /// Assumes a rotation of <c>0</c> faces <c>+X</c>; add a sprite offset if your
        /// art is drawn facing <c>+Y</c>.
        /// </para>
        /// </summary>
        public static void FaceVelocityDirection(this Rigidbody2D rb, float rotationSpeedDegrees)
        {
            Vector2 velocity = rb.velocity;
            float sqrSpeed = velocity.sqrMagnitude;
            if (sqrSpeed < LinearRestSqrEpsilon) return;

            float targetAngle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
            float rotated = Mathf.MoveTowardsAngle(rb.rotation, targetAngle, rotationSpeedDegrees * Time.fixedDeltaTime);
            rb.MoveRotation(rotated);
        }

        #endregion

        #region Queries & State

        /// <summary>
        /// True if combined linear and angular motion is below the rest threshold. <br/>
        /// More reliable than checking <see cref="Rigidbody2D.IsSleeping"/> alone, since a
        /// body can be technically awake yet have negligible velocity for several frames
        /// before Box2D puts it to sleep.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsAtRest(this Rigidbody2D rb)
            => rb.velocity.sqrMagnitude < LinearRestSqrEpsilon
            && rb.angularVelocity * rb.angularVelocity < AngularRestSqrEpsilon;

        /// <summary>
        /// True if the body's linear velocity exceeds <paramref name="sqrSpeedThreshold"/>. <br/>
        /// Pass a pre-squared threshold (e.g. <c>walkSpeed * walkSpeed</c>) to drive
        /// animator blend trees without a square root per character per frame.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsMoving(this Rigidbody2D rb, float sqrSpeedThreshold)
            => rb.velocity.sqrMagnitude > sqrSpeedThreshold;

        /// <summary>
        /// True if every component of <see cref="Rigidbody2D.velocity"/> is finite. <br/>
        /// Diagnostic guard for catching corrupted physics state (e.g. after a bad
        /// impulse or network desync) before it propagates into gameplay logic.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasValidVelocity(this Rigidbody2D rb)
        {
            Vector2 v = rb.velocity;
            return !float.IsNaN(v.x) && !float.IsNaN(v.y)
                && !float.IsInfinity(v.x) && !float.IsInfinity(v.y);
        }

        #endregion
    }
}