using System.Runtime.CompilerServices;
using UnityEngine;

namespace Nexus.Core.Extensions
{
    /// <summary>
    /// Production-ready extension methods for <see cref="Transform"/> covering position/rotation/
    /// scale manipulation, hierarchy traversal, safe look-at logic and world/local space queries.
    /// <para>Design rules:</para>
    /// <list type="bullet">
    ///     <item>Position/rotation/scale mutators are provided in both world-space (<c>position</c>) and local-space (<c>LocalPosition</c>) variants; the naming always makes the space explicit to prevent the classic "set position instead of localPosition on a nested prefab" bug.</item>
    ///     <item>All distance/range checks use squared magnitude; only <c>DistanceTo</c>-named methods call <see cref="Mathf.Sqrt(float)"/>.</item>
    ///     <item>Direction and look-at helpers guard against a zero-length or coincident target, never assigning a <c>NaN</c> rotation to <see cref="Transform.rotation"/>.</item>
    ///     <item>Hierarchy helpers destroy children back-to-front to avoid index shifting during iteration, and are explicit about whether they allocate.</item>
    ///     <item>Assumes Unity's Y-up coordinate system; XZ is treated as the ground plane throughout.</item>
    /// </list>
    /// </summary>
    public static class TransformExtensions
    {
        #region Constants

        /// <summary>
        /// Squared-magnitude tolerance used to detect a degenerate (zero-length) direction before
        /// computing a look rotation, avoiding a <c>NaN</c> result from
        /// <see cref="Quaternion.LookRotation(Vector3)"/>.
        /// </summary>
        private const float SqrEpsilon = 1e-8f;

        #endregion

        #region World Position

        /// <summary>
        /// Returns a copy of <paramref name="t"/>.position with only the X component replaced.
        /// <br/>
        /// Avoids the common three-line pattern of reading, modifying and reassigning
        /// <see cref="Transform.position"/> just to change one axis (e.g. locking an object's
        /// height while updating horizontal position).
        /// </summary>
        /// <param name="t">Transform whose world position is queried.</param>
        /// <param name="x">New world X value.</param>
        /// <returns>The resulting world position after the change is applied.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 WithPositionX(this Transform t, float x)
        {
            Vector3 p = t.position;
            p.x = x;
            t.position = p;
            return p;
        }

        /// <summary>
        /// Returns a copy of <paramref name="t"/>.position with only the Y component replaced.
        /// <br/>
        /// Ideal for pinning an object to a fixed height (ground-snapped UI markers, flat
        /// billboard sprites) while other systems freely modify X and Z.
        /// </summary>
        /// <param name="t">Transform whose world position is queried.</param>
        /// <param name="y">New world Y value.</param>
        /// <returns>The resulting world position after the change is applied.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 WithPositionY(this Transform t, float y)
        {
            Vector3 p = t.position;
            p.y = y;
            t.position = p;
            return p;
        }

        /// <summary>
        /// Returns a copy of <paramref name="t"/>.position with only the Z component replaced.
        /// <br/>
        /// Useful for depth-locking objects on a fixed Z plane in 2.5D or side-scrolling setups
        /// while X/Y remain gameplay-driven.
        /// </summary>
        /// <param name="t">Transform whose world position is queried.</param>
        /// <param name="z">New world Z value.</param>
        /// <returns>The resulting world position after the change is applied.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 WithPositionZ(this Transform t, float z)
        {
            Vector3 p = t.position;
            p.z = z;
            t.position = p;
            return p;
        }

        /// <summary>
        /// Offsets <paramref name="t"/>.position by <paramref name="offset"/> in world space.
        /// <br/>
        /// Reads more clearly than <c>t.position += offset</c> in fluent call chains and keeps
        /// intent explicit at call sites doing manual, non-physics translation (cutscene rigs,
        /// editor tools, procedural placement).
        /// </summary>
        /// <param name="t">Transform to move.</param>
        /// <param name="offset">World-space offset to add.</param>
        /// <returns>The resulting world position after the offset is applied.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 AddPosition(this Transform t, Vector3 offset)
        {
            Vector3 p = t.position + offset;
            t.position = p;
            return p;
        }

        #endregion

        #region Local Position

        /// <summary>
        /// Returns a copy of <paramref name="t"/>.localPosition with only the X component replaced.
        /// <br/>
        /// Explicitly named to prevent the frequent mistake of mutating world <c>position</c>
        /// when the intent was to adjust an object's offset within its parent (e.g. a weapon
        /// socket attachment).
        /// </summary>
        /// <param name="t">Transform whose local position is queried.</param>
        /// <param name="x">New local X value.</param>
        /// <returns>The resulting local position after the change is applied.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 WithLocalPositionX(this Transform t, float x)
        {
            Vector3 p = t.localPosition;
            p.x = x;
            t.localPosition = p;
            return p;
        }

        /// <summary>
        /// Returns a copy of <paramref name="t"/>.localPosition with only the Y component replaced.
        /// <br/>
        /// Ideal for adjusting a UI element's vertical offset within a layout parent without
        /// disturbing its horizontal placement.
        /// </summary>
        /// <param name="t">Transform whose local position is queried.</param>
        /// <param name="y">New local Y value.</param>
        /// <returns>The resulting local position after the change is applied.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 WithLocalPositionY(this Transform t, float y)
        {
            Vector3 p = t.localPosition;
            p.y = y;
            t.localPosition = p;
            return p;
        }

        /// <summary>
        /// Returns a copy of <paramref name="t"/>.localPosition with only the Z component replaced.
        /// <br/>
        /// Useful for adjusting a nested object's forward offset relative to its parent, such as
        /// tuning a camera rig's local dolly distance.
        /// </summary>
        /// <param name="t">Transform whose local position is queried.</param>
        /// <param name="z">New local Z value.</param>
        /// <returns>The resulting local position after the change is applied.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 WithLocalPositionZ(this Transform t, float z)
        {
            Vector3 p = t.localPosition;
            p.z = z;
            t.localPosition = p;
            return p;
        }

        /// <summary>
        /// Offsets <paramref name="t"/>.localPosition by <paramref name="offset"/> in local space.
        /// <br/>
        /// Ideal for procedural jitter, recoil offsets or spring-arm adjustments applied relative
        /// to a parent rig rather than world space.
        /// </summary>
        /// <param name="t">Transform to move.</param>
        /// <param name="offset">Local-space offset to add.</param>
        /// <returns>The resulting local position after the offset is applied.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 AddLocalPosition(this Transform t, Vector3 offset)
        {
            Vector3 p = t.localPosition + offset;
            t.localPosition = p;
            return p;
        }

        #endregion

        #region Rotation

        /// <summary>
        /// Rotates <paramref name="t"/> by <paramref name="eulerOffset"/> degrees in world space,
        /// added on top of its current rotation.
        /// <br/>
        /// Explicit alternative to <see cref="Transform.Rotate(Vector3)"/> that returns the
        /// resulting rotation for fluent chaining or immediate use (e.g. feeding a newly rotated
        /// value into a network sync packet).
        /// </summary>
        /// <param name="t">Transform to rotate.</param>
        /// <param name="eulerOffset">Euler angle offset, in degrees, applied in world space.</param>
        /// <returns>The resulting world rotation after the offset is applied.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Quaternion AddEulerAngles(this Transform t, Vector3 eulerOffset)
        {
            t.Rotate(eulerOffset, Space.World);
            return t.rotation;
        }

        /// <summary>
        /// Rotates <paramref name="t"/> by <paramref name="eulerOffset"/> degrees in local space,
        /// added on top of its current local rotation.
        /// <br/>
        /// Ideal for spinning an attached part (turret barrel, propeller, wheel) relative to its
        /// own parent regardless of the parent's world orientation.
        /// </summary>
        /// <param name="t">Transform to rotate.</param>
        /// <param name="eulerOffset">Euler angle offset, in degrees, applied in local space.</param>
        /// <returns>The resulting local rotation after the offset is applied.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Quaternion AddLocalEulerAngles(this Transform t, Vector3 eulerOffset)
        {
            t.Rotate(eulerOffset, Space.Self);
            return t.localRotation;
        }

        /// <summary>
        /// Rotates <paramref name="t"/> to face <paramref name="target"/>'s world position, using
        /// <see cref="Vector3.up"/> as the up reference. No-ops if <paramref name="target"/>
        /// coincides with <paramref name="t"/>'s position.
        /// <br/>
        /// Guards against the zero-direction case where a target overlaps the source exactly
        /// (e.g. a projectile spawned directly on its target), which would otherwise assign a
        /// <c>NaN</c> rotation via <see cref="Quaternion.LookRotation(Vector3)"/>.
        /// </summary>
        /// <param name="t">Transform to rotate.</param>
        /// <param name="target">World-space position to face.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LookAtSafe(this Transform t, Vector3 target)
            => t.LookAtSafe(target, Vector3.up);

        /// <summary>
        /// Rotates <paramref name="t"/> to face <paramref name="target"/>'s world position, using
        /// <paramref name="up"/> as the up reference. No-ops if <paramref name="target"/>
        /// coincides with <paramref name="t"/>'s position.
        /// <br/>
        /// Standard safe replacement for <see cref="Transform.LookAt(Vector3, Vector3)"/> in AI
        /// facing and turret-tracking code, where the target position is computed at runtime and
        /// may momentarily equal the source position.
        /// </summary>
        /// <param name="t">Transform to rotate.</param>
        /// <param name="target">World-space position to face.</param>
        /// <param name="up">Reference up vector.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LookAtSafe(this Transform t, Vector3 target, Vector3 up)
        {
            Vector3 direction = target - t.position;
            if (direction.sqrMagnitude < SqrEpsilon) return;

            t.rotation = Quaternion.LookRotation(direction, up);
        }

        /// <summary>
        /// Rotates <paramref name="t"/> to face <paramref name="target"/>'s world position while
        /// ignoring height, keeping <paramref name="t"/> upright on the XZ plane. No-ops if the
        /// horizontal projection of the direction is degenerate.
        /// <br/>
        /// The standard choice for grounded character/enemy facing, preventing a target above or
        /// below eye level from tilting the character's body forward or backward.
        /// </summary>
        /// <param name="t">Transform to rotate.</param>
        /// <param name="target">World-space position to face.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LookAtHorizontal(this Transform t, Vector3 target)
        {
            Vector3 direction = target - t.position;
            direction.y = 0f;

            if (direction.sqrMagnitude < SqrEpsilon) return;

            t.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }

        #endregion

        #region Reset

        /// <summary>
        /// Resets <paramref name="t"/>.localPosition to <see cref="Vector3.zero"/>.
        /// <br/>
        /// Common when re-parenting pooled objects: a stale local offset from a previous
        /// attachment point otherwise carries over and misplaces the object under its new parent.
        /// </summary>
        /// <param name="t">Transform to reset.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ResetLocalPosition(this Transform t)
            => t.localPosition = Vector3.zero;

        /// <summary>
        /// Resets <paramref name="t"/>.localRotation to <see cref="Quaternion.identity"/>.
        /// <br/>
        /// Prevents leftover rotation from a previous animation or ragdoll state from persisting
        /// after an object is returned to a pool and reused.
        /// </summary>
        /// <param name="t">Transform to reset.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ResetLocalRotation(this Transform t)
            => t.localRotation = Quaternion.identity;

        /// <summary>
        /// Resets <paramref name="t"/>.localScale to <see cref="Vector3.one"/>.
        /// <br/>
        /// Undoes a temporary squash/stretch tween (hit reactions, UI pop-in) that may not have
        /// completed cleanly before the object was reused or reparented.
        /// </summary>
        /// <param name="t">Transform to reset.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ResetLocalScale(this Transform t)
            => t.localScale = Vector3.one;

        /// <summary>
        /// Resets <paramref name="t"/>'s local position, rotation and scale to their identity
        /// values in a single call.
        /// <br/>
        /// The standard "restore to prefab default local transform" operation used when returning
        /// an object to a pool, ensuring no leftover offset, tilt or scale tween state leaks into
        /// its next use.
        /// </summary>
        /// <param name="t">Transform to reset.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ResetLocal(this Transform t)
        {
            t.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            t.localScale = Vector3.one;
        }

        #endregion

        #region Distance & Range

        /// <summary>
        /// Returns the squared distance between <paramref name="t"/> and <paramref name="other"/>'s
        /// world positions.
        /// <br/>
        /// Use for proximity comparisons (aggro range, trigger radius checks) to avoid the
        /// <see cref="Mathf.Sqrt(float)"/> cost hidden inside <see cref="DistanceTo(Transform, Transform)"/>.
        /// </summary>
        /// <param name="t">Source transform.</param>
        /// <param name="other">Target transform.</param>
        /// <returns>Squared distance between the two positions.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SqrDistanceTo(this Transform t, Transform other)
            => (other.position - t.position).sqrMagnitude;

        /// <summary>
        /// Returns the actual distance between <paramref name="t"/> and <paramref name="other"/>'s
        /// world positions.
        /// <br/>
        /// Named explicitly (not <c>SqrDistanceTo</c>) because this incurs a
        /// <see cref="Mathf.Sqrt(float)"/> call — use only when the real distance value is needed
        /// for display or a calculation that cannot work with squared units (e.g. driving a UI
        /// distance label).
        /// </summary>
        /// <param name="t">Source transform.</param>
        /// <param name="other">Target transform.</param>
        /// <returns>Distance between the two positions.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DistanceTo(this Transform t, Transform other)
            => Vector3.Distance(t.position, other.position);

        /// <summary>
        /// Returns the squared distance between <paramref name="t"/> and <paramref name="other"/>'s
        /// world positions on the XZ ground plane only, ignoring height.
        /// <br/>
        /// Ideal for aggro-range and proximity checks in 3D games where a target flying overhead
        /// or standing on a raised platform should not count as "farther away" for gameplay
        /// purposes.
        /// </summary>
        /// <param name="t">Source transform.</param>
        /// <param name="other">Target transform.</param>
        /// <returns>Squared horizontal distance between the two positions.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SqrHorizontalDistanceTo(this Transform t, Transform other)
        {
            float dx = other.position.x - t.position.x;
            float dz = other.position.z - t.position.z;
            return dx * dx + dz * dz;
        }

        /// <summary>
        /// Returns <c>true</c> if <paramref name="other"/> is within <paramref name="range"/> of
        /// <paramref name="t"/> in full 3D space.
        /// <br/>
        /// Uses squared-distance comparison internally to avoid a <see cref="Mathf.Sqrt(float)"/>
        /// call, making it safe to call every frame for many simultaneous agents (crowd aggro
        /// checks, area-of-effect targeting).
        /// </summary>
        /// <param name="t">Source transform.</param>
        /// <param name="other">Target transform.</param>
        /// <param name="range">Maximum allowed distance.</param>
        /// <returns><c>true</c> if within range.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsInRangeOf(this Transform t, Transform other, float range)
            => t.SqrDistanceTo(other) <= range * range;

        /// <summary>
        /// Returns <c>true</c> if <paramref name="other"/> is inside a cylinder centered on
        /// <paramref name="t"/>.
        /// <br/>
        /// Models melee reach, interaction volumes and area-of-effect zones far better than a
        /// sphere in a game with distinct floors/platforms, since height is checked independently
        /// from horizontal radius.
        /// </summary>
        /// <param name="t">Source transform, defining the cylinder's center.</param>
        /// <param name="other">Target transform to test.</param>
        /// <param name="radius">Horizontal radius of the cylinder.</param>
        /// <param name="halfHeight">Half-height of the cylinder along the Y axis.</param>
        /// <returns><c>true</c> if <paramref name="other"/> is within the cylinder.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsInCylinderOf(this Transform t, Transform other, float radius, float halfHeight)
            => t.SqrHorizontalDistanceTo(other) <= radius * radius
            && Mathf.Abs(other.position.y - t.position.y) <= halfHeight;

        #endregion

        #region Direction

        /// <summary>
        /// Returns the normalized world-space direction from <paramref name="t"/> to
        /// <paramref name="other"/>, or <see cref="Vector3.zero"/> if the two positions coincide.
        /// <br/>
        /// Guards <see cref="Vector3.normalized"/> against a zero-length input, preventing a
        /// <c>NaN</c> direction from propagating into steering, aim or velocity calculations when
        /// a target briefly overlaps the source.
        /// </summary>
        /// <param name="t">Source transform.</param>
        /// <param name="other">Target transform.</param>
        /// <returns>Unit-length direction, or <see cref="Vector3.zero"/> on coincident positions.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 DirectionTo(this Transform t, Transform other)
        {
            Vector3 delta = other.position - t.position;
            float sqrMag = delta.sqrMagnitude;
            return sqrMag < SqrEpsilon ? Vector3.zero : delta / Mathf.Sqrt(sqrMag);
        }

        /// <summary>
        /// Returns the normalized world-space direction from <paramref name="t"/> to
        /// <paramref name="other"/> projected onto the XZ ground plane, or
        /// <see cref="Vector3.zero"/> if the horizontal projection is degenerate.
        /// <br/>
        /// Standard input for grounded AI steering and turret yaw tracking, where vertical
        /// separation between source and target should not affect the horizontal facing direction.
        /// </summary>
        /// <param name="t">Source transform.</param>
        /// <param name="other">Target transform.</param>
        /// <returns>Unit-length horizontal direction, or <see cref="Vector3.zero"/> on degenerate input.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 HorizontalDirectionTo(this Transform t, Transform other)
        {
            Vector3 delta = other.position - t.position;
            delta.y = 0f;

            float sqrMag = delta.sqrMagnitude;
            return sqrMag < SqrEpsilon ? Vector3.zero : delta / Mathf.Sqrt(sqrMag);
        }

        /// <summary>
        /// Returns <c>true</c> if <paramref name="other"/> lies within <paramref name="t"/>'s
        /// forward field of view, measured as a half-angle in degrees.
        /// <br/>
        /// Avoids the trigonometric cost of computing the real angle via
        /// <see cref="Vector3.Angle(Vector3, Vector3)"/> by comparing the dot product of unit
        /// vectors directly against a precomputed cosine threshold — ideal for per-frame vision
        /// cone checks across many AI agents.
        /// </summary>
        /// <param name="t">Observer transform, whose forward direction defines the cone axis.</param>
        /// <param name="other">Target transform to test.</param>
        /// <param name="halfAngleDegrees">Half-angle of the vision cone, in degrees.</param>
        /// <returns><c>true</c> if <paramref name="other"/> is inside the cone.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsInFieldOfView(this Transform t, Transform other, float halfAngleDegrees)
        {
            Vector3 direction = t.DirectionTo(other);
            if (direction == Vector3.zero) return false;

            float dot = Vector3.Dot(t.forward, direction);
            float cosHalfAngle = Mathf.Cos(halfAngleDegrees * Mathf.Deg2Rad);
            return dot >= cosHalfAngle;
        }

        #endregion

        #region Hierarchy

        /// <summary>
        /// Destroys every child of <paramref name="t"/>, using the play-mode-aware destruction
        /// logic from <see cref="ObjectExtensions.DestroySafe(UnityEngine.Object, bool)"/>.
        /// <br/>
        /// Iterates back-to-front so that destroying a child does not shift the indices of
        /// children yet to be processed — iterating front-to-back with
        /// <see cref="Transform.GetChild(int)"/> while destroying is a common source of
        /// skipped-child bugs.
        /// </summary>
        /// <param name="t">Parent transform whose children are destroyed.</param>
        public static void DestroyAllChildren(this Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
                t.GetChild(i).gameObject.DestroySafe(allowDestroyingAssets: false);
        }

        /// <summary>
        /// Sets the active state of every immediate child of <paramref name="t"/>.
        /// <br/>
        /// Common for toggling an entire set of UI options, weapon attachment slots or spawned
        /// pooled effects in one call without manually looping at each call site.
        /// </summary>
        /// <param name="t">Parent transform whose direct children are toggled.</param>
        /// <param name="active">Desired active state for all children.</param>
        public static void SetChildrenActive(this Transform t, bool active)
        {
            for (int i = 0; i < t.childCount; i++)
                t.GetChild(i).gameObject.SetActive(active);
        }

        /// <summary>
        /// Returns the immediate child of <paramref name="t"/> with the given
        /// <paramref name="name"/>, or null if none matches.
        /// <br/>
        /// More explicit than <see cref="Transform.Find(string)"/> for a single-level search when
        /// the intent is specifically "direct child", not a slash-separated hierarchy path.
        /// </summary>
        /// <param name="t">Parent transform to search.</param>
        /// <param name="name">Name of the child to find.</param>
        /// <returns>The matching child transform, or null if not found.</returns>
        public static Transform FindDirectChild(this Transform t, string name)
        {
            for (int i = 0; i < t.childCount; i++)
            {
                Transform child = t.GetChild(i);
                if (child.name == name)
                    return child;
            }

            return null;
        }

        /// <summary>
        /// Returns the first descendant of <paramref name="t"/> with the given
        /// <paramref name="name"/>, searching depth-first through the entire hierarchy.
        /// <br/>
        /// Useful for locating a deeply nested socket or bone (e.g. "WeaponSocket_R") on a
        /// procedurally instantiated rig without needing to know or maintain the exact path.
        /// </summary>
        /// <param name="t">Root transform to search from.</param>
        /// <param name="name">Name of the descendant to find.</param>
        /// <returns>The matching descendant transform, or null if not found.</returns>
        public static Transform FindChildRecursive(this Transform t, string name)
        {
            for (int i = 0; i < t.childCount; i++)
            {
                Transform child = t.GetChild(i);
                if (child.name == name)
                    return child;

                Transform found = child.FindChildRecursive(name);
                if (found != null)
                    return found;
            }

            return null;
        }

        /// <summary>
        /// Returns an array containing every immediate child of <paramref name="t"/>.
        /// <br/>
        /// Snapshotting children into an array is required before performing destructive
        /// operations (reparenting, destroying) on them, since iterating
        /// <see cref="Transform.GetChild(int)"/> live while mutating the hierarchy produces
        /// unreliable results. Allocates one array by necessity; avoid calling every frame.
        /// </summary>
        /// <param name="t">Parent transform whose children are collected.</param>
        /// <returns>An array of direct child transforms, in hierarchy order.</returns>
        public static Transform[] GetChildren(this Transform t)
        {
            int count = t.childCount;
            Transform[] children = new Transform[count];

            for (int i = 0; i < count; i++)
                children[i] = t.GetChild(i);

            return children;
        }

        /// <summary>
        /// Sets the parent of <paramref name="t"/> to <paramref name="parent"/> while preserving
        /// its current world-space position, rotation and scale.
        /// <br/>
        /// Explicit, readable alternative to <see cref="Transform.SetParent(Transform, bool)"/>
        /// with <c>worldPositionStays: true</c> — useful when re-attaching a dropped item or
        /// detached limb to a new socket without it visually snapping to a different transform.
        /// </summary>
        /// <param name="t">Transform to reparent.</param>
        /// <param name="parent">New parent transform, or null to unparent.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetParentKeepWorld(this Transform t, Transform parent)
            => t.SetParent(parent, true);

        /// <summary>
        /// Sets the parent of <paramref name="t"/> to <paramref name="parent"/> and resets its
        /// local position, rotation and scale to identity in the new parent's space.
        /// <br/>
        /// The standard socket-attachment pattern (equipping a weapon, holstering an item) where
        /// the object should snap exactly to the new parent's origin rather than preserve its
        /// previous world transform.
        /// </summary>
        /// <param name="t">Transform to reparent.</param>
        /// <param name="parent">New parent transform.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetParentAndReset(this Transform t, Transform parent)
        {
            t.SetParent(parent, false);
            t.ResetLocal();
        }

        #endregion

        #region Flatten & Snap

        /// <summary>
        /// Returns <paramref name="t"/>'s world position projected onto the XZ ground plane at
        /// height <paramref name="groundY"/>.
        /// <br/>
        /// Ideal for computing a ground-space marker (minimap icon, shadow decal position) for an
        /// airborne or elevated object without mutating the transform itself.
        /// </summary>
        /// <param name="t">Transform whose position is flattened.</param>
        /// <param name="groundY">World Y value to place the flattened position at. Defaults to <c>0</c>.</param>
        /// <returns>The flattened world position.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 GetFlattenedPosition(this Transform t, float groundY = 0f)
        {
            Vector3 p = t.position;
            p.y = groundY;
            return p;
        }

        /// <summary>
        /// Snaps <paramref name="t"/>'s world position to the nearest multiple of
        /// <paramref name="gridSize"/> on all three axes.
        /// <br/>
        /// Standard grid-based building placement utility for construction/crafting systems where
        /// objects must align to a fixed world grid.
        /// </summary>
        /// <param name="t">Transform to snap.</param>
        /// <param name="gridSize">Grid spacing. Values &lt;= 0 disable snapping on that call.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SnapPosition(this Transform t, float gridSize)
        {
            Vector3 p = t.position;
            t.position = new Vector3(p.x.SnapTo(gridSize), p.y.SnapTo(gridSize), p.z.SnapTo(gridSize));
        }

        #endregion

        #region Debug

        /// <summary>
        /// Returns a formatted debug string describing <paramref name="t"/>'s hierarchy path,
        /// world position and world rotation.
        /// <br/>
        /// More informative than logging <see cref="Transform.position"/> alone when diagnosing
        /// placement bugs across many similarly-named pooled instances. Allocates a string and is
        /// intended for logs/inspectors, not hot paths.
        /// </summary>
        /// <param name="t">Transform to describe.</param>
        /// <returns>A formatted debug string.</returns>
        public static string ToTransformDebugString(this Transform t)
        {
            if (t.IsUnityNull())
                return "<null Transform>";

            Vector3 p = t.position;
            Vector3 e = t.eulerAngles;
            return $"{t.GetHierarchyPath()} Pos({p.x:F2}, {p.y:F2}, {p.z:F2}) Rot({e.x:F1}, {e.y:F1}, {e.z:F1})";
        }

        #endregion
    }
}