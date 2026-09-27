using System.Runtime.CompilerServices;
using UnityEngine;

namespace Nexus.Core.Extensions
{
    /// <summary>
    /// Extension methods for <see cref="Ray2D"/> covering point sampling, projection,
    /// line intersection, and safe construction.
    /// <para>Design rules:</para>
    /// <list type="bullet">
    /// <item>All proximity comparisons use <c>sqrMagnitude</c> to avoid hidden square roots.</item>
    /// <item>Line-intersection math never propagates <c>NaN</c>; parallel rays fail safely
    /// via <c>Try*</c> patterns.</item>
    /// <item>Projection helpers clamp to <c>t &gt;= 0</c> so results always lie on the ray
    /// itself, never on its backward extension.</item>
    /// <item>Zero allocation: every method operates on value types only.</item>
    /// </list>
    /// </summary>
    public static class Ray2DExtensions
    {
        /// <summary>
        /// Minimum absolute determinant below which two rays are treated as parallel.
        /// Prevents a divide-by-zero, and the resulting <c>NaN</c>, when two aim or edge
        /// rays are nearly collinear.
        /// </summary>
        private const float ParallelEpsilon = 1e-5f;

        #region Point & Distance

        /// <summary>
        /// Returns the point at <paramref name="distance"/> along the ray, clamped to be
        /// non-negative. <br/>
        /// Prevents accidentally sampling "behind" the ray origin when
        /// <paramref name="distance"/> comes from an untrusted source such as a physics
        /// query result or user input.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 PointAtClamped(this Ray2D ray, float distance)
            => ray.origin + ray.direction * Mathf.Max(0f, distance);

        /// <summary>
        /// Squared distance from <paramref name="point"/> to the closest point on the ray
        /// (clamped to <c>t &gt;= 0</c>). <br/>
        /// Use for proximity checks — such as "is the cursor near this aim line?" — without
        /// paying for a square root.
        /// </summary>
        public static float SqrDistanceToPoint(this Ray2D ray, Vector2 point)
        {
            Vector2 closest = ray.ClosestPointOnRay(point);
            return (point - closest).sqrMagnitude;
        }

        /// <summary>
        /// Actual distance from <paramref name="point"/> to the closest point on the ray. <br/>
        /// Only call when the real distance is needed; prefer <see cref="SqrDistanceToPoint"/>
        /// for comparisons.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DistanceToPoint(this Ray2D ray, Vector2 point)
            => Mathf.Sqrt(ray.SqrDistanceToPoint(point));

        /// <summary>
        /// Closest point on the ray (clamped to <c>t &gt;= 0</c>) to an arbitrary
        /// <paramref name="point"/>. <br/>
        /// The standard projection formula behind gizmo drawing and 2D aim-assist snapping.
        /// </summary>
        public static Vector2 ClosestPointOnRay(this Ray2D ray, Vector2 point)
        {
            Vector2 originToPoint = point - ray.origin;
            float t = Vector2.Dot(originToPoint, ray.direction);
            return ray.origin + ray.direction * Mathf.Max(0f, t);
        }

        #endregion

        #region Line Intersection

        /// <summary>
        /// Intersects the infinite line defined by this ray with the infinite line defined
        /// by <paramref name="other"/>. <br/>
        /// Returns <c>false</c> instead of a <c>NaN</c> point when the two rays are
        /// parallel. <br/>
        /// <para>
        /// The result is <b>not</b> clamped to either ray's positive direction — it is a
        /// line/line intersection, suitable for splitting 2D polygon edges or resolving
        /// crosshair/wall-trace geometry where the intersection may lie behind one origin.
        /// </para>
        /// </summary>
        public static bool TryIntersectLine(this Ray2D ray, Ray2D other, out Vector2 point)
        {
            Vector2 d1 = ray.direction;
            Vector2 d2 = other.direction;
            float denom = d1.x * d2.y - d1.y * d2.x;

            if (Mathf.Abs(denom) < ParallelEpsilon)
            {
                point = Vector2.zero;
                return false;
            }

            Vector2 originDiff = other.origin - ray.origin;
            float t = (originDiff.x * d2.y - originDiff.y * d2.x) / denom;
            point = ray.origin + d1 * t;
            return true;
        }

        #endregion

        #region Validation & Copies

        /// <summary>
        /// True if the ray's direction is non-zero and finite. <br/>
        /// Guards against propagating a degenerate ray into raycasts or projection math
        /// where it would silently produce <c>NaN</c> results.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsValid(this Ray2D ray)
        {
            Vector2 d = ray.direction;
            return d.sqrMagnitude > 0f && !float.IsNaN(d.x) && !float.IsNaN(d.y);
        }

        /// <summary>
        /// Returns a copy of the ray with <see cref="Ray2D.origin"/> replaced. <br/>
        /// Useful for offsetting a cached aim ray without disturbing its direction.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Ray2D WithOrigin(this Ray2D ray, Vector2 origin) => new Ray2D(origin, ray.direction);

        /// <summary>
        /// Returns a copy of the ray with <see cref="Ray2D.direction"/> replaced. <br/>
        /// Useful for re-aiming a ray from a fixed 2D socket, such as a turret pivot, every frame.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Ray2D WithDirection(this Ray2D ray, Vector2 direction) => new Ray2D(ray.origin, direction);

        #endregion
    }
}