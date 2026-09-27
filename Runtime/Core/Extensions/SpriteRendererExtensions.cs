using System.Collections;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Nexus.Core.Extensions
{
    /// <summary>
    /// Unified extension methods for <see cref="SpriteRenderer"/>.
    ///
    /// Coverage:
    /// - Color/tint and alpha control
    /// - Visibility and renderer state
    /// - Horizontal/vertical flipping and facing
    /// - Sorting layer/order helpers
    /// - Sprite assignment and pool reset helpers
    /// - World bounds, size, distance, containment and overlap queries
    /// - Pixel-grid snapping and bottom-edge alignment
    /// - Coroutine-driven fades and flash transitions
    ///
    /// Design rules:
    /// - Reject NaN/infinite values at public color/animation boundaries where they can
    ///   otherwise poison renderer state or create infinite coroutine loops.
    /// - Clamp normalized color/alpha inputs to 0..1.
    /// - Preserve current facing when directional input is effectively zero.
    /// - Prefer sortingLayerID for hot-path sorting-layer changes.
    /// - Prefer squared-distance queries when only relative distance is required.
    /// - No LINQ, allocations, boxing, or temporary collections.
    ///
    /// Extension methods intentionally assume a valid SpriteRenderer reference; passing
    /// null is treated as programmer error rather than silently doing nothing.
    /// </summary>
    public static class SpriteRendererExtensions
    {
        private const float FullyOpaqueAlpha = 1f;
        private const float FullyTransparentAlpha = 0f;
        private const float VisibilityEpsilon = 1e-3f;
        private const float DirectionEpsilon = 1e-3f;

        #region Validation

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFinite(float value)
            => !float.IsNaN(value) && !float.IsInfinity(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFinite(Color color)
            => IsFinite(color.r) &&
               IsFinite(color.g) &&
               IsFinite(color.b) &&
               IsFinite(color.a);

        #endregion

        #region Color & Tint

        /// <summary>
        /// Sets the full renderer color after rejecting non-finite components and clamping
        /// all channels to 0..1.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetColorSafe(this SpriteRenderer spriteRenderer, Color color)
        {
            if (!IsFinite(color))
                return;

            color.r = Mathf.Clamp01(color.r);
            color.g = Mathf.Clamp01(color.g);
            color.b = Mathf.Clamp01(color.b);
            color.a = Mathf.Clamp01(color.a);
            spriteRenderer.color = color;
        }

        /// <summary>
        /// Sets the full renderer color. Alias retained for explicit/clamped call sites.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetColorClamped(this SpriteRenderer spriteRenderer, Color color)
            => spriteRenderer.SetColorSafe(color);

        /// <summary>
        /// Multiplies the current color by <paramref name="tint"/> after validating and
        /// clamping the tint to normalized color-channel ranges.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MultiplyColor(this SpriteRenderer spriteRenderer, Color tint)
        {
            if (!IsFinite(tint))
                return;

            tint.r = Mathf.Clamp01(tint.r);
            tint.g = Mathf.Clamp01(tint.g);
            tint.b = Mathf.Clamp01(tint.b);
            tint.a = Mathf.Clamp01(tint.a);

            Color current = spriteRenderer.color;
            current.r = Mathf.Clamp01(current.r * tint.r);
            current.g = Mathf.Clamp01(current.g * tint.g);
            current.b = Mathf.Clamp01(current.b * tint.b);
            current.a = Mathf.Clamp01(current.a * tint.a);
            spriteRenderer.color = current;
        }

        /// <summary>
        /// Restores the renderer tint and alpha to <see cref="Color.white"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ClearTint(this SpriteRenderer spriteRenderer)
            => spriteRenderer.color = Color.white;

        /// <summary>
        /// Sets only RGB, preserving the current alpha. Non-finite RGB input is ignored.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetTint(this SpriteRenderer spriteRenderer, Color tint)
        {
            if (!IsFinite(tint.r) || !IsFinite(tint.g) || !IsFinite(tint.b))
                return;

            Color current = spriteRenderer.color;
            current.r = Mathf.Clamp01(tint.r);
            current.g = Mathf.Clamp01(tint.g);
            current.b = Mathf.Clamp01(tint.b);
            spriteRenderer.color = current;
        }

        /// <summary>
        /// Sets RGB while preserving alpha. Alias for <see cref="SetTint"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetColorPreservingAlpha(this SpriteRenderer spriteRenderer, Color color)
            => spriteRenderer.SetTint(color);

        /// <summary>
        /// Lerp toward a validated/clamped target color.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LerpColor(this SpriteRenderer spriteRenderer, Color target, float t)
        {
            if (!IsFinite(target) || !IsFinite(t))
                return;

            target.r = Mathf.Clamp01(target.r);
            target.g = Mathf.Clamp01(target.g);
            target.b = Mathf.Clamp01(target.b);
            target.a = Mathf.Clamp01(target.a);

            spriteRenderer.color = Color.Lerp(
                spriteRenderer.color,
                target,
                Mathf.Clamp01(t));
        }

        #endregion

        #region Alpha Control

        /// <summary>
        /// Sets alpha, clamped to 0..1. NaN/infinite input is ignored.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetAlpha(this SpriteRenderer spriteRenderer, float alpha)
        {
            if (!IsFinite(alpha))
                return;

            Color color = spriteRenderer.color;
            color.a = Mathf.Clamp01(alpha);
            spriteRenderer.color = color;
        }

        /// <summary>
        /// Alias for <see cref="SetAlpha"/> emphasizing clamping behavior.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetAlphaClamped(this SpriteRenderer spriteRenderer, float alpha)
            => spriteRenderer.SetAlpha(alpha);

        /// <summary>
        /// Adds alpha and clamps the result to 0..1. Non-finite delta is ignored.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddAlpha(this SpriteRenderer spriteRenderer, float delta)
        {
            if (!IsFinite(delta))
                return;

            Color color = spriteRenderer.color;
            color.a = Mathf.Clamp01(color.a + delta);
            spriteRenderer.color = color;
        }

        /// <summary>
        /// Makes the sprite fully opaque while preserving RGB.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetOpaque(this SpriteRenderer spriteRenderer)
            => spriteRenderer.SetAlpha(FullyOpaqueAlpha);

        /// <summary>
        /// Makes the sprite fully transparent while preserving RGB.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetTransparent(this SpriteRenderer spriteRenderer)
            => spriteRenderer.SetAlpha(FullyTransparentAlpha);

        #endregion

        #region Visibility & Renderer Control

        /// <summary>
        /// Enables the renderer and makes the sprite fully opaque.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Show(this SpriteRenderer spriteRenderer)
        {
            spriteRenderer.enabled = true;
            spriteRenderer.SetAlpha(FullyOpaqueAlpha);
        }

        /// <summary>
        /// Disables the renderer without changing its color or transform state.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Hide(this SpriteRenderer spriteRenderer)
            => spriteRenderer.enabled = false;

        /// <summary>
        /// Enables/disables the renderer. When enabling, current alpha/tint is preserved.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetEnabled(this SpriteRenderer spriteRenderer, bool enabled)
            => spriteRenderer.enabled = enabled;

        /// <summary>
        /// Enables/disables the renderer using a boolean visibility value. Enabling resets
        /// alpha to 1 through <see cref="Show"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetVisible(this SpriteRenderer spriteRenderer, bool visible)
        {
            if (visible)
                spriteRenderer.Show();
            else
                spriteRenderer.Hide();
        }

        /// <summary>
        /// Toggles renderer enabled state.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ToggleVisibility(this SpriteRenderer spriteRenderer)
            => spriteRenderer.enabled = !spriteRenderer.enabled;

        /// <summary>
        /// True when the renderer is enabled and alpha is meaningfully above zero.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsVisible(this SpriteRenderer spriteRenderer)
            => spriteRenderer.enabled && spriteRenderer.color.a > VisibilityEpsilon;

        /// <summary>
        /// True when alpha alone is meaningfully above zero, regardless of renderer enabled state.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasVisibleAlpha(this SpriteRenderer spriteRenderer)
            => spriteRenderer.color.a > VisibilityEpsilon;

        /// <summary>
        /// True when the renderer is enabled and alpha is effectively 1.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFullyVisible(this SpriteRenderer spriteRenderer)
            => spriteRenderer.enabled &&
               spriteRenderer.color.a >= FullyOpaqueAlpha - VisibilityEpsilon;

        /// <summary>
        /// True when alpha is effectively 1, regardless of renderer enabled state.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFullyOpaque(this SpriteRenderer spriteRenderer)
            => spriteRenderer.color.a >= FullyOpaqueAlpha - VisibilityEpsilon;

        /// <summary>
        /// True when renderer is disabled or alpha is effectively zero.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFullyHidden(this SpriteRenderer spriteRenderer)
            => !spriteRenderer.enabled || spriteRenderer.color.a <= VisibilityEpsilon;

        /// <summary>
        /// True when alpha is effectively zero, regardless of renderer enabled state.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFullyTransparent(this SpriteRenderer spriteRenderer)
            => spriteRenderer.color.a <= FullyTransparentAlpha + VisibilityEpsilon;

        /// <summary>
        /// True when the renderer is enabled, has a sprite, and has visible alpha.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsRendered(this SpriteRenderer spriteRenderer)
            => spriteRenderer.enabled &&
               spriteRenderer.sprite != null &&
               spriteRenderer.color.a > VisibilityEpsilon;

        #endregion

        #region Flip & Orientation

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetFlipX(this SpriteRenderer spriteRenderer, bool flipX)
            => spriteRenderer.flipX = flipX;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetFlipY(this SpriteRenderer spriteRenderer, bool flipY)
            => spriteRenderer.flipY = flipY;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetFlip(this SpriteRenderer spriteRenderer, bool flipX, bool flipY)
        {
            spriteRenderer.flipX = flipX;
            spriteRenderer.flipY = flipY;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ToggleFlipX(this SpriteRenderer spriteRenderer)
            => spriteRenderer.flipX = !spriteRenderer.flipX;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ToggleFlipY(this SpriteRenderer spriteRenderer)
            => spriteRenderer.flipY = !spriteRenderer.flipY;

        /// <summary>
        /// Faces according to a 2D direction. Near-zero X preserves the current facing.
        /// Assumes the artwork faces right when flipX is false.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FaceDirection(this SpriteRenderer spriteRenderer, Vector2 direction)
        {
            if (direction.x > DirectionEpsilon)
                spriteRenderer.flipX = false;
            else if (direction.x < -DirectionEpsilon)
                spriteRenderer.flipX = true;
        }

        /// <summary>
        /// Faces according to the supplied X direction. Near-zero values preserve the
        /// current facing.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FaceDirectionX(this SpriteRenderer spriteRenderer, float directionX)
        {
            if (directionX > DirectionEpsilon)
                spriteRenderer.flipX = false;
            else if (directionX < -DirectionEpsilon)
                spriteRenderer.flipX = true;
        }

        /// <summary>
        /// Faces the sprite toward a world-space target.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FaceTarget(this SpriteRenderer spriteRenderer, Vector2 target)
            => spriteRenderer.FaceDirection(target - (Vector2)spriteRenderer.transform.position);

        /// <summary>
        /// Faces toward target on the X axis using an explicit origin.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FaceTowardsX(
            this SpriteRenderer spriteRenderer,
            Vector3 origin,
            Vector3 target)
            => spriteRenderer.FaceDirectionX(target.x - origin.x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFacingRight(this SpriteRenderer spriteRenderer)
            => !spriteRenderer.flipX;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFacingLeft(this SpriteRenderer spriteRenderer)
            => spriteRenderer.flipX;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFlippedX(this SpriteRenderer spriteRenderer)
            => spriteRenderer.flipX;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFlippedY(this SpriteRenderer spriteRenderer)
            => spriteRenderer.flipY;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 GetFacingDirection(this SpriteRenderer spriteRenderer)
            => spriteRenderer.flipX ? Vector2.left : Vector2.right;

        #endregion

        #region Sorting

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetSortingOrder(this SpriteRenderer spriteRenderer, int order)
            => spriteRenderer.sortingOrder = order;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddSortingOrder(this SpriteRenderer spriteRenderer, int delta)
            => spriteRenderer.sortingOrder += delta;

        /// <summary>
        /// Alias for <see cref="AddSortingOrder"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void OffsetSortingOrder(this SpriteRenderer spriteRenderer, int delta)
            => spriteRenderer.AddSortingOrder(delta);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void BringInFrontOf(
            this SpriteRenderer spriteRenderer,
            SpriteRenderer other)
        {
            if (other != null)
                spriteRenderer.sortingOrder = other.sortingOrder + 1;
        }

        /// <summary>
        /// Alias for <see cref="BringInFrontOf"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RenderOnTopOf(
            this SpriteRenderer spriteRenderer,
            SpriteRenderer reference)
            => spriteRenderer.BringInFrontOf(reference);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SendBehind(
            this SpriteRenderer spriteRenderer,
            SpriteRenderer other)
        {
            if (other != null)
                spriteRenderer.sortingOrder = other.sortingOrder - 1;
        }

        /// <summary>
        /// Alias for <see cref="SendBehind"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RenderBehind(
            this SpriteRenderer spriteRenderer,
            SpriteRenderer reference)
            => spriteRenderer.SendBehind(reference);

        /// <summary>
        /// Sets the sorting layer by ID. Prefer this in hot paths.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetSortingLayerByID(this SpriteRenderer spriteRenderer, int layerID)
            => spriteRenderer.sortingLayerID = layerID;

        /// <summary>
        /// Sets the sorting layer by name when the layer name is valid.
        /// Prefer SetSortingLayerByID for repeated per-frame operations.
        /// </summary>
        public static void SetSortingLayerByName(
            this SpriteRenderer spriteRenderer,
            string layerName)
        {
            if (!string.IsNullOrEmpty(layerName))
                spriteRenderer.sortingLayerName = layerName;
        }

        /// <summary>
        /// Alias for <see cref="SetSortingLayerByName"/>.
        /// </summary>
        public static void SetSortingLayerSafe(
            this SpriteRenderer spriteRenderer,
            string layerName)
            => spriteRenderer.SetSortingLayerByName(layerName);

        #endregion

        #region Sprite & Renderer State

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasSprite(this SpriteRenderer spriteRenderer)
            => spriteRenderer.sprite != null;

        /// <summary>
        /// Assigns a non-null sprite; null leaves the current sprite unchanged.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetSpriteSafe(
            this SpriteRenderer spriteRenderer,
            Sprite sprite)
        {
            if (sprite != null)
                spriteRenderer.sprite = sprite;
        }

        /// <summary>
        /// Assigns a non-null sprite only when it differs from the current sprite.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetSpriteIfDifferent(
            this SpriteRenderer spriteRenderer,
            Sprite sprite)
        {
            if (sprite != null && spriteRenderer.sprite != sprite)
                spriteRenderer.sprite = sprite;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ClearSprite(this SpriteRenderer spriteRenderer)
            => spriteRenderer.sprite = null;

        /// <summary>
        /// Resets render state to predictable pooled-object defaults.
        /// </summary>
        public static void ResetForPool(this SpriteRenderer spriteRenderer)
        {
            spriteRenderer.sprite = null;
            spriteRenderer.color = Color.white;
            spriteRenderer.flipX = false;
            spriteRenderer.flipY = false;
            spriteRenderer.sortingOrder = 0;
            spriteRenderer.enabled = false;
        }

        #endregion

        #region Bounds, Size & Positioning

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bounds GetWorldBounds(this SpriteRenderer spriteRenderer)
            => spriteRenderer.bounds;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 GetWorldSize(this SpriteRenderer spriteRenderer)
            => spriteRenderer.bounds.size;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 GetWorldHalfSize(this SpriteRenderer spriteRenderer)
            => spriteRenderer.bounds.extents;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 GetWorldCenter(this SpriteRenderer spriteRenderer)
            => spriteRenderer.bounds.center;

        /// <summary>
        /// Squared distance to the closest point on the renderer's world-space AABB.
        /// This is the canonical SqrDistanceTo query because it measures distance to
        /// the rendered bounds rather than only to its center.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SqrDistanceTo(
            this SpriteRenderer spriteRenderer,
            Vector3 worldPoint)
        {
            Vector3 closest = spriteRenderer.bounds.ClosestPoint(worldPoint);
            return (worldPoint - closest).sqrMagnitude;
        }

        /// <summary>
        /// Squared distance from a point to the sprite's bounds center.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SqrDistanceToCenter(
            this SpriteRenderer spriteRenderer,
            Vector3 worldPoint)
        {
            Vector3 delta = worldPoint - spriteRenderer.bounds.center;
            return delta.sqrMagnitude;
        }

        /// <summary>
        /// Actual distance to the sprite's bounds center.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DistanceToCenter(
            this SpriteRenderer spriteRenderer,
            Vector3 worldPoint)
            => Mathf.Sqrt(spriteRenderer.SqrDistanceToCenter(worldPoint));

        /// <summary>
        /// Alias for <see cref="DistanceToCenter"/> retained for API compatibility with
        /// the center-distance implementation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DistanceTo(
            this SpriteRenderer spriteRenderer,
            Vector3 worldPoint)
            => spriteRenderer.DistanceToCenter(worldPoint);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ContainsWorldPoint(
            this SpriteRenderer spriteRenderer,
            Vector3 worldPoint)
            => spriteRenderer.bounds.Contains(worldPoint);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsOverlapping(
            this SpriteRenderer spriteRenderer,
            SpriteRenderer other)
            => other != null && spriteRenderer.bounds.Intersects(other.bounds);

        /// <summary>
        /// Snaps world X/Y to the pixel grid. Z is preserved by default for 2D layering.
        /// </summary>
        public static void SnapToPixelGrid(
            this SpriteRenderer spriteRenderer,
            float pixelsPerUnit)
        {
            spriteRenderer.SnapToPixelGrid(pixelsPerUnit, false);
        }

        /// <summary>
        /// Snaps world position to the pixel grid. When <paramref name="snapZ"/> is true,
        /// Z is snapped too; otherwise Z is left untouched.
        /// </summary>
        public static void SnapToPixelGrid(
            this SpriteRenderer spriteRenderer,
            float pixelsPerUnit,
            bool snapZ)
        {
            if (!IsFinite(pixelsPerUnit) || pixelsPerUnit <= 0f)
                return;

            float unitPerPixel = 1f / pixelsPerUnit;
            Transform transform = spriteRenderer.transform;
            Vector3 position = transform.position;

            position.x = Mathf.Round(position.x / unitPerPixel) * unitPerPixel;
            position.y = Mathf.Round(position.y / unitPerPixel) * unitPerPixel;

            if (snapZ)
                position.z = Mathf.Round(position.z / unitPerPixel) * unitPerPixel;

            transform.position = position;
        }

        /// <summary>
        /// Snaps using the assigned sprite's native pixels-per-unit value.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SnapToPixelGrid(this SpriteRenderer spriteRenderer)
        {
            Sprite sprite = spriteRenderer.sprite;
            if (sprite != null)
                spriteRenderer.SnapToPixelGrid(sprite.pixelsPerUnit);
        }

        /// <summary>
        /// Moves the renderer so its world-space bounds bottom edge is exactly at
        /// <paramref name="groundY"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AlignBottomTo(
            this SpriteRenderer spriteRenderer,
            float groundY)
        {
            Bounds bounds = spriteRenderer.bounds;
            Transform transform = spriteRenderer.transform;
            Vector3 position = transform.position;
            position.y += groundY - bounds.min.y;
            transform.position = position;
        }

        #endregion

        #region Fade & Flash Transitions

        /// <summary>
        /// Coroutine that fades alpha from its current value to the target over duration.
        /// Renderer enabled state and RGB are unchanged.
        ///
        /// NaN target preserves current alpha. Infinite target resolves to the corresponding
        /// endpoint (positive infinity -> 1, negative infinity -> 0). Non-finite or
        /// non-positive duration resolves immediately.
        /// </summary>
        public static IEnumerator FadeAlphaTo(
            this SpriteRenderer spriteRenderer,
            float targetAlpha,
            float duration,
            bool useUnscaledTime = false)
        {
            float currentAlpha = spriteRenderer.color.a;
            float target;

            if (float.IsNaN(targetAlpha))
                target = currentAlpha;
            else if (float.IsPositiveInfinity(targetAlpha))
                target = FullyOpaqueAlpha;
            else if (float.IsNegativeInfinity(targetAlpha))
                target = FullyTransparentAlpha;
            else
                target = Mathf.Clamp01(targetAlpha);

            if (!IsFinite(duration) || duration <= 0f)
            {
                spriteRenderer.SetAlpha(target);
                yield break;
            }

            float start = currentAlpha;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                float deltaTime = useUnscaledTime
                    ? Time.unscaledDeltaTime
                    : Time.deltaTime;

                elapsed += deltaTime;

                float t = Mathf.Clamp01(elapsed / duration);
                spriteRenderer.SetAlpha(Mathf.Lerp(start, target, t));
                yield return null;
            }

            spriteRenderer.SetAlpha(target);
        }

        /// <summary>
        /// Enables the renderer and fades alpha from zero to one.
        /// </summary>
        public static IEnumerator FadeIn(
            this SpriteRenderer spriteRenderer,
            float duration,
            bool useUnscaledTime = false)
        {
            spriteRenderer.enabled = true;
            spriteRenderer.SetAlpha(FullyTransparentAlpha);
            yield return spriteRenderer.FadeAlphaTo(
                FullyOpaqueAlpha,
                duration,
                useUnscaledTime);
        }

        /// <summary>
        /// Fades alpha to zero and disables the renderer when complete.
        /// </summary>
        public static IEnumerator FadeOut(
            this SpriteRenderer spriteRenderer,
            float duration,
            bool useUnscaledTime = false)
        {
            yield return spriteRenderer.FadeAlphaTo(
                FullyTransparentAlpha,
                duration,
                useUnscaledTime);

            spriteRenderer.enabled = false;
        }

        /// <summary>
        /// Flash the sprite toward a target color for half the duration, then restore the
        /// color captured at the start for the second half.
        ///
        /// A non-positive/non-finite duration applies the flash color immediately.
        /// </summary>
        public static IEnumerator FlashColor(
            this SpriteRenderer spriteRenderer,
            Color flashColor,
            float flashDuration,
            bool useUnscaledTime = false)
        {
            if (!IsFinite(flashColor))
                yield break;

            flashColor.r = Mathf.Clamp01(flashColor.r);
            flashColor.g = Mathf.Clamp01(flashColor.g);
            flashColor.b = Mathf.Clamp01(flashColor.b);
            flashColor.a = Mathf.Clamp01(flashColor.a);

            Color original = spriteRenderer.color;

            if (!IsFinite(flashDuration) || flashDuration <= 0f)
            {
                spriteRenderer.color = flashColor;
                yield break;
            }

            float halfDuration = flashDuration * 0.5f;
            float elapsed = 0f;

            while (elapsed < halfDuration)
            {
                elapsed += useUnscaledTime
                    ? Time.unscaledDeltaTime
                    : Time.deltaTime;

                float t = Mathf.Clamp01(elapsed / halfDuration);
                spriteRenderer.color = Color.Lerp(original, flashColor, t);
                yield return null;
            }

            elapsed = 0f;

            while (elapsed < halfDuration)
            {
                elapsed += useUnscaledTime
                    ? Time.unscaledDeltaTime
                    : Time.deltaTime;

                float t = Mathf.Clamp01(elapsed / halfDuration);
                spriteRenderer.color = Color.Lerp(flashColor, original, t);
                yield return null;
            }

            spriteRenderer.color = original;
        }

        #endregion
    }
}
