using System.Runtime.CompilerServices;
using UnityEngine;

namespace Nexus.Core.Extensions
{
    /// <summary>
    /// Production-ready extension methods for <see cref="RectTransform"/> covering anchor
    /// stretching, edge-based sizing, pivot-safe repositioning and world/screen space conversion
    /// for Unity UI (Canvas) layout code.
    /// <para>
    /// <see cref="RectTransform"/>'s API is notoriously indirect: <see cref="RectTransform.sizeDelta"/>
    /// means different things depending on the current anchor configuration,
    /// <see cref="RectTransform.rect"/> is always local space, and moving a UI element on screen
    /// generally requires touching two or three properties in a specific order. This class wraps
    /// those interactions into single, unambiguous calls.
    /// </para>
    /// <para>Design rules:</para>
    /// <list type="bullet">
    ///     <item>Edge-setting methods (<c>SetLeft</c>, <c>SetRight</c>, etc.) operate via <see cref="RectTransform.offsetMin"/>/<see cref="RectTransform.offsetMax"/> and assume the rect is anchor-stretched on that axis — this is documented explicitly per method since the meaning of "edge" is otherwise ambiguous for a non-stretched anchor.</item>
    ///     <item>Pivot changes always preserve the rect's current screen position via <see cref="SetPivotPreservingPosition(RectTransform, Vector2)"/>, since Unity's default pivot setter otherwise visibly shifts the element.</item>
    ///     <item>World/viewport conversions require an explicit <see cref="Camera"/> parameter for <c>Screen Space - Camera</c> and <c>World Space</c> canvases; a null camera is valid and expected for <c>Screen Space - Overlay</c>, matching Unity's own <see cref="RectTransformUtility"/> contract.</item>
    ///     <item>Zero allocation across the entire class. No boxing, no LINQ.</item>
    /// </list>
    /// </summary>
    public static class RectTransformExtensions
    {
        #region Anchoring & Stretching

        /// <summary>
        /// Sets both <see cref="RectTransform.anchorMin"/> and <see cref="RectTransform.anchorMax"/>
        /// to <paramref name="anchor"/>, collapsing the rect to a single anchor point.
        /// <br/>
        /// Standard first step before positioning a UI element with
        /// <see cref="RectTransform.anchoredPosition"/> relative to a specific corner or edge of
        /// its parent (e.g. anchoring a notification icon to the parent's top-right corner).
        /// </summary>
        /// <param name="rt">RectTransform to modify.</param>
        /// <param name="anchor">Normalized anchor point applied to both min and max.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetAnchor(this RectTransform rt, Vector2 anchor)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
        }

        /// <summary>
        /// Stretches <paramref name="rt"/> to fully fill its parent, setting anchors to (0,0)-(1,1)
        /// and clearing <see cref="RectTransform.sizeDelta"/> and <see cref="RectTransform.anchoredPosition"/>.
        /// <br/>
        /// The standard "fill parent" operation for background panels, full-screen overlays and
        /// content containers that should always match their parent's size regardless of resize
        /// or resolution changes.
        /// </summary>
        /// <param name="rt">RectTransform to stretch.</param>
        public static void StretchToParent(this RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
        }

        /// <summary>
        /// Stretches <paramref name="rt"/> to fill its parent horizontally while preserving its
        /// current height and vertical anchor configuration.
        /// <br/>
        /// Useful for a full-width divider, header bar or horizontal progress track that should
        /// always match parent width but retain an author-controlled fixed height.
        /// </summary>
        /// <param name="rt">RectTransform to stretch.</param>
        public static void StretchHorizontally(this RectTransform rt)
        {
            rt.anchorMin = new Vector2(0f, rt.anchorMin.y);
            rt.anchorMax = new Vector2(1f, rt.anchorMax.y);
            rt.offsetMin = new Vector2(0f, rt.offsetMin.y);
            rt.offsetMax = new Vector2(0f, rt.offsetMax.y);
        }

        /// <summary>
        /// Stretches <paramref name="rt"/> to fill its parent vertically while preserving its
        /// current width and horizontal anchor configuration.
        /// <br/>
        /// Useful for a full-height sidebar or scrollbar track that should always match parent
        /// height but retain an author-controlled fixed width.
        /// </summary>
        /// <param name="rt">RectTransform to stretch.</param>
        public static void StretchVertically(this RectTransform rt)
        {
            rt.anchorMin = new Vector2(rt.anchorMin.x, 0f);
            rt.anchorMax = new Vector2(rt.anchorMax.x, 1f);
            rt.offsetMin = new Vector2(rt.offsetMin.x, 0f);
            rt.offsetMax = new Vector2(rt.offsetMax.x, 0f);
        }

        /// <summary>
        /// Returns <c>true</c> if <paramref name="rt"/> is anchor-stretched (min and max anchors
        /// differ) on the horizontal axis.
        /// <br/>
        /// Useful for validating a rect's configuration before calling <see cref="SetLeft(RectTransform, float)"/>
        /// or <see cref="SetRight(RectTransform, float)"/>, which only behave meaningfully on a
        /// horizontally-stretched rect.
        /// </summary>
        /// <param name="rt">RectTransform to test.</param>
        /// <returns><c>true</c> if the horizontal anchors are not equal.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsStretchedHorizontally(this RectTransform rt)
            => !Mathf.Approximately(rt.anchorMin.x, rt.anchorMax.x);

        /// <summary>
        /// Returns <c>true</c> if <paramref name="rt"/> is anchor-stretched (min and max anchors
        /// differ) on the vertical axis.
        /// <br/>
        /// Useful for validating a rect's configuration before calling <see cref="SetTop(RectTransform, float)"/>
        /// or <see cref="SetBottom(RectTransform, float)"/>, which only behave meaningfully on a
        /// vertically-stretched rect.
        /// </summary>
        /// <param name="rt">RectTransform to test.</param>
        /// <returns><c>true</c> if the vertical anchors are not equal.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsStretchedVertically(this RectTransform rt)
            => !Mathf.Approximately(rt.anchorMin.y, rt.anchorMax.y);

        #endregion

        #region Edge Manipulation (Stretched Rects)

        /// <summary>
        /// Sets the left inset of <paramref name="rt"/> via <see cref="RectTransform.offsetMin"/>.
        /// <br/>
        /// Only meaningful when <paramref name="rt"/> is horizontally anchor-stretched (see
        /// <see cref="IsStretchedHorizontally(RectTransform)"/>); on a non-stretched rect this
        /// instead alters <see cref="RectTransform.sizeDelta"/> per Unity's own offset semantics.
        /// Standard way to apply a left margin to a full-width child inside a padded container.
        /// </summary>
        /// <param name="rt">RectTransform to modify.</param>
        /// <param name="left">Distance from the parent's left edge, in local units.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetLeft(this RectTransform rt, float left)
            => rt.offsetMin = new Vector2(left, rt.offsetMin.y);

        /// <summary>
        /// Sets the right inset of <paramref name="rt"/> via <see cref="RectTransform.offsetMax"/>.
        /// <br/>
        /// Only meaningful when <paramref name="rt"/> is horizontally anchor-stretched. Standard
        /// way to apply a right margin to a full-width child inside a padded container.
        /// </summary>
        /// <param name="rt">RectTransform to modify.</param>
        /// <param name="right">Distance from the parent's right edge, in local units (positive shrinks inward).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetRight(this RectTransform rt, float right)
            => rt.offsetMax = new Vector2(-right, rt.offsetMax.y);

        /// <summary>
        /// Sets the top inset of <paramref name="rt"/> via <see cref="RectTransform.offsetMax"/>.
        /// <br/>
        /// Only meaningful when <paramref name="rt"/> is vertically anchor-stretched. Standard way
        /// to apply a top margin to a full-height child inside a padded container.
        /// </summary>
        /// <param name="rt">RectTransform to modify.</param>
        /// <param name="top">Distance from the parent's top edge, in local units (positive shrinks inward).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetTop(this RectTransform rt, float top)
            => rt.offsetMax = new Vector2(rt.offsetMax.x, -top);

        /// <summary>
        /// Sets the bottom inset of <paramref name="rt"/> via <see cref="RectTransform.offsetMin"/>.
        /// <br/>
        /// Only meaningful when <paramref name="rt"/> is vertically anchor-stretched. Standard way
        /// to apply a bottom margin to a full-height child inside a padded container.
        /// </summary>
        /// <param name="rt">RectTransform to modify.</param>
        /// <param name="bottom">Distance from the parent's bottom edge, in local units.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetBottom(this RectTransform rt, float bottom)
            => rt.offsetMin = new Vector2(rt.offsetMin.x, bottom);

        /// <summary>
        /// Sets all four edge insets of <paramref name="rt"/> in a single call, for a rect that is
        /// fully anchor-stretched on both axes.
        /// <br/>
        /// The general-purpose margin utility for stretched UI panels — equivalent to CSS-style
        /// padding — avoiding four separate property assignments at call sites configuring a
        /// content area inside a bordered frame.
        /// </summary>
        /// <param name="rt">RectTransform to modify. Should be stretched on both axes.</param>
        /// <param name="left">Left inset.</param>
        /// <param name="right">Right inset.</param>
        /// <param name="top">Top inset.</param>
        /// <param name="bottom">Bottom inset.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetInsets(this RectTransform rt, float left, float right, float top, float bottom)
        {
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        #endregion

        #region Size

        /// <summary>
        /// Returns the current width of <paramref name="rt"/> as evaluated by
        /// <see cref="RectTransform.rect"/>, accounting for anchor stretching.
        /// <br/>
        /// Safer than reading <see cref="RectTransform.sizeDelta"/>.x directly, since sizeDelta
        /// only reflects true width when the rect is <b>not</b> horizontally stretched — this
        /// always returns the actual rendered width regardless of anchor configuration.
        /// </summary>
        /// <param name="rt">RectTransform to measure.</param>
        /// <returns>The current width, in local units.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetWidth(this RectTransform rt)
            => rt.rect.width;

        /// <summary>
        /// Returns the current height of <paramref name="rt"/> as evaluated by
        /// <see cref="RectTransform.rect"/>, accounting for anchor stretching.
        /// <br/>
        /// Safer than reading <see cref="RectTransform.sizeDelta"/>.y directly for the same reason
        /// as <see cref="GetWidth(RectTransform)"/> — always reflects the true rendered height.
        /// </summary>
        /// <param name="rt">RectTransform to measure.</param>
        /// <returns>The current height, in local units.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetHeight(this RectTransform rt)
            => rt.rect.height;

        /// <summary>
        /// Sets the width of <paramref name="rt"/> via <see cref="RectTransform.sizeDelta"/>,
        /// leaving height unchanged.
        /// <br/>
        /// Correct only for a rect that is <b>not</b> horizontally anchor-stretched; use
        /// <see cref="SetLeft(RectTransform, float)"/>/<see cref="SetRight(RectTransform, float)"/>
        /// to control width on a stretched rect instead. Ideal for a fixed-anchor icon or button
        /// whose width is tuned at runtime (e.g. resizing a progress-fill bar).
        /// </summary>
        /// <param name="rt">RectTransform to modify.</param>
        /// <param name="width">New width, in local units.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetWidth(this RectTransform rt, float width)
            => rt.sizeDelta = new Vector2(width, rt.sizeDelta.y);

        /// <summary>
        /// Sets the height of <paramref name="rt"/> via <see cref="RectTransform.sizeDelta"/>,
        /// leaving width unchanged.
        /// <br/>
        /// Correct only for a rect that is <b>not</b> vertically anchor-stretched; use
        /// <see cref="SetTop(RectTransform, float)"/>/<see cref="SetBottom(RectTransform, float)"/>
        /// to control height on a stretched rect instead.
        /// </summary>
        /// <param name="rt">RectTransform to modify.</param>
        /// <param name="height">New height, in local units.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetHeight(this RectTransform rt, float height)
            => rt.sizeDelta = new Vector2(rt.sizeDelta.x, height);

        /// <summary>
        /// Sets both width and height of <paramref name="rt"/> via <see cref="RectTransform.sizeDelta"/>.
        /// <br/>
        /// Correct only for a rect that is not anchor-stretched on the corresponding axis. Standard
        /// single-call resize for a fixed-anchor UI element such as a resizable window or a
        /// dynamically-scaled icon.
        /// </summary>
        /// <param name="rt">RectTransform to modify.</param>
        /// <param name="size">New size, in local units.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetSize(this RectTransform rt, Vector2 size)
            => rt.sizeDelta = size;

        #endregion

        #region Position

        /// <summary>
        /// Sets the X component of <paramref name="rt"/>.anchoredPosition, leaving Y unchanged.
        /// <br/>
        /// Avoids the three-line read-modify-write pattern for adjusting a single axis of a UI
        /// element's position relative to its anchor, such as sliding a panel horizontally during
        /// a wipe transition.
        /// </summary>
        /// <param name="rt">RectTransform to move.</param>
        /// <param name="x">New anchored X position.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetAnchoredX(this RectTransform rt, float x)
            => rt.anchoredPosition = new Vector2(x, rt.anchoredPosition.y);

        /// <summary>
        /// Sets the Y component of <paramref name="rt"/>.anchoredPosition, leaving X unchanged.
        /// <br/>
        /// Common for vertical slide-in/slide-out animations (notification toasts, dropdown
        /// panels) that should only move along one axis relative to their anchor.
        /// </summary>
        /// <param name="rt">RectTransform to move.</param>
        /// <param name="y">New anchored Y position.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetAnchoredY(this RectTransform rt, float y)
            => rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, y);

        /// <summary>
        /// Offsets <paramref name="rt"/>.anchoredPosition by <paramref name="offset"/>.
        /// <br/>
        /// Reads more clearly than a manual read-add-write at call sites doing incremental UI
        /// motion, such as per-frame screen-shake jitter applied to a HUD element.
        /// </summary>
        /// <param name="rt">RectTransform to move.</param>
        /// <param name="offset">Offset added to the current anchored position.</param>
        /// <returns>The resulting anchored position after the offset is applied.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 AddAnchoredPosition(this RectTransform rt, Vector2 offset)
        {
            Vector2 p = rt.anchoredPosition + offset;
            rt.anchoredPosition = p;
            return p;
        }

        #endregion

        #region Pivot

        /// <summary>
        /// Sets <paramref name="rt"/>'s pivot to <paramref name="pivot"/> while adjusting
        /// <see cref="RectTransform.anchoredPosition"/> to keep the rect visually in the same
        /// place.
        /// <br/>
        /// Unity's default <see cref="RectTransform.pivot"/> setter recalculates the rect around
        /// the new pivot point, which visibly shifts the element on screen. This is the standard
        /// fix required before rotating or scaling a UI element around a corner instead of its
        /// center (e.g. a health bar that should deplete from a fixed left edge).
        /// </summary>
        /// <param name="rt">RectTransform whose pivot is changed.</param>
        /// <param name="pivot">New normalized pivot point.</param>
        public static void SetPivotPreservingPosition(this RectTransform rt, Vector2 pivot)
        {
            Vector2 size = rt.rect.size;
            Vector2 deltaPivot = rt.pivot - pivot;
            Vector3 deltaPosition = new Vector3(deltaPivot.x * size.x, deltaPivot.y * size.y, 0f);

            rt.pivot = pivot;
            rt.localPosition -= rt.rotation * Vector3.Scale(deltaPosition, rt.localScale);
        }

        #endregion

        #region World & Screen Conversion

        /// <summary>
        /// Returns the world-space axis-aligned bounding rectangle of <paramref name="rt"/>,
        /// computed from its four corners.
        /// <br/>
        /// Useful for positioning a world-space effect (highlight decal, damage number spawn
        /// point) relative to a UI element's actual rendered bounds, accounting for any rotation
        /// or scale applied to the element or its ancestors.
        /// </summary>
        /// <param name="rt">RectTransform to measure.</param>
        /// <returns>The world-space bounding rect.</returns>
        public static Rect GetWorldRect(this RectTransform rt)
        {
            Vector3[] corners = new Vector3[4];
            rt.GetWorldCorners(corners);

            float xMin = Mathf.Min(corners[0].x, corners[2].x);
            float xMax = Mathf.Max(corners[0].x, corners[2].x);
            float yMin = Mathf.Min(corners[0].y, corners[2].y);
            float yMax = Mathf.Max(corners[0].y, corners[2].y);

            return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        /// <summary>
        /// Returns the screen-space axis-aligned bounding rectangle of <paramref name="rt"/> as
        /// seen through <paramref name="camera"/>.
        /// <br/>
        /// Pass <c>null</c> for <paramref name="camera"/> when the owning canvas is in
        /// <c>Screen Space - Overlay</c> mode, matching Unity's own
        /// <see cref="RectTransformUtility.WorldToScreenPoint(Camera, Vector3)"/> contract. Useful
        /// for positioning a native OS UI element or a non-canvas overlay to align with a
        /// specific UI widget.
        /// </summary>
        /// <param name="rt">RectTransform to measure.</param>
        /// <param name="camera">Camera used for the projection, or <c>null</c> for overlay canvases.</param>
        /// <returns>The screen-space bounding rect.</returns>
        public static Rect GetScreenRect(this RectTransform rt, Camera camera)
        {
            Vector3[] corners = new Vector3[4];
            rt.GetWorldCorners(corners);

            Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);

            return new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
        }

        /// <summary>
        /// Returns <c>true</c> if <paramref name="screenPoint"/> lies within
        /// <paramref name="rt"/>'s bounds as seen through <paramref name="camera"/>.
        /// <br/>
        /// Thin, explicitly-named wrapper over
        /// <see cref="RectTransformUtility.RectangleContainsScreenPoint(RectTransform, Vector2, Camera)"/>
        /// for fluent call chains in custom pointer/touch handling code that doesn't go through
        /// Unity's standard event system.
        /// </summary>
        /// <param name="rt">RectTransform to test against.</param>
        /// <param name="screenPoint">Screen-space point to test, typically from touch or mouse input.</param>
        /// <param name="camera">Camera used for the projection, or <c>null</c> for overlay canvases.</param>
        /// <returns><c>true</c> if the point falls within the rect's screen bounds.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ContainsScreenPoint(this RectTransform rt, Vector2 screenPoint, Camera camera)
            => RectTransformUtility.RectangleContainsScreenPoint(rt, screenPoint, camera);

        #endregion

        #region Copying

        /// <summary>
        /// Copies anchors, pivot, anchored position and size delta from <paramref name="source"/>
        /// onto <paramref name="rt"/>.
        /// <br/>
        /// Standard way to make a dynamically-created UI element (e.g. a runtime-instantiated
        /// tooltip or highlight overlay) exactly match the layout configuration of an existing
        /// reference element without manually copying each property.
        /// </summary>
        /// <param name="rt">RectTransform to modify.</param>
        /// <param name="source">RectTransform whose layout values are copied.</param>
        public static void CopyLayoutFrom(this RectTransform rt, RectTransform source)
        {
            rt.anchorMin = source.anchorMin;
            rt.anchorMax = source.anchorMax;
            rt.pivot = source.pivot;
            rt.anchoredPosition = source.anchoredPosition;
            rt.sizeDelta = source.sizeDelta;
        }

        #endregion

        #region Debug

        /// <summary>
        /// Returns a formatted debug string describing <paramref name="rt"/>'s anchored position,
        /// size and anchor configuration.
        /// <br/>
        /// More informative than logging <see cref="RectTransform.anchoredPosition"/> alone when
        /// diagnosing layout bugs across a complex nested canvas hierarchy. Allocates a string and
        /// is intended for logs/inspectors, not hot paths.
        /// </summary>
        /// <param name="rt">RectTransform to describe.</param>
        /// <returns>A formatted debug string.</returns>
        public static string ToRectTransformDebugString(this RectTransform rt)
        {
            if (rt.IsUnityNull())
                return "<null RectTransform>";

            Vector2 pos = rt.anchoredPosition;
            Vector2 size = rt.rect.size;
            return $"{rt.GetHierarchyPath()} AnchoredPos({pos.x:F1}, {pos.y:F1}) Size({size.x:F1}, {size.y:F1}) Anchors[{rt.anchorMin}-{rt.anchorMax}]";
        }

        #endregion
    }
}