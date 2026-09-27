using System.Runtime.CompilerServices;
using UnityEngine;

namespace Nexus.Core.Extensions
{
    /// <summary>
    /// Extension methods for <see cref="Collider2D"/> covering closest-point queries,
    /// distance checks, layer/tag filtering, and rigidbody access.
    /// <para>Design rules:</para>
    /// <list type="bullet">
    /// <item>All proximity comparisons prefer <c>sqrMagnitude</c> over
    /// <see cref="Vector2.Distance(Vector2, Vector2)"/> to avoid hidden square roots in
    /// hot paths such as target selection and aggro checks.</item>
    /// <item>Unlike its 3D counterpart, <see cref="Collider2D.ClosestPoint(Vector2)"/> has
    /// no convexity restriction, so no fallback wrapper is required — 2D colliders are
    /// always well-defined for this query.</item>
    /// <item>Layer and tag checks avoid string concatenation and boxing to remain safe in
    /// per-frame hit-processing and overlap-result loops.</item>
    /// <item>Zero allocation: no LINQ, no temporary collections.</item>
    /// </list>
    /// </summary>
    public static class Collider2DExtensions
    {
        #region Closest Point & Distance

        /// <summary>
        /// Squared distance from <paramref name="point"/> to the closest point on the
        /// collider's surface. <br/>
        /// Use for target-selection and interaction-range checks — "which nearby collider
        /// is actually closest?" — without paying for a square root per candidate.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SqrDistanceTo(this Collider2D collider, Vector2 point)
        {
            Vector2 closest = collider.ClosestPoint(point);
            return (point - closest).sqrMagnitude;
        }

        /// <summary>
        /// Actual distance from <paramref name="point"/> to the closest point on the
        /// collider's surface. <br/>
        /// Only call when the real distance is needed (UI readouts, audio falloff);
        /// prefer <see cref="SqrDistanceTo"/> for comparisons.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DistanceTo(this Collider2D collider, Vector2 point)
            => Mathf.Sqrt(collider.SqrDistanceTo(point));

        /// <summary>
        /// True if <paramref name="point"/> lies within <paramref name="range"/> of the
        /// collider's surface. <br/>
        /// Preferred over a raw <see cref="Vector2.Distance(Vector2, Vector2)"/> check
        /// against the transform position because it accounts for the collider's actual
        /// shape and size — important for large platforms or enemies where the pivot is
        /// far from the nearest edge.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsWithinRange(this Collider2D collider, Vector2 point, float range)
            => collider.SqrDistanceTo(point) <= range * range;

        /// <summary>
        /// True if <paramref name="point"/> lies inside the collider's shape. <br/>
        /// Thin wrapper around <see cref="Collider2D.OverlapPoint(Vector2)"/> kept for
        /// naming consistency with the rest of the extension library.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ContainsPoint(this Collider2D collider, Vector2 point)
            => collider.OverlapPoint(point);

        #endregion

        #region Bounds & Geometry

        /// <summary>
        /// World-space centre of the collider's axis-aligned bounding box. <br/>
        /// Shorthand for <c>collider.bounds.center</c> flattened to 2D, kept for naming
        /// consistency with the rest of the extension library.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 GetWorldCenter(this Collider2D collider) => collider.bounds.center;

        /// <summary>
        /// True if this collider's AABB intersects <paramref name="other"/>'s AABB. <br/>
        /// Fast, approximate overlap test suitable for broad-phase spatial partitioning
        /// before falling back to a precise <see cref="Collider2D.IsTouching(Collider2D)"/>
        /// check.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool BoundsIntersects(this Collider2D collider, Collider2D other)
            => collider.bounds.Intersects(other.bounds);

        #endregion

        #region Layer & Tag Filtering

        /// <summary>
        /// True if the collider's <see cref="GameObject"/> sits on a layer included in
        /// <paramref name="layerMask"/>. <br/>
        /// Filters <see cref="Physics2D.OverlapCircleAll"/> and raycast results without
        /// requiring a second, more restrictive physics query.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsOnLayer(this Collider2D collider, LayerMask layerMask)
            => (layerMask.value & (1 << collider.gameObject.layer)) != 0;

        /// <summary>
        /// True if the collider carries <paramref name="tag"/>. <br/>
        /// Thin wrapper kept for naming symmetry with <see cref="IsOnLayer"/> so overlap
        /// filtering code reads consistently regardless of which check is used.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasTag(this Collider2D collider, string tag) => collider.CompareTag(tag);

        #endregion

        #region Rigidbody Access

        /// <summary>
        /// Outputs the <see cref="Rigidbody2D"/> attached to this collider, if any. <br/>
        /// Consolidates the common "is this a dynamic physics object or static geometry?"
        /// null-check into a single call, avoiding repeated <c>attachedRigidbody != null</c>
        /// checks scattered across gameplay code.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetAttachedRigidbody(this Collider2D collider, out Rigidbody2D rigidbody)
        {
            rigidbody = collider.attachedRigidbody;
            return rigidbody != null;
        }

        /// <summary>
        /// True if this collider has no attached <see cref="Rigidbody2D"/>, meaning it
        /// acts as static level geometry to the physics engine. <br/>
        /// Useful for classifying raycast hits into "hit the world" versus "hit a dynamic
        /// object" without an extra layer-mask pass.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsStaticGeometry(this Collider2D collider) => collider.attachedRigidbody == null;

        #endregion
    }
}