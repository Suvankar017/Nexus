using System.Runtime.CompilerServices;
using UnityEngine;

namespace Nexus.Core.Extensions
{
    /// <summary>
    /// Extension methods for <see cref="Collider"/> covering safe closest-point queries,
    /// distance checks, layer/tag filtering, and rigidbody access.
    /// <para>Design rules:</para>
    /// <list type="bullet">
    /// <item>All proximity comparisons prefer <c>sqrMagnitude</c> over
    /// <see cref="Vector3.Distance(Vector3, Vector3)"/> to avoid hidden square roots in
    /// hot paths such as target selection and aggro checks.</item>
    /// <item><see cref="Collider.ClosestPoint(Vector3)"/> only supports convex colliders;
    /// calling it on a non-convex <see cref="MeshCollider"/> can throw. <see cref="ClosestPointSafe"/>
    /// guards against this by falling back to <see cref="Collider.ClosestPointOnBounds(Vector3)"/>.</item>
    /// <item>Layer and tag checks avoid string concatenation and boxing to remain safe in
    /// per-frame hit-processing and overlap-result loops.</item>
    /// <item>Zero allocation: no LINQ, no temporary collections.</item>
    /// </list>
    /// </summary>
    public static class ColliderExtensions
    {
        #region Closest Point & Distance

        /// <summary>
        /// Closest point on the collider's surface to <paramref name="point"/>, falling
        /// back to <see cref="Collider.ClosestPointOnBounds(Vector3)"/> if the collider is
        /// a non-convex <see cref="MeshCollider"/>. <br/>
        /// Calling <see cref="Collider.ClosestPoint(Vector3)"/> directly on a non-convex
        /// mesh throws at runtime; this guard keeps AI targeting and cover-point code from
        /// crashing when it encounters level geometry built from concave meshes.
        /// </summary>
        public static Vector3 ClosestPointSafe(this Collider collider, Vector3 point)
        {
            if (collider is MeshCollider meshCollider && !meshCollider.convex)
                return collider.ClosestPointOnBounds(point);

            return collider.ClosestPoint(point);
        }

        /// <summary>
        /// Squared distance from <paramref name="point"/> to the closest point on the
        /// collider's surface. <br/>
        /// Use for target-selection and interaction-range checks — "which nearby collider
        /// is actually closest?" — without paying for a square root per candidate.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SqrDistanceTo(this Collider collider, Vector3 point)
        {
            Vector3 closest = collider.ClosestPointSafe(point);
            return (point - closest).sqrMagnitude;
        }

        /// <summary>
        /// Actual distance from <paramref name="point"/> to the closest point on the
        /// collider's surface. <br/>
        /// Only call when the real distance is needed (UI readouts, footstep volume
        /// falloff); prefer <see cref="SqrDistanceTo"/> for comparisons.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DistanceTo(this Collider collider, Vector3 point)
            => Mathf.Sqrt(collider.SqrDistanceTo(point));

        /// <summary>
        /// True if <paramref name="point"/> lies within <paramref name="range"/> of the
        /// collider's surface. <br/>
        /// Preferred over a raw <see cref="Vector3.Distance(Vector3, Vector3)"/> check
        /// against the transform position because it accounts for the collider's actual
        /// shape and size — important for large enemies or environment pieces where the
        /// pivot is far from the nearest edge.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsWithinRange(this Collider collider, Vector3 point, float range)
            => collider.SqrDistanceTo(point) <= range * range;

        /// <summary>
        /// True if <paramref name="point"/> lies exactly on or inside the collider's
        /// surface (within a small tolerance). <br/>
        /// Since <see cref="Collider.ClosestPoint(Vector3)"/> returns the input point
        /// unchanged when it is already inside the collider, comparing the returned point
        /// to the original is a cheap, allocation-free inside/outside test.
        /// </summary>
        public static bool ContainsPoint(this Collider collider, Vector3 point, float tolerance = 1e-4f)
        {
            Vector3 closest = collider.ClosestPointSafe(point);
            return (point - closest).sqrMagnitude <= tolerance * tolerance;
        }

        #endregion

        #region Bounds & Geometry

        /// <summary>
        /// World-space centre of the collider's axis-aligned bounding box. <br/>
        /// Shorthand for <c>collider.bounds.center</c>, kept for naming consistency with
        /// the rest of the extension library and to make call sites read as intent
        /// ("get world centre") rather than a raw property chain.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 GetWorldCenter(this Collider collider) => collider.bounds.center;

        /// <summary>
        /// Squared distance between the AABBs of two colliders, or <c>0</c> if they
        /// overlap. <br/>
        /// A cheap broad-phase rejection test — for example skipping expensive narrow-phase
        /// checks between distant colliders in a custom spatial query — without the cost
        /// of <see cref="Physics.ComputePenetration"/>.
        /// </summary>
        public static float SqrDistanceToBounds(this Collider collider, Collider other)
        {
            Bounds a = collider.bounds;
            Bounds b = other.bounds;
            Vector3 closest = b.ClosestPoint(a.center);
            Vector3 aClosest = a.ClosestPoint(closest);
            return (closest - aClosest).sqrMagnitude;
        }

        /// <summary>
        /// True if this collider's AABB intersects <paramref name="other"/>'s AABB. <br/>
        /// Fast, approximate overlap test suitable for broad-phase spatial partitioning
        /// before falling back to precise physics queries like
        /// <see cref="Physics.ComputePenetration"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool BoundsIntersects(this Collider collider, Collider other)
            => collider.bounds.Intersects(other.bounds);

        #endregion

        #region Layer & Tag Filtering

        /// <summary>
        /// True if the collider's <see cref="GameObject"/> sits on a layer included in
        /// <paramref name="layerMask"/>. <br/>
        /// Filters <see cref="Physics.OverlapSphereNonAlloc"/> and raycast results without
        /// requiring a second, more restrictive physics query.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsOnLayer(this Collider collider, LayerMask layerMask)
            => (layerMask.value & (1 << collider.gameObject.layer)) != 0;

        /// <summary>
        /// True if the collider carries <paramref name="tag"/>. <br/>
        /// Thin wrapper kept for naming symmetry with <see cref="IsOnLayer"/> so overlap
        /// filtering code reads consistently regardless of which check is used.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasTag(this Collider collider, string tag) => collider.CompareTag(tag);

        #endregion

        #region Rigidbody Access

        /// <summary>
        /// Outputs the <see cref="Rigidbody"/> attached to this collider, if any. <br/>
        /// Consolidates the common "is this a dynamic physics object or static geometry?"
        /// null-check into a single call, avoiding repeated <c>attachedRigidbody != null</c>
        /// checks scattered across combat and interaction code.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetAttachedRigidbody(this Collider collider, out Rigidbody rigidbody)
        {
            rigidbody = collider.attachedRigidbody;
            return rigidbody != null;
        }

        /// <summary>
        /// True if this collider has no attached <see cref="Rigidbody"/>, meaning it acts
        /// as static level geometry to the physics engine. <br/>
        /// Useful for classifying raycast hits into "hit the world" versus "hit a dynamic
        /// object" without an extra layer-mask pass.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsStaticGeometry(this Collider collider) => collider.attachedRigidbody == null;

        #endregion
    }
}