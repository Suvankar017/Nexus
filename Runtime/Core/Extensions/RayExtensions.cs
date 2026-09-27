using System.Runtime.CompilerServices;
using UnityEngine;

namespace Nexus.Core.Extensions
{
    /// <summary>
    /// Extension methods for <see cref="Ray"/> covering point sampling, projection,
    /// plane intersection, and safe construction.
    /// <para>Design rules:</para>
    /// <list type="bullet">
    /// <item>All proximity comparisons use <c>sqrMagnitude</c> to avoid hidden square roots.</item>
    /// <item>Plane and ground intersections never propagate <c>NaN</c>; parallel rays or
    /// intersections behind the origin fail safely via <c>Try*</c> patterns.</item>
    /// <item>Projection helpers clamp to <c>t &gt;= 0</c> so results always lie on the ray
    /// itself, never on its backward extension.</item>
    /// <item>Zero allocation: every method operates on value types only.</item>
    /// </list>
    /// </summary>
    public static class RayExtensions
    {
        /// <summary>
        /// Minimum absolute value below which a ray direction component (or a plane/ray
        /// denominator) is treated as parallel. Prevents a divide-by-zero, and the
        /// resulting <c>NaN</c>, when a ray barely grazes a plane or another ray.
        /// </summary>
        private const float ParallelEpsilon = 1e-5f;

        #region Point & Distance

        /// <summary>
        /// Returns the point at <paramref name="distance"/> along the ray, clamped to be
        /// non-negative. <br/>
        /// Prevents accidentally sampling "behind" the ray origin when
        /// <paramref name="distance"/> comes from an untrusted source, such as a network
        /// payload or a designer-authored curve that could dip negative.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 PointAtClamped(this Ray ray, float distance)
            => ray.origin + ray.direction * Mathf.Max(0f, distance);

        /// <summary>
        /// Squared distance from <paramref name="point"/> to the closest point on the ray
        /// (clamped to <c>t &gt;= 0</c>). <br/>
        /// Use for proximity checks — such as "is this object near the player's aim line?" —
        /// without paying for a square root.
        /// </summary>
        public static float SqrDistanceToPoint(this Ray ray, Vector3 point)
        {
            Vector3 closest = ray.ClosestPointOnRay(point);
            return (point - closest).sqrMagnitude;
        }

        /// <summary>
        /// Actual distance from <paramref name="point"/> to the closest point on the ray. <br/>
        /// Only call when the real distance is needed (UI readouts, gizmo labels); prefer
        /// <see cref="SqrDistanceToPoint"/> for comparisons.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DistanceToPoint(this Ray ray, Vector3 point)
            => Mathf.Sqrt(ray.SqrDistanceToPoint(point));

        /// <summary>
        /// Closest point on the ray (clamped to <c>t &gt;= 0</c>) to an arbitrary
        /// <paramref name="point"/>. <br/>
        /// The standard projection formula behind gizmo drawing, aim-assist snapping, and
        /// constraining movement to a fixed rail direction.
        /// </summary>
        public static Vector3 ClosestPointOnRay(this Ray ray, Vector3 point)
        {
            Vector3 originToPoint = point - ray.origin;
            float t = Vector3.Dot(originToPoint, ray.direction);
            return ray.origin + ray.direction * Mathf.Max(0f, t);
        }

        #endregion

        #region Plane Intersection

        /// <summary>
        /// Intersects the ray with an arbitrary <paramref name="plane"/>, returning
        /// <c>false</c> instead of a <c>NaN</c> point when the ray is parallel to the
        /// plane or the intersection lies behind the origin. <br/>
        /// Thin safety wrapper around <see cref="Plane.Raycast"/> that also resolves the
        /// world-space point directly, saving the caller a manual <see cref="Ray.GetPoint"/> call.
        /// </summary>
        public static bool TryIntersectPlane(this Ray ray, Plane plane, out Vector3 point)
        {
            if (plane.Raycast(ray, out float distance))
            {
                point = ray.GetPoint(distance);
                return true;
            }

            point = Vector3.zero;
            return false;
        }

        /// <summary>
        /// Intersects the ray with the horizontal plane at height <paramref name="planeY"/>,
        /// without requiring a <see cref="Plane"/> struct or a physics collider. <br/>
        /// Ideal for mouse-picking on the ground in top-down or strategy games where
        /// placing an invisible plane collider is wasteful. Returns <c>false</c> instead
        /// of a <c>NaN</c> point when the ray is parallel to the plane or the intersection
        /// lies behind the origin.
        /// </summary>
        public static bool TryIntersectHorizontalPlane(this Ray ray, float planeY, out Vector3 point)
        {
            if (Mathf.Abs(ray.direction.y) < ParallelEpsilon)
            {
                point = Vector3.zero;
                return false;
            }

            float t = (planeY - ray.origin.y) / ray.direction.y;
            if (t < 0f)
            {
                point = Vector3.zero;
                return false;
            }

            point = ray.origin + ray.direction * t;
            return true;
        }

        #endregion

        #region Validation & Copies

        /// <summary>
        /// True if the ray's direction is non-zero and finite. <br/>
        /// Guards against propagating a degenerate ray — for example one built from two
        /// identical points — into raycasts or projection math where it would silently
        /// produce <c>NaN</c> results.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsValid(this Ray ray)
        {
            Vector3 d = ray.direction;
            return d.sqrMagnitude > 0f
                && !float.IsNaN(d.x) && !float.IsNaN(d.y) && !float.IsNaN(d.z);
        }

        /// <summary>
        /// Returns a copy of the ray with <see cref="Ray.origin"/> replaced. <br/>
        /// Useful for offsetting a cached aim ray — for example moving it forward past a
        /// weapon muzzle — without disturbing its direction.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Ray WithOrigin(this Ray ray, Vector3 origin) => new Ray(origin, ray.direction);

        /// <summary>
        /// Returns a copy of the ray with <see cref="Ray.direction"/> replaced. <br/>
        /// Useful for re-aiming a ray that originates from a fixed socket, such as a
        /// turret barrel, every frame.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Ray WithDirection(this Ray ray, Vector3 direction) => new Ray(ray.origin, direction);

        #endregion
    }
}