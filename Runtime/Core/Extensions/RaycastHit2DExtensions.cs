using System.Runtime.CompilerServices;
using UnityEngine;

namespace Nexus.Core.Extensions
{
    /// <summary>
    /// Extension methods for <see cref="RaycastHit2D"/> covering validity checks, slope
    /// analysis, and reflection.
    /// <para>Design rules:</para>
    /// <list type="bullet">
    /// <item>Always check <see cref="IsValid"/> first when working with entries from a
    /// <c>RaycastHit2D[]</c> buffer filled by <c>Physics2D.RaycastNonAlloc</c>, since
    /// trailing slots contain default structs with a <c>null</c> collider.</item>
    /// <item>Layer and tag checks avoid string concatenation and boxing to remain safe in
    /// per-frame hit-processing loops.</item>
    /// <item>Slope helpers assume a Y-up 2D world (side-scroller / top-down with vertical
    /// gravity) and are intended for platformer ground checks.</item>
    /// <item>Zero allocation: no LINQ, no temporary collections.</item>
    /// </list>
    /// </summary>
    public static class RaycastHit2DExtensions
    {
        #region Validation

        /// <summary>
        /// True if the hit actually struck a collider. <br/>
        /// Required when iterating a <c>RaycastHit2D[]</c> buffer populated by
        /// <see cref="Physics2D.RaycastNonAlloc"/>, whose unused trailing entries are
        /// default structs with a <c>null</c> <see cref="RaycastHit2D.collider"/> that
        /// would otherwise throw on access.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsValid(this RaycastHit2D hit) => hit.collider != null;

        /// <summary>
        /// True if the struck collider's <see cref="GameObject"/> sits on a layer included
        /// in <paramref name="layerMask"/>. <br/>
        /// Filters combined raycast results (e.g. "hit anything", then narrow to
        /// "hit an enemy") without a second, more restrictive raycast.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsOnLayer(this RaycastHit2D hit, LayerMask layerMask)
            => hit.collider != null && (layerMask.value & (1 << hit.collider.gameObject.layer)) != 0;

        /// <summary>
        /// True if the struck collider carries <paramref name="tag"/>, guarded against a
        /// missing collider. <br/>
        /// Avoids the null-reference exception that a raw <c>hit.collider.CompareTag(...)</c>
        /// throws when called on an unfiltered hit result.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasTag(this RaycastHit2D hit, string tag)
            => hit.collider != null && hit.collider.CompareTag(tag);

        /// <summary>
        /// Outputs the <see cref="Rigidbody2D"/> attached to the struck collider, if any. <br/>
        /// Consolidates the common "did I hit a physics body?" null-check into a single
        /// call instead of repeating <c>hit.rigidbody != null</c> across gameplay code.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetAttachedRigidbody(this RaycastHit2D hit, out Rigidbody2D rigidbody)
        {
            rigidbody = hit.rigidbody;
            return rigidbody != null;
        }

        #endregion

        #region Surface & Slope

        /// <summary>
        /// Angle, in degrees, between the hit surface normal and world up. <br/>
        /// The standard metric for ground-slope checks in 2D platformer controllers:
        /// compare against a maximum walkable angle to distinguish floor from wall.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetSlopeAngle(this RaycastHit2D hit) => Vector2.Angle(hit.normal, Vector2.up);

        /// <summary>
        /// True if the hit surface's slope does not exceed <paramref name="maxSlopeAngle"/>
        /// degrees. <br/>
        /// Drop-in ground-validity check for custom 2D character controllers that need to
        /// reject sheer walls and steep ramps while still allowing gentle slopes.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsWalkableSlope(this RaycastHit2D hit, float maxSlopeAngle)
            => hit.GetSlopeAngle() <= maxSlopeAngle;

        #endregion

        #region Reflection

        /// <summary>
        /// Reflects <paramref name="incomingDirection"/> off the hit surface normal. <br/>
        /// Drives bounce behavior for 2D projectiles and deflected shots without manually
        /// re-deriving the reflection formula at each call site.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 ReflectDirection(this RaycastHit2D hit, Vector2 incomingDirection)
            => Vector2.Reflect(incomingDirection, hit.normal);

        #endregion
    }
}