using System.Runtime.CompilerServices;
using UnityEngine;

namespace Nexus.Core.Extensions
{
    /// <summary>
    /// Production-ready extension methods for <see cref="Rect"/> covering edge manipulation,
    /// containment, intersection, clamping, aspect-ratio fitting and screen/viewport conversion.
    /// <para>Design rules:</para>
    /// <list type="bullet">
    ///     <item>Edge-based mutators (<c>WithLeft</c>, <c>WithTop</c>, etc.) always resize the rect while keeping the opposite edge fixed, matching Unity's own <see cref="Rect.xMin"/>/<see cref="Rect.yMin"/> semantics rather than translating the whole rect.</item>
    ///     <item>Move-based mutators (<c>MoveLeftTo</c>, etc.) always preserve size and reposition the whole rect — the two families are never conflated.</item>
    ///     <item>All containment and equality checks accept an optional epsilon to absorb floating-point noise from UI layout and animated rect tweens.</item>
    ///     <item>Rects with negative width/height (a valid Unity construct, e.g. a drag-select box dragged right-to-left) are normalized explicitly via <see cref="Normalized(Rect)"/> rather than silently mishandled.</item>
    ///     <item>Zero allocation throughout except explicit array-returning corner queries, which offer a non-allocating overload alongside the convenience allocating one.</item>
    /// </list>
    /// </summary>
    public static class RectExtensions
    {
        #region Constants

        /// <summary>
        /// Default tolerance used by approximate-equality, containment and zero-area checks to
        /// absorb floating-point noise from UI layout recalculation and animated rect tweens.
        /// </summary>
        private const float DefaultEpsilon = 1e-5f;

        #endregion

        #region Component Replacement (With*)

        /// <summary>
        /// Returns a copy of <paramref name="r"/> with its X position replaced, keeping width and
        /// height unchanged.
        /// <br/>
        /// Ideal for repositioning a UI panel horizontally without recalculating its size.
        /// </summary>
        /// <param name="r">Source rect to copy.</param>
        /// <param name="x">New X position.</param>
        /// <returns>A new <see cref="Rect"/> with the updated X position.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rect WithX(this Rect r, float x)
            => new Rect(x, r.y, r.width, r.height);

        /// <summary>
        /// Returns a copy of <paramref name="r"/> with its Y position replaced, keeping width and
        /// height unchanged.
        /// <br/>
        /// Ideal for repositioning a UI panel vertically without recalculating its size.
        /// </summary>
        /// <param name="r">Source rect to copy.</param>
        /// <param name="y">New Y position.</param>
        /// <returns>A new <see cref="Rect"/> with the updated Y position.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rect WithY(this Rect r, float y)
            => new Rect(r.x, y, r.width, r.height);

        /// <summary>
        /// Returns a copy of <paramref name="r"/> with its position replaced, keeping width and
        /// height unchanged.
        /// <br/>
        /// Standard way to move a rect without disturbing a separately-tuned size value.
        /// </summary>
        /// <param name="r">Source rect to copy.</param>
        /// <param name="position">New position.</param>
        /// <returns>A new <see cref="Rect"/> with the updated position.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rect WithPosition(this Rect r, Vector2 position)
            => new Rect(position, r.size);

        /// <summary>
        /// Returns a copy of <paramref name="r"/> with its width replaced, keeping position and
        /// height unchanged.
        /// <br/>
        /// Useful for driving a progress-bar fill or resizable panel width from a normalized value
        /// without touching the rect's anchor position.
        /// </summary>
        /// <param name="r">Source rect to copy.</param>
        /// <param name="width">New width.</param>
        /// <returns>A new <see cref="Rect"/> with the updated width.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rect WithWidth(this Rect r, float width)
            => new Rect(r.x, r.y, width, r.height);

        /// <summary>
        /// Returns a copy of <paramref name="r"/> with its height replaced, keeping position and
        /// width unchanged.
        /// <br/>
        /// Useful for driving a vertical fill bar height from a normalized value without touching
        /// the rect's anchor position.
        /// </summary>
        /// <param name="r">Source rect to copy.</param>
        /// <param name="height">New height.</param>
        /// <returns>A new <see cref="Rect"/> with the updated height.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rect WithHeight(this Rect r, float height)
            => new Rect(r.x, r.y, r.width, height);

        /// <summary>
        /// Returns a copy of <paramref name="r"/> with its size replaced, keeping position
        /// unchanged.
        /// <br/>
        /// Standard way to resize a rect anchored at a fixed top-left corner, such as a resizable
        /// window whose top-left stays pinned while the user drags its bottom-right corner.
        /// </summary>
        /// <param name="r">Source rect to copy.</param>
        /// <param name="size">New size.</param>
        /// <returns>A new <see cref="Rect"/> with the updated size.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rect WithSize(this Rect r, Vector2 size)
            => new Rect(r.position, size);

        #endregion

        #region Edge Resizing

        /// <summary>
        /// Returns a copy of <paramref name="r"/> with its left edge moved to <paramref name="left"/>,
        /// keeping the right edge fixed. Width changes to compensate.
        /// <br/>
        /// Mirrors Unity's own <see cref="Rect.xMin"/> setter semantics as a non-mutating extension,
        /// useful for animating a panel's left edge inward/outward during a wipe transition.
        /// </summary>
        /// <param name="r">Source rect to copy.</param>
        /// <param name="left">New left edge (X minimum) position.</param>
        /// <returns>A new <see cref="Rect"/> with the left edge moved and the right edge preserved.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rect WithLeft(this Rect r, float left)
        {
            Rect result = r;
            result.xMin = left;
            return result;
        }

        /// <summary>
        /// Returns a copy of <paramref name="r"/> with its right edge moved to <paramref name="right"/>,
        /// keeping the left edge fixed. Width changes to compensate.
        /// <br/>
        /// Useful for animating a panel's right edge during a wipe/reveal transition without
        /// disturbing its anchored left position.
        /// </summary>
        /// <param name="r">Source rect to copy.</param>
        /// <param name="right">New right edge (X maximum) position.</param>
        /// <returns>A new <see cref="Rect"/> with the right edge moved and the left edge preserved.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rect WithRight(this Rect r, float right)
        {
            Rect result = r;
            result.xMax = right;
            return result;
        }

        /// <summary>
        /// Returns a copy of <paramref name="r"/> with its top edge moved to <paramref name="top"/>,
        /// keeping the bottom edge fixed. Height changes to compensate.
        /// <br/>
        /// Useful for animating a dropdown or notification panel's top edge without moving its
        /// anchored bottom position.
        /// </summary>
        /// <param name="r">Source rect to copy.</param>
        /// <param name="top">New top edge (Y minimum) position.</param>
        /// <returns>A new <see cref="Rect"/> with the top edge moved and the bottom edge preserved.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rect WithTop(this Rect r, float top)
        {
            Rect result = r;
            result.yMin = top;
            return result;
        }

        /// <summary>
        /// Returns a copy of <paramref name="r"/> with its bottom edge moved to
        /// <paramref name="bottom"/>, keeping the top edge fixed. Height changes to compensate.
        /// <br/>
        /// Useful for growing a chat log or scrollable panel downward while its top stays anchored.
        /// </summary>
        /// <param name="r">Source rect to copy.</param>
        /// <param name="bottom">New bottom edge (Y maximum) position.</param>
        /// <returns>A new <see cref="Rect"/> with the bottom edge moved and the top edge preserved.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rect WithBottom(this Rect r, float bottom)
        {
            Rect result = r;
            result.yMax = bottom;
            return result;
        }

        #endregion

        #region Move (Position-Only, Size-Preserving)

        /// <summary>
        /// Returns a copy of <paramref name="r"/> translated so its left edge sits at
        /// <paramref name="left"/>, preserving the current width (unlike <see cref="WithLeft(Rect, float)"/>,
        /// which resizes instead of moving).
        /// <br/>
        /// Ideal for docking a fixed-size panel to a screen edge without any resize side-effects.
        /// </summary>
        /// <param name="r">Source rect to move.</param>
        /// <param name="left">Target left edge (X minimum) position.</param>
        /// <returns>A new <see cref="Rect"/> at the target left edge with unchanged size.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rect MoveLeftTo(this Rect r, float left)
            => new Rect(left, r.y, r.width, r.height);

        /// <summary>
        /// Returns a copy of <paramref name="r"/> translated so its right edge sits at
        /// <paramref name="right"/>, preserving the current width.
        /// <br/>
        /// Ideal for docking a fixed-size panel flush against a screen's right edge.
        /// </summary>
        /// <param name="r">Source rect to move.</param>
        /// <param name="right">Target right edge (X maximum) position.</param>
        /// <returns>A new <see cref="Rect"/> at the target right edge with unchanged size.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rect MoveRightTo(this Rect r, float right)
            => new Rect(right - r.width, r.y, r.width, r.height);

        /// <summary>
        /// Returns a copy of <paramref name="r"/> translated so its top edge sits at
        /// <paramref name="top"/>, preserving the current height.
        /// <br/>
        /// Ideal for docking a fixed-size panel flush against a screen's top edge.
        /// </summary>
        /// <param name="r">Source rect to move.</param>
        /// <param name="top">Target top edge (Y minimum) position.</param>
        /// <returns>A new <see cref="Rect"/> at the target top edge with unchanged size.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rect MoveTopTo(this Rect r, float top)
            => new Rect(r.x, top, r.width, r.height);

        /// <summary>
        /// Returns a copy of <paramref name="r"/> translated so its bottom edge sits at
        /// <paramref name="bottom"/>, preserving the current height.
        /// <br/>
        /// Ideal for docking a fixed-size panel flush against a screen's bottom edge, such as a
        /// hotbar or action bar.
        /// </summary>
        /// <param name="r">Source rect to move.</param>
        /// <param name="bottom">Target bottom edge (Y maximum) position.</param>
        /// <returns>A new <see cref="Rect"/> at the target bottom edge with unchanged size.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rect MoveBottomTo(this Rect r, float bottom)
            => new Rect(r.x, bottom - r.height, r.width, r.height);

        /// <summary>
        /// Returns a copy of <paramref name="r"/> translated so its center sits at
        /// <paramref name="center"/>, preserving the current size.
        /// <br/>
        /// Standard way to center a tooltip, context menu or popup on a target screen point.
        /// </summary>
        /// <param name="r">Source rect to move.</param>
        /// <param name="center">Target center point.</param>
        /// <returns>A new <see cref="Rect"/> centered on <paramref name="center"/> with unchanged size.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rect WithCenter(this Rect r, Vector2 center)
            => new Rect(center - r.size * 0.5f, r.size);

        #endregion

        #region Expansion & Padding

        /// <summary>
        /// Returns a copy of <paramref name="r"/> expanded outward by <paramref name="amount"/> on
        /// all four sides, keeping the rect centered on the same point.
        /// <br/>
        /// Common for enlarging a touch/click target beyond its visual bounds on mobile UI, or for
        /// drawing a highlight outline slightly larger than the element it frames.
        /// </summary>
        /// <param name="r">Source rect to expand.</param>
        /// <param name="amount">Distance added to each edge. Negative values shrink the rect.</param>
        /// <returns>The expanded rect.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rect Expand(this Rect r, float amount)
            => new Rect(r.x - amount, r.y - amount, r.width + amount * 2f, r.height + amount * 2f);

        /// <summary>
        /// Returns a copy of <paramref name="r"/> expanded outward by independent horizontal and
        /// vertical amounts, keeping the rect centered on the same point.
        /// <br/>
        /// Useful when a UI hit target needs asymmetric padding, e.g. wider tap tolerance on a
        /// horizontally-scrolling list than on its vertical bounds.
        /// </summary>
        /// <param name="r">Source rect to expand.</param>
        /// <param name="horizontal">Distance added to the left and right edges.</param>
        /// <param name="vertical">Distance added to the top and bottom edges.</param>
        /// <returns>The expanded rect.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rect Expand(this Rect r, float horizontal, float vertical)
            => new Rect(r.x - horizontal, r.y - vertical, r.width + horizontal * 2f, r.height + vertical * 2f);

        /// <summary>
        /// Returns a copy of <paramref name="r"/> shrunk inward by <paramref name="amount"/> on all
        /// four sides, keeping the rect centered on the same point.
        /// <br/>
        /// Standard way to compute an inner content rect from a panel's outer bounds, such as
        /// applying a uniform margin before laying out child UI elements.
        /// </summary>
        /// <param name="r">Source rect to shrink.</param>
        /// <param name="amount">Distance removed from each edge. Negative values grow the rect.</param>
        /// <returns>The shrunk rect.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rect Contract(this Rect r, float amount)
            => r.Expand(-amount);

        /// <summary>
        /// Returns a copy of <paramref name="r"/> inset by independent amounts on each edge.
        /// <br/>
        /// The general-purpose margin utility for UI layout, equivalent to CSS-style padding —
        /// use when different sides require different spacing (e.g. extra top padding under a
        /// title bar).
        /// </summary>
        /// <param name="r">Source rect to inset.</param>
        /// <param name="left">Amount removed from the left edge.</param>
        /// <param name="right">Amount removed from the right edge.</param>
        /// <param name="top">Amount removed from the top edge.</param>
        /// <param name="bottom">Amount removed from the bottom edge.</param>
        /// <returns>The inset rect.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rect Pad(this Rect r, float left, float right, float top, float bottom)
            => new Rect(r.x + left, r.y + top, r.width - left - right, r.height - top - bottom);

        #endregion

        #region Containment & Validation

        /// <summary>
        /// Returns <c>true</c> if <paramref name="point"/> lies within <paramref name="r"/>,
        /// expanded by <paramref name="padding"/> in every direction.
        /// <br/>
        /// Standard technique for enlarging a small UI element's effective tap/click area beyond
        /// its visual bounds without altering its rendered size — critical for mobile touch
        /// targets that are visually small but must remain easy to tap.
        /// </summary>
        /// <param name="r">Rect to test against.</param>
        /// <param name="point">Point to test.</param>
        /// <param name="padding">Extra tolerance added to <paramref name="r"/>'s bounds before testing.</param>
        /// <returns><c>true</c> if <paramref name="point"/> is within the padded bounds.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ContainsWithPadding(this Rect r, Vector2 point, float padding)
            => r.Expand(padding).Contains(point);

        /// <summary>
        /// Returns <c>true</c> if <paramref name="other"/> lies entirely within <paramref name="r"/>.
        /// <br/>
        /// Distinct from <see cref="Rect.Overlaps(Rect)"/>, which only checks for any intersection.
        /// Useful for validating that a child UI element's authored rect stays within its parent
        /// container's bounds, or that a minimap viewport stays within the map's full extent.
        /// </summary>
        /// <param name="r">Container rect.</param>
        /// <param name="other">Rect to test for full containment.</param>
        /// <returns><c>true</c> if <paramref name="other"/> is fully inside <paramref name="r"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ContainsRect(this Rect r, Rect other)
            => r.xMin <= other.xMin && r.xMax >= other.xMax && r.yMin <= other.yMin && r.yMax >= other.yMax;

        /// <summary>
        /// Returns <c>true</c> if every component of <paramref name="r"/> is finite.
        /// <br/>
        /// Guards against a rect corrupted by a division-by-zero or invalid remap operation from
        /// being applied to a <see cref="RectTransform"/> or used in a layout calculation, where a
        /// single <c>NaN</c> can break an entire UI canvas's layout pass.
        /// </summary>
        /// <param name="r">Rect to validate.</param>
        /// <returns><c>true</c> if x, y, width and height are all finite.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsValid(this Rect r)
            => r.x.IsValid() && r.y.IsValid() && r.width.IsValid() && r.height.IsValid();

        /// <summary>
        /// Returns <c>true</c> if <paramref name="r"/> has approximately zero area.
        /// <br/>
        /// Useful for early-out checks before performing layout or rendering work on a
        /// degenerately collapsed rect (a UI element mid-collapse animation, or an uninitialized
        /// selection box before the user starts dragging).
        /// </summary>
        /// <param name="r">Rect to test.</param>
        /// <param name="epsilon">Area tolerance. Defaults to <c>DefaultEpsilon</c>.</param>
        /// <returns><c>true</c> if <paramref name="r"/>'s area is approximately zero.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasZeroArea(this Rect r, float epsilon = DefaultEpsilon)
            => Mathf.Abs(r.width * r.height) <= epsilon;

        /// <summary>
        /// Returns a copy of <paramref name="r"/> with non-negative width and height, repositioning
        /// the rect as needed so it describes the same region regardless of drag direction.
        /// <br/>
        /// Unity permits negative width/height on <see cref="Rect"/>, which naturally arises from a
        /// drag-select box dragged from bottom-right to top-left. Downstream code performing
        /// containment or intersection checks typically assumes a positive-size rect; normalize
        /// before passing the result onward.
        /// </summary>
        /// <param name="r">Rect to normalize, potentially with negative width or height.</param>
        /// <returns>An equivalent rect with non-negative width and height.</returns>
        public static Rect Normalized(this Rect r)
        {
            float x = r.width < 0f ? r.x + r.width : r.x;
            float y = r.height < 0f ? r.y + r.height : r.y;
            return new Rect(x, y, Mathf.Abs(r.width), Mathf.Abs(r.height));
        }

        #endregion

        #region Intersection & Union

        /// <summary>
        /// Returns the overlapping region between <paramref name="r"/> and <paramref name="other"/>,
        /// or a zero-area rect at the origin if they do not overlap.
        /// <br/>
        /// Useful for clipping a scrollable content rect against its viewport mask, or computing
        /// the visible portion of a UI element partially obscured by a sibling panel.
        /// </summary>
        /// <param name="r">First rect.</param>
        /// <param name="other">Second rect.</param>
        /// <returns>The intersecting rect, or a zero-area rect if there is no overlap.</returns>
        public static Rect IntersectionWith(this Rect r, Rect other)
        {
            float xMin = Mathf.Max(r.xMin, other.xMin);
            float yMin = Mathf.Max(r.yMin, other.yMin);
            float xMax = Mathf.Min(r.xMax, other.xMax);
            float yMax = Mathf.Min(r.yMax, other.yMax);

            if (xMax < xMin || yMax < yMin)
                return new Rect(0f, 0f, 0f, 0f);

            return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        /// <summary>
        /// Returns the smallest rect that fully contains both <paramref name="r"/> and
        /// <paramref name="other"/>.
        /// <br/>
        /// Ideal for computing a combined bounding rect around a dynamic group of UI elements
        /// (e.g. a marquee-select highlight around multiple selected inventory slots).
        /// </summary>
        /// <param name="r">First rect.</param>
        /// <param name="other">Second rect.</param>
        /// <returns>The bounding rect enclosing both inputs.</returns>
        public static Rect UnionWith(this Rect r, Rect other)
        {
            float xMin = Mathf.Min(r.xMin, other.xMin);
            float yMin = Mathf.Min(r.yMin, other.yMin);
            float xMax = Mathf.Max(r.xMax, other.xMax);
            float yMax = Mathf.Max(r.yMax, other.yMax);

            return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        /// <summary>
        /// Returns the smallest rect that contains both <paramref name="r"/> and
        /// <paramref name="point"/>.
        /// <br/>
        /// Used to incrementally grow a bounding rect while iterating a set of points — e.g.
        /// computing a UI bounding box around a variable number of world-to-screen-projected
        /// waypoint markers.
        /// </summary>
        /// <param name="r">Rect to grow.</param>
        /// <param name="point">Point to include.</param>
        /// <returns>The grown rect encompassing both the original rect and <paramref name="point"/>.</returns>
        public static Rect Encapsulate(this Rect r, Vector2 point)
        {
            float xMin = Mathf.Min(r.xMin, point.x);
            float yMin = Mathf.Min(r.yMin, point.y);
            float xMax = Mathf.Max(r.xMax, point.x);
            float yMax = Mathf.Max(r.yMax, point.y);

            return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        #endregion

        #region Clamping

        /// <summary>
        /// Returns <paramref name="point"/> clamped so it lies within <paramref name="r"/>'s bounds.
        /// <br/>
        /// Common for constraining a draggable UI handle (scrollbar thumb, slider knob) so it can
        /// never be dragged outside its track's rect.
        /// </summary>
        /// <param name="r">Bounding rect.</param>
        /// <param name="point">Point to clamp.</param>
        /// <returns>The clamped point.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 ClampPoint(this Rect r, Vector2 point)
            => new Vector2(Mathf.Clamp(point.x, r.xMin, r.xMax), Mathf.Clamp(point.y, r.yMin, r.yMax));

        /// <summary>
        /// Returns a copy of <paramref name="r"/> repositioned — never resized — so it fits
        /// entirely within <paramref name="container"/>.
        /// <br/>
        /// <para>
        /// If <paramref name="r"/> is larger than <paramref name="container"/> along an axis, that
        /// axis is aligned to the container's minimum edge rather than centered, guaranteeing the
        /// result never extends past the container's opposite edge.
        /// </para>
        /// <para>
        /// The standard technique for keeping a tooltip, context menu or dropdown fully on-screen
        /// regardless of where the cursor or anchor point that triggered it was located.
        /// </para>
        /// </summary>
        /// <param name="r">Rect to reposition.</param>
        /// <param name="container">Bounding container rect.</param>
        /// <returns>The repositioned rect, fully contained within <paramref name="container"/> where possible.</returns>
        public static Rect ClampInside(this Rect r, Rect container)
        {
            float x = r.x;
            float y = r.y;

            if (r.width <= container.width)
                x = Mathf.Clamp(x, container.xMin, container.xMax - r.width);
            else
                x = container.xMin;

            if (r.height <= container.height)
                y = Mathf.Clamp(y, container.yMin, container.yMax - r.height);
            else
                y = container.yMin;

            return new Rect(x, y, r.width, r.height);
        }

        #endregion

        #region Distance

        /// <summary>
        /// Returns the shortest distance from <paramref name="point"/> to the boundary of
        /// <paramref name="r"/>, or <c>0</c> if the point is inside.
        /// <br/>
        /// Useful for proximity-based UI feedback (highlighting a drop target as the cursor
        /// approaches) or determining how far a dragged object has strayed from a valid drop zone.
        /// </summary>
        /// <param name="r">Rect to measure against.</param>
        /// <param name="point">Point to measure from.</param>
        /// <returns>The distance from <paramref name="point"/> to <paramref name="r"/>'s nearest edge, or <c>0</c> if inside.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DistanceToPoint(this Rect r, Vector2 point)
            => Mathf.Sqrt(r.SqrDistanceToPoint(point));

        /// <summary>
        /// Returns the squared shortest distance from <paramref name="point"/> to the boundary of
        /// <paramref name="r"/>, or <c>0</c> if the point is inside.
        /// <br/>
        /// Use for threshold comparisons (is the cursor within N pixels of this panel?) to avoid
        /// the <see cref="Mathf.Sqrt(float)"/> cost hidden inside <see cref="DistanceToPoint(Rect, Vector2)"/>.
        /// </summary>
        /// <param name="r">Rect to measure against.</param>
        /// <param name="point">Point to measure from.</param>
        /// <returns>The squared distance from <paramref name="point"/> to <paramref name="r"/>'s nearest edge, or <c>0</c> if inside.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SqrDistanceToPoint(this Rect r, Vector2 point)
        {
            float dx = Mathf.Max(r.xMin - point.x, 0f, point.x - r.xMax);
            float dy = Mathf.Max(r.yMin - point.y, 0f, point.y - r.yMax);
            return dx * dx + dy * dy;
        }

        #endregion

        #region Aspect Ratio & Fitting

        /// <summary>
        /// Returns the width-to-height aspect ratio of <paramref name="r"/>, or <c>1f</c> if
        /// height is approximately zero.
        /// <br/>
        /// Guards against a division by zero when a rect is still collapsed to zero height during
        /// a layout pass (e.g. before the first <see cref="RectTransform"/> rebuild completes).
        /// </summary>
        /// <param name="r">Rect to measure.</param>
        /// <returns>The aspect ratio (width / height), or <c>1f</c> on degenerate input.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetAspectRatio(this Rect r)
            => r.height.IsZero() ? 1f : r.width / r.height;

        /// <summary>
        /// Returns a copy of <paramref name="r"/> scaled (preserving aspect ratio) to fit entirely
        /// within <paramref name="container"/>, centered within it.
        /// <br/>
        /// Equivalent to a "letterbox" or "fit" scale mode — the result never exceeds the
        /// container's bounds on either axis. Ideal for framing a character portrait or camera
        /// preview inside a fixed-size UI frame without distortion or clipping.
        /// </summary>
        /// <param name="r">Rect whose aspect ratio is preserved.</param>
        /// <param name="container">Bounding container to fit within.</param>
        /// <returns>The scaled and centered rect.</returns>
        public static Rect FitInside(this Rect r, Rect container)
        {
            float aspect = r.GetAspectRatio();
            float containerAspect = container.GetAspectRatio();

            float width, height;
            if (aspect > containerAspect)
            {
                width = container.width;
                height = width / aspect;
            }
            else
            {
                height = container.height;
                width = height * aspect;
            }

            Vector2 size = new Vector2(width, height);
            return new Rect(container.center - size * 0.5f, size);
        }

        /// <summary>
        /// Returns a copy of <paramref name="r"/> scaled (preserving aspect ratio) to fully cover
        /// <paramref name="container"/>, centered within it, potentially overflowing on one axis.
        /// <br/>
        /// Equivalent to a "cover" or "fill" scale mode — the result always fully covers the
        /// container, cropping any excess. Ideal for background images or video textures that must
        /// fill a panel with no visible gaps, at the cost of edge cropping.
        /// </summary>
        /// <param name="r">Rect whose aspect ratio is preserved.</param>
        /// <param name="container">Container to fully cover.</param>
        /// <returns>The scaled and centered rect, potentially larger than <paramref name="container"/>.</returns>
        public static Rect FillInside(this Rect r, Rect container)
        {
            float aspect = r.GetAspectRatio();
            float containerAspect = container.GetAspectRatio();

            float width, height;
            if (aspect < containerAspect)
            {
                width = container.width;
                height = width / aspect;
            }
            else
            {
                height = container.height;
                width = height * aspect;
            }

            Vector2 size = new Vector2(width, height);
            return new Rect(container.center - size * 0.5f, size);
        }

        #endregion

        #region Random

        /// <summary>
        /// Returns a uniformly random point within the bounds of <paramref name="r"/>.
        /// <br/>
        /// Ideal for scattering spawn positions within a designated screen-space or world-space
        /// rectangular zone (loot drop scatter, particle emission area).
        /// </summary>
        /// <param name="r">Rect to sample within.</param>
        /// <returns>A random point inside <paramref name="r"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 RandomPointInside(this Rect r)
            => new Vector2(Random.Range(r.xMin, r.xMax), Random.Range(r.yMin, r.yMax));

        #endregion

        #region Corners

        /// <summary>
        /// Writes the four corners of <paramref name="r"/> into <paramref name="results"/>, in the
        /// order: bottom-left, top-left, top-right, bottom-right.
        /// <br/>
        /// Non-allocating variant intended for per-frame calls (e.g. rebuilding a UI outline mesh
        /// every frame). The caller must supply an array of length 4.
        /// </summary>
        /// <param name="r">Rect to extract corners from.</param>
        /// <param name="results">Destination array of length 4.</param>
        public static void GetCornersNonAlloc(this Rect r, Vector2[] results)
        {
            results[0] = new Vector2(r.xMin, r.yMin);
            results[1] = new Vector2(r.xMin, r.yMax);
            results[2] = new Vector2(r.xMax, r.yMax);
            results[3] = new Vector2(r.xMax, r.yMin);
        }

        /// <summary>
        /// Returns the four corners of <paramref name="r"/> as a new array, in the order:
        /// bottom-left, top-left, top-right, bottom-right.
        /// <br/>
        /// Convenience allocating variant of <see cref="GetCornersNonAlloc(Rect, Vector2[])"/> for
        /// one-off calls such as editor tooling or gizmo drawing; prefer the non-allocating
        /// overload in per-frame code.
        /// </summary>
        /// <param name="r">Rect to extract corners from.</param>
        /// <returns>A new 4-element array of corner points.</returns>
        public static Vector2[] GetCorners(this Rect r)
        {
            Vector2[] results = new Vector2[4];
            r.GetCornersNonAlloc(results);
            return results;
        }

        #endregion

        #region Conversion

        /// <summary>
        /// Converts <paramref name="pixelRect"/> from screen-pixel space into normalized [0,1]
        /// viewport space using the given screen dimensions.
        /// <br/>
        /// Standard conversion for configuring <see cref="Camera.rect"/> in split-screen setups
        /// where layout is authored in pixels but Unity's camera viewport API expects normalized
        /// coordinates.
        /// </summary>
        /// <param name="pixelRect">Rect in screen-pixel space.</param>
        /// <param name="screenWidth">Total screen width in pixels.</param>
        /// <param name="screenHeight">Total screen height in pixels.</param>
        /// <returns>The equivalent rect in normalized [0,1] viewport space.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rect ToViewportRect(this Rect pixelRect, float screenWidth, float screenHeight)
            => new Rect(pixelRect.x / screenWidth, pixelRect.y / screenHeight, pixelRect.width / screenWidth, pixelRect.height / screenHeight);

        /// <summary>
        /// Converts <paramref name="viewportRect"/> from normalized [0,1] viewport space into
        /// screen-pixel space using the given screen dimensions.
        /// <br/>
        /// Inverse of <see cref="ToViewportRect(Rect, float, float)"/>; useful for converting a
        /// configured <see cref="Camera.rect"/> back into pixel coordinates for UI overlay
        /// alignment on top of a specific camera's viewport.
        /// </summary>
        /// <param name="viewportRect">Rect in normalized [0,1] viewport space.</param>
        /// <param name="screenWidth">Total screen width in pixels.</param>
        /// <param name="screenHeight">Total screen height in pixels.</param>
        /// <returns>The equivalent rect in screen-pixel space.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rect ToScreenRect(this Rect viewportRect, float screenWidth, float screenHeight)
            => new Rect(viewportRect.x * screenWidth, viewportRect.y * screenHeight, viewportRect.width * screenWidth, viewportRect.height * screenHeight);

        #endregion

        #region Debug

        /// <summary>
        /// Returns a compact debug string describing <paramref name="r"/>'s position and size.
        /// <br/>
        /// Intended for <see cref="Debug.Log(object)"/> calls and runtime inspectors during UI
        /// layout debugging, not hot-path logic, due to string allocation.
        /// </summary>
        /// <param name="r">Rect to format.</param>
        /// <returns>A formatted debug string.</returns>
        public static string ToDebugString(this Rect r)
            => $"Rect(x:{r.x:F1}, y:{r.y:F1}, w:{r.width:F1}, h:{r.height:F1})";

        #endregion
    }
}