using System.Runtime.CompilerServices;
using UnityEngine;

namespace Nexus.Core.Extensions
{
    /// <summary>
    /// Extension methods for <see cref="RaycastHit"/> covering validity checks, surface
    /// alignment, slope analysis, and reflection.
    /// <para>Design rules:</para>
    /// <list type="bullet">
    /// <item>Always check <see cref="IsValid"/> first when working with entries from a
    /// <c>RaycastHit[]</c> buffer filled by <c>Physics.RaycastNonAlloc</c>, since trailing
    /// slots contain default structs with a <c>null</c> collider.</item>
    /// <item>Layer and tag checks avoid string concatenation and boxing to remain safe in
    /// per-frame hit-processing loops.</item>
    /// <item>Slope and rotation helpers assume a Y-up world and are intended for character
    /// controllers, decal placement, and projectile bounce logic.</item>
    /// <item>Zero allocation: no LINQ, no temporary collections.</item>
    /// </list>
    /// </summary>
    public static class RaycastHitExtensions
    {
        /// <summary>
        /// Squared-length threshold below which a projected forward direction is treated
        /// as degenerate during surface alignment. Guards <see cref="GetSurfaceRotation"/>
        /// against producing a <c>NaN</c> rotation when the hint direction is parallel to
        /// the surface normal.
        /// </summary>
        private const float ProjectionSqrEpsilon = 1e-8f;

        #region Validation

        /// <summary>
        /// True if the hit actually struck a collider. <br/>
        /// Required when iterating a <c>RaycastHit[]</c> buffer populated by
        /// <see cref="Physics.RaycastNonAlloc"/>, whose unused trailing entries are
        /// default structs with a <c>null</c> <see cref="RaycastHit.collider"/> that would
        /// otherwise throw on access.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsValid(this RaycastHit hit) => hit.collider != null;

        /// <summary>
        /// True if the struck collider's <see cref="GameObject"/> sits on a layer included
        /// in <paramref name="layerMask"/>. <br/>
        /// Filters combined raycast results (e.g. "hit anything", then narrow to
        /// "hit an enemy") without a second, more restrictive raycast.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsOnLayer(this RaycastHit hit, LayerMask layerMask)
            => hit.collider != null && (layerMask.value & (1 << hit.collider.gameObject.layer)) != 0;

        /// <summary>
        /// True if the struck collider carries <paramref name="tag"/>, guarded against a
        /// missing collider. <br/>
        /// Avoids the null-reference exception that a raw <c>hit.collider.CompareTag(...)</c>
        /// throws when called on an unfiltered hit result.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasTag(this RaycastHit hit, string tag)
            => hit.collider != null && hit.collider.CompareTag(tag);

        /// <summary>
        /// Outputs the <see cref="Rigidbody"/> attached to the struck collider, if any. <br/>
        /// Consolidates the common "did I hit a physics body?" null-check into a single
        /// call instead of repeating <c>hit.rigidbody != null</c> across combat and
        /// physics-interaction code.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetAttachedRigidbody(this RaycastHit hit, out Rigidbody rigidbody)
        {
            rigidbody = hit.rigidbody;
            return rigidbody != null;
        }

        #endregion

        #region Surface & Slope

        /// <summary>
        /// Angle, in degrees, between the hit surface normal and world up. <br/>
        /// The standard metric for ground-slope checks in character controllers: compare
        /// against a maximum walkable angle to decide whether the surface is a floor or a wall.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetSlopeAngle(this RaycastHit hit) => Vector3.Angle(hit.normal, Vector3.up);

        /// <summary>
        /// True if the hit surface's slope does not exceed <paramref name="maxSlopeAngle"/>
        /// degrees. <br/>
        /// Drop-in ground-validity check for custom character controllers and navigation
        /// systems that need to reject cliffs and steep ramps.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsWalkableSlope(this RaycastHit hit, float maxSlopeAngle)
            => hit.GetSlopeAngle() <= maxSlopeAngle;

        /// <summary>
        /// Builds a rotation whose up axis matches the hit surface normal and whose
        /// forward axis is <paramref name="forwardHint"/> projected onto that surface. <br/>
        /// This is the standard technique for placing decals, footprints, and props flush
        /// against uneven terrain. <br/>
        /// Falls back to an arbitrary perpendicular forward if <paramref name="forwardHint"/>
        /// is parallel to the normal, guaranteeing a valid, <c>NaN</c>-free rotation.
        /// </summary>
        public static Quaternion GetSurfaceRotation(this RaycastHit hit, Vector3 forwardHint)
        {
            Vector3 normal = hit.normal;
            Vector3 projectedForward = Vector3.ProjectOnPlane(forwardHint, normal);

            if (projectedForward.sqrMagnitude < ProjectionSqrEpsilon)
            {
                projectedForward = Vector3.ProjectOnPlane(Vector3.forward, normal);
                if (projectedForward.sqrMagnitude < ProjectionSqrEpsilon)
                    projectedForward = Vector3.ProjectOnPlane(Vector3.right, normal);
            }

            return Quaternion.LookRotation(projectedForward, normal);
        }

        #endregion

        #region Reflection

        /// <summary>
        /// Reflects <paramref name="incomingDirection"/> off the hit surface normal. <br/>
        /// Drives bounce behavior for grenades, arrows, and deflected projectiles without
        /// manually re-deriving the reflection formula at each call site.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 ReflectDirection(this RaycastHit hit, Vector3 incomingDirection)
            => Vector3.Reflect(incomingDirection, hit.normal);

        #endregion
    }
}