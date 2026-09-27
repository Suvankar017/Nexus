using System.Runtime.CompilerServices;
using UnityEngine;

namespace Nexus.Core.Extensions
{
    /// <summary>
    /// Extension methods for <see cref="Camera"/> covering frustum culling, world-space
    /// sizing, screen/viewport queries, and camera-relative movement directions.
    /// <para>Design rules:</para>
    /// <list type="bullet">
    /// <item>Frustum tests use the non-allocating <see cref="GeometryUtility.CalculateFrustumPlanes(Camera, Plane[])"/>
    /// overload; callers supply and reuse a <c>Plane[6]</c> buffer instead of letting Unity
    /// allocate a fresh array every call, which matters when culling hundreds of objects
    /// per frame.</item>
    /// <item>All distance comparisons prefer <c>sqrMagnitude</c> over <see cref="Vector3.Distance(Vector3, Vector3)"/>
    /// to avoid hidden square roots in hot paths (LOD checks, culling, aggro).</item>
    /// <item>Ray/plane and viewport math never propagates <c>NaN</c>; degenerate cases
    /// (parallel rays, zero-length directions) fail safely via <c>Try*</c> or <c>*Or</c>
    /// patterns instead of corrupting downstream transforms.</item>
    /// <item>Horizontal (XZ-plane) direction helpers assume a Y-up world and are intended
    /// for camera-relative character movement, not for aiming or free-fly cameras.</item>
    /// </list>
    /// </summary>
    public static class CameraExtensions
    {
        /// <summary>
        /// Minimum absolute ray-direction component below which a ray is treated as
        /// parallel to a plane. Prevents a divide-by-zero (and resulting <c>NaN</c>)
        /// when raycasting a near-horizontal camera ray against a ground plane.
        /// </summary>
        private const float ParallelEpsilon = 1e-5f;

        /// <summary>
        /// Squared-length threshold below which a flattened direction vector is treated
        /// as degenerate. Guards <see cref="HorizontalForward"/> and <see cref="HorizontalRight"/>
        /// against producing <c>NaN</c> when the camera looks straight up or down.
        /// </summary>
        private const float DirectionSqrEpsilon = 1e-8f;

        #region Frustum & Visibility

        /// <summary>
        /// True if <paramref name="worldPoint"/> falls within the camera's visible viewport
        /// and lies in front of the near plane. <br/>
        /// Cheaper than a full frustum-plane test for single-point checks such as
        /// "is this pickup currently visible to the player?".
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsPointInView(this Camera camera, Vector3 worldPoint)
        {
            Vector3 vp = camera.WorldToViewportPoint(worldPoint);
            return vp.z > 0f && vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f;
        }

        /// <summary>
        /// True if <paramref name="worldPoint"/> falls within the camera's viewport,
        /// expanded or contracted by <paramref name="margin"/> (in viewport units). <br/>
        /// A small positive margin creates a spawn/despawn buffer so objects appear or
        /// disappear slightly off-screen instead of visibly popping at the exact edge.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsPointInView(this Camera camera, Vector3 worldPoint, float margin)
        {
            Vector3 vp = camera.WorldToViewportPoint(worldPoint);
            return vp.z > 0f
                && vp.x >= -margin && vp.x <= 1f + margin
                && vp.y >= -margin && vp.y <= 1f + margin;
        }

        /// <summary>
        /// Fills a caller-owned <c>Plane[6]</c> buffer with the camera's current frustum
        /// planes. <br/>
        /// Reusing one buffer across frames (instead of calling
        /// <see cref="GeometryUtility.CalculateFrustumPlanes(Camera)"/>, which allocates a
        /// new array every call) removes a per-frame GC allocation from culling systems
        /// that test many bounds against the same camera.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CalculateFrustumPlanesNonAlloc(this Camera camera, Plane[] frustumPlaneBuffer)
            => GeometryUtility.CalculateFrustumPlanes(camera, frustumPlaneBuffer);

        /// <summary>
        /// True if <paramref name="bounds"/> intersects the camera's view frustum. <br/>
        /// Pass a reused <c>Plane[6]</c> buffer via <paramref name="frustumPlaneBuffer"/>
        /// to keep large-scale culling passes (e.g. manual occlusion systems, minimap
        /// visibility) completely allocation-free.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsInView(this Camera camera, Bounds bounds, Plane[] frustumPlaneBuffer)
        {
            GeometryUtility.CalculateFrustumPlanes(camera, frustumPlaneBuffer);
            return GeometryUtility.TestPlanesAABB(frustumPlaneBuffer, bounds);
        }

        #endregion

        #region World-Space Sizing

        /// <summary>
        /// Height, in world units, of the camera's visible area at <paramref name="distance"/>
        /// from the lens. <br/>
        /// For a <see cref="Camera.orthographic"/> camera the distance is irrelevant since
        /// the view is a parallel projection. Useful for sizing a background quad or
        /// procedurally fitting a sprite to exactly fill the screen.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float WorldHeightAtDistance(this Camera camera, float distance)
            => camera.orthographic
                ? camera.orthographicSize * 2f
                : 2f * distance * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);

        /// <summary>
        /// Width, in world units, of the camera's visible area at <paramref name="distance"/>
        /// from the lens, derived from <see cref="WorldHeightAtDistance"/> and
        /// <see cref="Camera.aspect"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float WorldWidthAtDistance(this Camera camera, float distance)
            => camera.WorldHeightAtDistance(distance) * camera.aspect;

        /// <summary>
        /// Width and height, in world units, of the camera's visible area at
        /// <paramref name="distance"/> from the lens, combined into a single call to
        /// avoid computing the shared height term twice.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 WorldSizeAtDistance(this Camera camera, float distance)
        {
            float height = camera.WorldHeightAtDistance(distance);
            return new Vector2(height * camera.aspect, height);
        }

        /// <summary>
        /// Sets <see cref="Camera.orthographicSize"/> so the camera's vertical view
        /// exactly spans <paramref name="worldHeight"/> world units. <br/>
        /// The standard way to frame a level bounds or arena in a 2D or top-down game.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FitOrthographicSizeToHeight(this Camera camera, float worldHeight)
            => camera.orthographicSize = worldHeight * 0.5f;

        /// <summary>
        /// Sets <see cref="Camera.orthographicSize"/> so the camera's horizontal view
        /// exactly spans <paramref name="worldWidth"/> world units, accounting for
        /// <see cref="Camera.aspect"/>. <br/>
        /// Use when the width of the play area (not its height) is the framing constraint,
        /// such as fitting a side-scroller's level width on ultra-wide monitors.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FitOrthographicSizeToWidth(this Camera camera, float worldWidth)
            => camera.orthographicSize = worldWidth / (2f * camera.aspect);

        #endregion

        #region Screen & Viewport Queries

        /// <summary>
        /// Centre of this camera's rendered pixel area, using <see cref="Camera.pixelWidth"/>/
        /// <see cref="Camera.pixelHeight"/> rather than <see cref="Screen.width"/>/<see cref="Screen.height"/>. <br/>
        /// Correct for split-screen or picture-in-picture setups where a camera renders
        /// to only part of the screen and the global <see cref="Screen"/> values would be wrong.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 ScreenCenter(this Camera camera)
            => new Vector2(camera.pixelWidth * 0.5f, camera.pixelHeight * 0.5f);

        /// <summary>
        /// Converts <paramref name="worldPoint"/> to screen space and clamps it inside the
        /// camera's pixel rect, inset by <paramref name="margin"/> pixels. <br/>
        /// When the point is behind the camera, the coordinates are mirrored around the
        /// screen centre before clamping so the result swings to the correct edge instead
        /// of freezing on the wrong side. <br/>
        /// This is the standard technique behind off-screen waypoint and enemy-direction
        /// indicators (minimap arrows, objective markers).
        /// </summary>
        public static Vector2 WorldToScreenPointClamped(this Camera camera, Vector3 worldPoint, float margin = 20f)
        {
            Vector3 screenPoint = camera.WorldToScreenPoint(worldPoint);
            if (screenPoint.z < 0f)
            {
                screenPoint.x = camera.pixelWidth - screenPoint.x;
                screenPoint.y = camera.pixelHeight - screenPoint.y;
            }

            screenPoint.x = Mathf.Clamp(screenPoint.x, margin, camera.pixelWidth - margin);
            screenPoint.y = Mathf.Clamp(screenPoint.y, margin, camera.pixelHeight - margin);
            return screenPoint;
        }

        #endregion

        #region Ground & Plane Projection

        /// <summary>
        /// Casts a ray from <paramref name="screenPoint"/> through the camera and
        /// analytically intersects it with the horizontal plane at height
        /// <paramref name="groundY"/>, without requiring a physics collider. <br/>
        /// Ideal for mouse-picking on the ground in top-down or strategy games where
        /// placing an invisible plane collider is wasteful. Returns <c>false</c> instead
        /// of a <c>NaN</c> point when the ray is parallel to the plane or the intersection
        /// lies behind the camera.
        /// </summary>
        public static bool TryScreenPointToGroundPoint(this Camera camera, Vector2 screenPoint, float groundY, out Vector3 worldPoint)
        {
            Ray ray = camera.ScreenPointToRay(screenPoint);
            if (Mathf.Abs(ray.direction.y) < ParallelEpsilon)
            {
                worldPoint = Vector3.zero;
                return false;
            }

            float t = (groundY - ray.origin.y) / ray.direction.y;
            if (t < 0f)
            {
                worldPoint = Vector3.zero;
                return false;
            }

            worldPoint = ray.origin + ray.direction * t;
            return true;
        }

        /// <summary>
        /// Safe wrapper around <see cref="TryScreenPointToGroundPoint"/> that returns
        /// <paramref name="fallback"/> instead of requiring an <c>out</c> parameter. <br/>
        /// Convenient for one-line cursor-to-world lookups in UI or tool code where a
        /// missed raycast should simply keep the previous position.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 ScreenPointToGroundPointOr(this Camera camera, Vector2 screenPoint, float groundY, Vector3 fallback)
            => camera.TryScreenPointToGroundPoint(screenPoint, groundY, out Vector3 result) ? result : fallback;

        #endregion

        #region Camera-Relative Directions

        /// <summary>
        /// The camera's forward direction flattened onto the XZ plane and re-normalized. <br/>
        /// Feeding raw <see cref="Transform.forward"/> into WASD-style movement makes the
        /// character speed up or slow down as the camera pitches; flattening first keeps
        /// horizontal movement speed constant regardless of camera tilt. <br/>
        /// Falls back to <see cref="Vector3.forward"/> if the camera looks straight up or
        /// down, where the flattened vector would otherwise be zero-length.
        /// </summary>
        public static Vector3 HorizontalForward(this Camera camera)
        {
            Vector3 fwd = camera.transform.forward;
            fwd.y = 0f;
            float sqrLen = fwd.sqrMagnitude;
            if (sqrLen < DirectionSqrEpsilon)
            {
                fwd = camera.transform.up;
                fwd.y = 0f;
                sqrLen = fwd.sqrMagnitude;
                if (sqrLen < DirectionSqrEpsilon) return Vector3.forward;
            }

            return fwd * (1f / Mathf.Sqrt(sqrLen));
        }

        /// <summary>
        /// The camera's right direction flattened onto the XZ plane and re-normalized. <br/>
        /// Pairs with <see cref="HorizontalForward"/> to build a camera-relative movement
        /// basis for third-person and top-down controllers. Falls back to
        /// <see cref="Vector3.right"/> in the degenerate case of a heavily rolled camera
        /// whose right vector flattens to zero.
        /// </summary>
        public static Vector3 HorizontalRight(this Camera camera)
        {
            Vector3 right = camera.transform.right;
            right.y = 0f;
            float sqrLen = right.sqrMagnitude;
            if (sqrLen < DirectionSqrEpsilon) return Vector3.right;

            return right * (1f / Mathf.Sqrt(sqrLen));
        }

        #endregion

        #region Distance & Depth

        /// <summary>
        /// Squared Euclidean distance from the camera to <paramref name="point"/>. <br/>
        /// Use for LOD selection, sound attenuation cutoffs, or draw-distance culling
        /// comparisons without paying for a square root.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SqrDistanceTo(this Camera camera, Vector3 point)
        {
            Vector3 d = point - camera.transform.position;
            return d.sqrMagnitude;
        }

        /// <summary>
        /// Actual distance from the camera to <paramref name="point"/>. <br/>
        /// Only call when the real distance is needed (UI readouts, fog density
        /// calculations); prefer <see cref="SqrDistanceTo"/> for comparisons.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DistanceTo(this Camera camera, Vector3 point)
            => Mathf.Sqrt(camera.SqrDistanceTo(point));

        /// <summary>
        /// Signed distance of <paramref name="point"/> along the camera's forward axis,
        /// rather than straight-line Euclidean distance. <br/>
        /// This is view-space depth: the value used internally for transparency sorting
        /// and fog, and the correct metric for "how far into the scene" a point is when
        /// the camera is not looking directly at it.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DepthOf(this Camera camera, Vector3 point)
            => Vector3.Dot(point - camera.transform.position, camera.transform.forward);

        /// <summary>
        /// True if <paramref name="point"/> lies behind the camera's near plane. <br/>
        /// Cheap early-out for culling and screen-space projection code before calling
        /// the more expensive <see cref="Camera.WorldToScreenPoint"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsBehind(this Camera camera, Vector3 point)
            => camera.DepthOf(point) < 0f;

        #endregion
    }
}