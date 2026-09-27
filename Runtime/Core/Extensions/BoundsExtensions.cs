using System.Runtime.CompilerServices;
using UnityEngine;

namespace Nexus.Core.Extensions
{
    /// <summary>
    /// Production-ready extension methods for <see cref="Bounds"/> covering containment,
    /// intersection, camera visibility, random sampling and closest-point queries for
    /// world-space AABB math.
    /// <para>Design rules:</para>
    /// <list type="bullet">
    ///     <item>All comparisons that can be expressed via squared distance avoid <see cref="Mathf.Sqrt(float)"/>; only methods explicitly named <c>Distance</c> incur the real square root.</item>
    ///     <item>Degenerate bounds (zero or negative size, invalid center) are guarded against before division or normalization, never silently propagating <c>NaN</c> into spawn positions or camera framing calculations.</item>
    ///     <item>Camera visibility checks use <see cref="GeometryUtility.CalculateFrustumPlanes(Camera)"/> for full frustum culling (including near/far planes), rather than a naive viewport-only projection which incorrectly reports objects behind the camera as visible.</item>
    ///     <item>Encapsulation and expansion helpers never mutate the input <see cref="Bounds"/> in place; every method returns a new value, consistent with the value-type semantics of <see cref="Bounds"/> itself.</item>
    ///     <item>Zero allocation throughout except the explicit corner-array query, which offers a non-allocating overload alongside the convenience allocating one.</item>
    /// </list>
    /// </summary>
    public static class BoundsExtensions
    {
        #region Constants

        /// <summary>
        /// Minimum volume tolerance used to detect a degenerate (zero-size) bounds before
        /// performing random sampling or normalization, avoiding a division by a
        /// near-zero extent.
        /// </summary>
        private const float MinVolumeEpsilon = 1e-8f;

        #endregion

        #region Validation

        /// <summary>
        /// Returns <c>true</c> if every component of <paramref name="b"/>'s center and extents is
        /// finite.
        /// <br/>
        /// Guards against a bounds corrupted by a failed mesh calculation or degenerate transform
        /// scale from being used in camera framing, spawn placement or physics queries, where a
        /// single <c>NaN</c> extent can silently break an entire culling or overlap test.
        /// </summary>
        /// <param name="b">Bounds to validate.</param>
        /// <returns><c>true</c> if the center and extents are all finite.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsValid(this Bounds b)
            => b.center.IsValid() && b.extents.IsValid();

        /// <summary>
        /// Returns <c>true</c> if <paramref name="b"/> has approximately zero volume.
        /// <br/>
        /// Useful for early-out checks before attempting <see cref="RandomPointInside(Bounds)"/>
        /// or camera-fit framing on a bounds that has not yet been populated by a renderer or
        /// collider (e.g. an empty <see cref="Renderer.bounds"/> before the first frame).
        /// </summary>
        /// <param name="b">Bounds to test.</param>
        /// <returns><c>true</c> if the bounds' volume is approximately zero.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasZeroVolume(this Bounds b)
        {
            Vector3 size = b.size;
            return Mathf.Abs(size.x * size.y * size.z) <= MinVolumeEpsilon;
        }

        #endregion

        #region Containment

        /// <summary>
        /// Returns <c>true</c> if <paramref name="point"/> lies within <paramref name="b"/>,
        /// expanded by <paramref name="padding"/> in every direction.
        /// <br/>
        /// Useful for enlarging a trigger volume's effective containment check beyond its visual
        /// bounds without altering the collider itself — e.g. giving a pickup zone extra tolerance
        /// so fast-moving projectiles are less likely to tunnel past its exact edge.
        /// </summary>
        /// <param name="b">Bounds to test against.</param>
        /// <param name="point">Point to test.</param>
        /// <param name="padding">Extra tolerance added to <paramref name="b"/>'s extents before testing.</param>
        /// <returns><c>true</c> if <paramref name="point"/> is within the padded bounds.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ContainsWithPadding(this Bounds b, Vector3 point, float padding)
        {
            Bounds padded = b;
            padded.Expand(padding * 2f);
            return padded.Contains(point);
        }

        /// <summary>
        /// Returns <c>true</c> if <paramref name="other"/> lies entirely within <paramref name="b"/>.
        /// <br/>
        /// Distinct from <see cref="Bounds.Intersects(Bounds)"/>, which only checks for any
        /// overlap. Useful for validating that a spawned prop's bounds stay fully inside a
        /// designated placement volume, or that a room's contents fit within its walls.
        /// </summary>
        /// <param name="b">Container bounds.</param>
        /// <param name="other">Bounds to test for full containment.</param>
        /// <returns><c>true</c> if <paramref name="other"/> is fully inside <paramref name="b"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ContainsBounds(this Bounds b, Bounds other)
            => b.Contains(other.min) && b.Contains(other.max);

        #endregion

        #region Distance

        /// <summary>
        /// Returns the shortest distance from <paramref name="point"/> to the surface of
        /// <paramref name="b"/>, or <c>0</c> if the point is inside.
        /// <br/>
        /// Named explicitly (not <c>SqrDistance</c>) because this incurs a
        /// <see cref="Mathf.Sqrt(float)"/> call — use only when the real distance value is needed
        /// (UI proximity readouts, audio falloff), and prefer
        /// <see cref="SqrDistanceToPoint(Bounds, Vector3)"/> for threshold comparisons.
        /// </summary>
        /// <param name="b">Bounds to measure against.</param>
        /// <param name="point">Point to measure from.</param>
        /// <returns>The distance from <paramref name="point"/> to <paramref name="b"/>'s nearest surface, or <c>0</c> if inside.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DistanceToPoint(this Bounds b, Vector3 point)
            => Mathf.Sqrt(b.SqrDistance(point));

        /// <summary>
        /// Returns the squared shortest distance from <paramref name="point"/> to the surface of
        /// <paramref name="b"/>, or <c>0</c> if the point is inside.
        /// <br/>
        /// Use for threshold comparisons (is the player within N units of this trigger zone?) to
        /// avoid the <see cref="Mathf.Sqrt(float)"/> cost hidden inside
        /// <see cref="DistanceToPoint(Bounds, Vector3)"/>. Wraps Unity's own
        /// <see cref="Bounds.SqrDistance(Vector3)"/> with a name consistent with this framework's
        /// <c>Sqr*</c> convention.
        /// </summary>
        /// <param name="b">Bounds to measure against.</param>
        /// <param name="point">Point to measure from.</param>
        /// <returns>The squared distance from <paramref name="point"/> to <paramref name="b"/>'s nearest surface, or <c>0</c> if inside.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SqrDistanceToPoint(this Bounds b, Vector3 point)
            => b.SqrDistance(point);

        /// <summary>
        /// Returns the closest point on or inside <paramref name="b"/> to <paramref name="point"/>.
        /// <br/>
        /// Ideal for clamping a projectile impact position or a dragged object's position so it
        /// never leaves a designated bounding volume, and for computing a "look at surface" target
        /// point for a camera framing a large object from outside its bounds.
        /// </summary>
        /// <param name="b">Bounds to clamp against.</param>
        /// <param name="point">Point to clamp.</param>
        /// <returns>The closest point within <paramref name="b"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 ClosestPoint(this Bounds b, Vector3 point)
            => b.ClosestPoint(point);

        #endregion

        #region Expansion & Encapsulation

        /// <summary>
        /// Returns a copy of <paramref name="b"/> expanded outward by <paramref name="amount"/> on
        /// every axis, keeping the same center.
        /// <br/>
        /// Non-mutating alternative to <see cref="Bounds.Expand(float)"/>, which modifies
        /// <paramref name="b"/> in place — useful in expression chains and when the original
        /// bounds must be preserved for a subsequent comparison.
        /// </summary>
        /// <param name="b">Source bounds.</param>
        /// <param name="amount">Total distance added to each axis (split evenly on both sides, matching Unity's own <see cref="Bounds.Expand(float)"/> semantics).</param>
        /// <returns>The expanded bounds.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bounds Expanded(this Bounds b, float amount)
        {
            Bounds result = b;
            result.Expand(amount);
            return result;
        }

        /// <summary>
        /// Returns the smallest bounds that fully contains both <paramref name="b"/> and
        /// <paramref name="other"/>.
        /// <br/>
        /// Non-mutating alternative to <see cref="Bounds.Encapsulate(Bounds)"/>. Ideal for
        /// incrementally building a combined bounding volume around a group of renderers or
        /// colliders (e.g. computing a whole character rig's bounds from its individual limb
        /// meshes) without mutating an accumulator in a loop by reference.
        /// </summary>
        /// <param name="b">First bounds.</param>
        /// <param name="other">Second bounds.</param>
        /// <returns>The bounding volume enclosing both inputs.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bounds Encapsulating(this Bounds b, Bounds other)
        {
            Bounds result = b;
            result.Encapsulate(other);
            return result;
        }

        /// <summary>
        /// Returns the smallest bounds that contains both <paramref name="b"/> and
        /// <paramref name="point"/>.
        /// <br/>
        /// Non-mutating alternative to <see cref="Bounds.Encapsulate(Vector3)"/>, useful when
        /// growing a bounding volume across a sequence of points (patrol path waypoints, particle
        /// positions) in a functional/LINQ-free accumulation loop.
        /// </summary>
        /// <param name="b">Source bounds.</param>
        /// <param name="point">Point to include.</param>
        /// <returns>The grown bounds encompassing both the original bounds and <paramref name="point"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bounds Encapsulating(this Bounds b, Vector3 point)
        {
            Bounds result = b;
            result.Encapsulate(point);
            return result;
        }

        #endregion

        #region Flatten & Horizontal

        /// <summary>
        /// Returns a copy of <paramref name="b"/> flattened onto the XZ ground plane, with its Y
        /// center and extent collapsed to <paramref name="groundY"/> and <c>0</c> respectively.
        /// <br/>
        /// Useful for computing a ground-projected footprint bounds for minimap rendering, shadow
        /// decal sizing or top-down collision approximation, ignoring an object's height entirely.
        /// </summary>
        /// <param name="b">Source bounds to flatten.</param>
        /// <param name="groundY">World Y value to place the flattened bounds at. Defaults to <c>0</c>.</param>
        /// <returns>The flattened bounds.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bounds Flatten(this Bounds b, float groundY = 0f)
        {
            Vector3 center = b.center;
            center.y = groundY;

            Vector3 size = b.size;
            size.y = 0f;

            return new Bounds(center, size);
        }

        /// <summary>
        /// Returns <c>true</c> if <paramref name="point"/> lies within <paramref name="b"/>'s
        /// horizontal (XZ) footprint, ignoring height entirely.
        /// <br/>
        /// Ideal for area-trigger checks in 3D games with vertical traversal (multiple floors,
        /// flying enemies) where a point far above or below the volume should still register as
        /// "inside" for gameplay purposes such as a capture zone.
        /// </summary>
        /// <param name="b">Bounds to test against.</param>
        /// <param name="point">Point to test.</param>
        /// <returns><c>true</c> if <paramref name="point"/> is within the horizontal footprint of <paramref name="b"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ContainsHorizontal(this Bounds b, Vector3 point)
        {
            Vector3 min = b.min;
            Vector3 max = b.max;
            return point.x >= min.x && point.x <= max.x && point.z >= min.z && point.z <= max.z;
        }

        #endregion

        #region Camera Visibility

        /// <summary>
        /// Returns <c>true</c> if any part of <paramref name="b"/> is inside
        /// <paramref name="camera"/>'s view frustum.
        /// <br/>
        /// <para>
        /// Uses <see cref="GeometryUtility.CalculateFrustumPlanes(Camera)"/> and
        /// <see cref="GeometryUtility.TestPlanesAABB(Plane[], Bounds)"/> for a full 6-plane
        /// frustum test, correctly rejecting objects behind the camera or beyond its far clip
        /// plane — a naive <see cref="Camera.WorldToViewportPoint(Vector3)"/> check on the center
        /// alone would incorrectly report large or off-center objects as visible/invisible.
        /// </para>
        /// <para>
        /// This allocates a small frustum-plane array per call; for repeated per-frame checks
        /// against many objects, cache the planes once via <see cref="GeometryUtility.CalculateFrustumPlanes(Camera)"/>
        /// and call <see cref="GeometryUtility.TestPlanesAABB(Plane[], Bounds)"/> directly instead.
        /// </para>
        /// </summary>
        /// <param name="b">Bounds to test.</param>
        /// <param name="camera">Camera whose frustum is tested against.</param>
        /// <returns><c>true</c> if <paramref name="b"/> intersects the camera's frustum.</returns>
        public static bool IsVisibleFrom(this Bounds b, Camera camera)
        {
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(camera);
            return GeometryUtility.TestPlanesAABB(planes, b);
        }

        /// <summary>
        /// Returns <c>true</c> if any part of <paramref name="b"/> is inside
        /// <paramref name="camera"/>'s view frustum.
        /// <br/>
        /// <para>
        /// Uses <see cref="GeometryUtility.CalculateFrustumPlanes(Camera, Plane[])"/> and
        /// <see cref="GeometryUtility.TestPlanesAABB(Plane[], Bounds)"/> for a full 6-plane
        /// frustum test, correctly rejecting objects behind the camera or beyond its far clip
        /// plane — a naive <see cref="Camera.WorldToViewportPoint(Vector3)"/> check on the center
        /// alone would incorrectly report large or off-center objects as visible/invisible.
        /// </para>
        /// <para>
        /// Pre allocates a small frustum-plane array (of length 6); for repeated per-frame checks
        /// against many objects.
        /// </para>
        /// </summary>
        /// <param name="b">Bounds to test.</param>
        /// <param name="camera">Camera whose frustum is tested against.</param>
        /// <param name="planes">View frustum planes array of length 6.</param>
        /// <returns><c>true</c> if <paramref name="b"/> intersects the camera's frustum.</returns>
        public static bool IsVisibleFrom(this Bounds b, Camera camera, Plane[] planes)
        {
            GeometryUtility.CalculateFrustumPlanes(camera, planes);
            return GeometryUtility.TestPlanesAABB(planes, b);
        }

        #endregion

        #region Random Sampling

        /// <summary>
        /// Returns a uniformly random point within <paramref name="b"/>.
        /// <br/>
        /// Ideal for scattering spawn positions within a designated world-space volume (loot
        /// scatter, particle emission region, ambient wildlife spawn zone) without biasing toward
        /// the center or edges.
        /// </summary>
        /// <param name="b">Bounds to sample within.</param>
        /// <returns>A random point inside <paramref name="b"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 RandomPointInside(this Bounds b)
        {
            Vector3 min = b.min;
            Vector3 max = b.max;
            return new Vector3(
                Random.Range(min.x, max.x),
                Random.Range(min.y, max.y),
                Random.Range(min.z, max.z));
        }

        /// <summary>
        /// Returns a uniformly random point on the horizontal (XZ) footprint of
        /// <paramref name="b"/>, at a fixed <paramref name="y"/> height.
        /// <br/>
        /// Standard ground-spawn utility for placing objects on a flat plane within a volume,
        /// avoiding the need to separately sample and discard the Y component.
        /// </summary>
        /// <param name="b">Bounds to sample within.</param>
        /// <param name="y">Fixed world Y value for the returned point.</param>
        /// <returns>A random point on the horizontal footprint of <paramref name="b"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 RandomPointOnGround(this Bounds b, float y)
        {
            Vector3 min = b.min;
            Vector3 max = b.max;
            return new Vector3(Random.Range(min.x, max.x), y, Random.Range(min.z, max.z));
        }

        #endregion

        #region Corners

        /// <summary>
        /// Writes the eight corners of <paramref name="b"/> into <paramref name="results"/>.
        /// <br/>
        /// Non-allocating variant intended for per-frame calls such as rebuilding a wireframe
        /// gizmo or debug-draw outline. The caller must supply an array of length 8.
        /// </summary>
        /// <param name="b">Bounds to extract corners from.</param>
        /// <param name="results">Destination array of length 8.</param>
        public static void GetCornersNonAlloc(this Bounds b, Vector3[] results)
        {
            Vector3 min = b.min;
            Vector3 max = b.max;

            results[0] = new Vector3(min.x, min.y, min.z);
            results[1] = new Vector3(max.x, min.y, min.z);
            results[2] = new Vector3(min.x, max.y, min.z);
            results[3] = new Vector3(max.x, max.y, min.z);
            results[4] = new Vector3(min.x, min.y, max.z);
            results[5] = new Vector3(max.x, min.y, max.z);
            results[6] = new Vector3(min.x, max.y, max.z);
            results[7] = new Vector3(max.x, max.y, max.z);
        }

        /// <summary>
        /// Returns the eight corners of <paramref name="b"/> as a new array.
        /// <br/>
        /// Convenience allocating variant of <see cref="GetCornersNonAlloc(Bounds, Vector3[])"/>
        /// for one-off calls such as editor tooling or gizmo drawing; prefer the non-allocating
        /// overload in per-frame code.
        /// </summary>
        /// <param name="b">Bounds to extract corners from.</param>
        /// <returns>A new 8-element array of corner points.</returns>
        public static Vector3[] GetCorners(this Bounds b)
        {
            Vector3[] results = new Vector3[8];
            b.GetCornersNonAlloc(results);
            return results;
        }

        #endregion

        #region Conversion

        /// <summary>
        /// Returns the world-space bounds converted into a <see cref="Rect"/> on the XZ ground
        /// plane, discarding height.
        /// <br/>
        /// Useful for feeding a world volume into minimap rendering or top-down UI systems that
        /// operate on <see cref="Rect"/> rather than <see cref="Bounds"/>.
        /// </summary>
        /// <param name="b">Bounds to convert.</param>
        /// <returns>A <see cref="Rect"/> spanning <paramref name="b"/>'s X and Z extents.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rect ToXZRect(this Bounds b)
        {
            Vector3 min = b.min;
            Vector3 size = b.size;
            return new Rect(min.x, min.z, size.x, size.z);
        }

        #endregion

        #region Debug

        /// <summary>
        /// Returns a compact debug string describing <paramref name="b"/>'s center and size.
        /// <br/>
        /// Intended for <see cref="Debug.Log(object)"/> calls and runtime inspectors during
        /// spawn-volume or trigger-zone debugging, not hot-path logic, due to string allocation.
        /// </summary>
        /// <param name="b">Bounds to format.</param>
        /// <returns>A formatted debug string.</returns>
        public static string ToDebugString(this Bounds b)
        {
            Vector3 c = b.center;
            Vector3 s = b.size;
            return $"Bounds(Center:({c.x:F2},{c.y:F2},{c.z:F2}), Size:({s.x:F2},{s.y:F2},{s.z:F2}))";
        }

        #endregion
    }
}