using System.Collections;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Nexus.Core.Extensions
{
    /// <summary>
    /// Extension methods for <see cref="CanvasGroup"/> covering visibility state, alpha
    /// control, interaction locking, and coroutine-driven fades.
    /// <para>Design rules:</para>
    /// <list type="bullet">
    /// <item><see cref="CanvasGroup.alpha"/> alone does not stop input. A group faded to
    /// <c>0</c> still blocks raycasts and still accepts interaction unless
    /// <see cref="CanvasGroup.blocksRaycasts"/> and <see cref="CanvasGroup.interactable"/>
    /// are explicitly cleared. Every visibility helper here sets all three properties
    /// together to avoid the classic "invisible panel eats my click" bug.</item>
    /// <item>Alpha setters clamp to <c>0</c>–<c>1</c> and reject <c>NaN</c>. A <c>NaN</c>
    /// alpha silently disables the entire canvas render for that group with no console
    /// warning, which is a difficult bug to trace back to its source.</item>
    /// <item>The <c>FadeTo</c>/<c>FadeIn</c>/<c>FadeOut</c> methods return
    /// <see cref="IEnumerator"/> for use with <see cref="MonoBehaviour.StartCoroutine(IEnumerator)"/>.
    /// This is the one deliberate exception to the zero-allocation rule in this library:
    /// a C# iterator state machine is allocated per call. UI fades run once per transition,
    /// not per frame across hundreds of instances, so this cost is acceptable where it
    /// would not be in a physics or math helper.</item>
    /// <item>Interaction-locking helpers distinguish "locked" (still blocks clicks, e.g. a
    /// busy overlay) from "click-through" (fully passive, e.g. a decorative vignette),
    /// since conflating the two is a common source of unintentionally unclickable UI.</item>
    /// </list>
    /// </summary>
    public static class CanvasGroupExtensions
    {
        /// <summary>
        /// Alpha value representing a fully visible group.
        /// </summary>
        private const float FullyVisibleAlpha = 1f;

        /// <summary>
        /// Alpha value representing a fully hidden group.
        /// </summary>
        private const float FullyHiddenAlpha = 0f;

        /// <summary>
        /// Tolerance used when comparing alpha against <see cref="FullyVisibleAlpha"/> or
        /// <see cref="FullyHiddenAlpha"/>, avoiding false negatives from floating-point
        /// fade results that land at <c>0.999998</c> instead of exactly <c>1</c>.
        /// </summary>
        private const float VisibilityEpsilon = 1e-3f;

        #region Visibility State

        /// <summary>
        /// Makes the group fully visible and, by default, interactable. <br/>
        /// Sets <see cref="CanvasGroup.alpha"/>, <see cref="CanvasGroup.interactable"/>, and
        /// <see cref="CanvasGroup.blocksRaycasts"/> together so a shown panel is guaranteed
        /// to actually receive input, rather than silently remaining unclickable because
        /// only alpha was changed elsewhere.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Show(this CanvasGroup group, bool interactable = true)
        {
            group.alpha = FullyVisibleAlpha;
            group.interactable = interactable;
            group.blocksRaycasts = interactable;
        }

        /// <summary>
        /// Makes the group fully invisible, non-interactable, and click-through. <br/>
        /// Clearing <see cref="CanvasGroup.blocksRaycasts"/> is the part most manual code
        /// forgets: without it, a hidden panel keeps intercepting clicks meant for the UI
        /// behind it, producing "dead zones" that are notoriously hard to diagnose.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Hide(this CanvasGroup group)
        {
            group.alpha = FullyHiddenAlpha;
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        /// <summary>
        /// Calls <see cref="Show"/> or <see cref="Hide"/> based on <paramref name="visible"/>. <br/>
        /// Lets menu and panel controllers drive visibility from a single bound bool
        /// (e.g. a settings toggle or a state machine flag) without an <c>if</c>/<c>else</c>
        /// at every call site.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetVisible(this CanvasGroup group, bool visible)
        {
            if (visible) group.Show();
            else group.Hide();
        }

        /// <summary>
        /// Flips the group between <see cref="Show"/> and <see cref="Hide"/> based on its
        /// current alpha. <br/>
        /// Convenient for a single keybind or button that opens and closes the same panel,
        /// such as an inventory or pause menu, without external state tracking.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ToggleVisibility(this CanvasGroup group)
            => group.SetVisible(!group.IsVisible());

        /// <summary>
        /// True if the group has any non-zero alpha. <br/>
        /// Use for cheap "is this panel currently shown at all" checks, such as skipping
        /// update logic for a fully hidden tooltip.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsVisible(this CanvasGroup group) => group.alpha > VisibilityEpsilon;

        /// <summary>
        /// True if the group's alpha is at (or effectively at) <c>1</c>. <br/>
        /// Distinguishes "finished fading in" from "currently fading in" — useful for
        /// gating input or animation triggers until a transition has fully completed.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFullyVisible(this CanvasGroup group)
            => group.alpha >= FullyVisibleAlpha - VisibilityEpsilon;

        /// <summary>
        /// True if the group's alpha is at (or effectively at) <c>0</c>. <br/>
        /// The correct check for "safe to return this panel to a pool" — unlike
        /// <see cref="IsVisible"/>'s negation, this tolerates leftover floating-point
        /// fade error instead of requiring an exact zero.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFullyHidden(this CanvasGroup group)
            => group.alpha <= FullyHiddenAlpha + VisibilityEpsilon;

        #endregion

        #region Alpha Control

        /// <summary>
        /// Sets <see cref="CanvasGroup.alpha"/>, clamped to <c>0</c>–<c>1</c> and rejecting
        /// <c>NaN</c>. <br/>
        /// A raw <c>NaN</c> assignment to <see cref="CanvasGroup.alpha"/> silently makes the
        /// group invisible with no console warning; guarding here stops a bad interpolation
        /// value (e.g. from a divide-by-zero in a custom tween) from ever reaching the UI.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetAlphaClamped(this CanvasGroup group, float alpha)
        {
            if (float.IsNaN(alpha)) return;
            group.alpha = Mathf.Clamp01(alpha);
        }

        /// <summary>
        /// Adds <paramref name="delta"/> to the group's current alpha, clamped to
        /// <c>0</c>–<c>1</c>. <br/>
        /// Drives manual per-frame fades (<c>group.AddAlpha(fadeSpeed * Time.deltaTime)</c>)
        /// without external tweening libraries, while still guaranteeing the result never
        /// leaves the valid alpha range.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddAlpha(this CanvasGroup group, float delta)
        {
            if (float.IsNaN(delta)) return;
            group.alpha = Mathf.Clamp01(group.alpha + delta);
        }

        #endregion

        #region Interaction Control

        /// <summary>
        /// Sets <see cref="CanvasGroup.interactable"/> and <see cref="CanvasGroup.blocksRaycasts"/>
        /// together: <c>true</c> restores full interaction, <c>false</c> disables input
        /// while the group stays visible and still blocks clicks. <br/>
        /// The standard pattern for a "busy" overlay — a save/loading spinner, for example —
        /// that must remain visible and swallow input without fading anything out.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetInteractionLocked(this CanvasGroup group, bool locked)
        {
            group.interactable = !locked;
            group.blocksRaycasts = true;
        }

        /// <summary>
        /// True if the group is visible but currently rejecting interaction while still
        /// blocking clicks (the state produced by <c>SetInteractionLocked(true)</c>). <br/>
        /// Lets input-handling code distinguish "temporarily locked" panels from normally
        /// interactive ones without duplicating the underlying property comparison.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsInteractionLocked(this CanvasGroup group)
            => !group.interactable && group.blocksRaycasts;

        /// <summary>
        /// Sets both <see cref="CanvasGroup.interactable"/> and <see cref="CanvasGroup.blocksRaycasts"/>
        /// to <c>false</c> when <paramref name="clickThrough"/> is <c>true</c>, letting
        /// clicks pass entirely through the group to whatever sits beneath it, or restores
        /// both to <c>true</c> otherwise. <br/>
        /// Used for purely decorative overlays — vignettes, color-grade panels, screen-space
        /// damage flashes — that must never intercept input meant for gameplay or UI below them.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetClickThrough(this CanvasGroup group, bool clickThrough)
        {
            group.interactable = !clickThrough;
            group.blocksRaycasts = !clickThrough;
        }

        /// <summary>
        /// True if the group neither accepts interaction nor blocks raycasts (the state
        /// produced by <c>SetClickThrough(true)</c>). <br/>
        /// Useful for editor tooling or automated tests that need to assert a decorative
        /// overlay is not accidentally intercepting input.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsClickThrough(this CanvasGroup group)
            => !group.interactable && !group.blocksRaycasts;

        /// <summary>
        /// Sets <see cref="CanvasGroup.ignoreParentGroups"/>. <br/>
        /// Lets a specific element — a tooltip, a context menu, an always-on debug HUD —
        /// stay interactive even when every ancestor <see cref="CanvasGroup"/> in the
        /// hierarchy has been faded out or locked.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetIgnoreParentGroups(this CanvasGroup group, bool ignore)
            => group.ignoreParentGroups = ignore;

        #endregion

        #region Fade Transitions

        /// <summary>
        /// Coroutine that linearly interpolates <see cref="CanvasGroup.alpha"/> from its
        /// current value to <paramref name="targetAlpha"/> over <paramref name="duration"/>
        /// seconds. <br/>
        /// Run with <see cref="MonoBehaviour.StartCoroutine(IEnumerator)"/>. Non-finite or
        /// non-positive durations, and non-finite target values, resolve the alpha
        /// immediately instead of dividing by zero or looping forever.
        /// <para>
        /// Only touches <see cref="CanvasGroup.alpha"/> — interaction state is left
        /// untouched. Prefer <see cref="FadeIn"/>/<see cref="FadeOut"/> for UI panels that
        /// should also stop accepting or blocking input at the correct point in the transition.
        /// </para>
        /// </summary>
        public static IEnumerator FadeTo(this CanvasGroup group, float targetAlpha, float duration, bool useUnscaledTime = false)
        {
            float target = float.IsNaN(targetAlpha) ? group.alpha : Mathf.Clamp01(targetAlpha);

            if (float.IsNaN(duration) || duration <= 0f)
            {
                group.alpha = target;
                yield break;
            }

            float start = group.alpha;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                group.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            group.alpha = target;
        }

        /// <summary>
        /// Coroutine that enables interaction immediately, then fades the group's alpha to
        /// <c>1</c> over <paramref name="duration"/> seconds. <br/>
        /// Enabling <see cref="CanvasGroup.interactable"/> and <see cref="CanvasGroup.blocksRaycasts"/>
        /// before the fade starts, rather than after it ends, lets impatient players click
        /// a button the instant it becomes legible instead of waiting for the animation to
        /// fully settle.
        /// </summary>
        public static IEnumerator FadeIn(this CanvasGroup group, float duration, bool useUnscaledTime = false)
        {
            group.interactable = true;
            group.blocksRaycasts = true;
            yield return group.FadeTo(FullyVisibleAlpha, duration, useUnscaledTime);
        }

        /// <summary>
        /// Coroutine that disables interaction immediately, fades the group's alpha to
        /// <c>0</c> over <paramref name="duration"/> seconds, then clears
        /// <see cref="CanvasGroup.blocksRaycasts"/>. <br/>
        /// Disabling <see cref="CanvasGroup.interactable"/> up front stops a player from
        /// clicking a button that is already visually fading away. <see cref="CanvasGroup.blocksRaycasts"/>
        /// is kept <c>true</c> until the fade completes, so the disappearing panel still
        /// shields whatever is behind it until it is actually gone, then release input
        /// through to the scene beneath it.
        /// </summary>
        public static IEnumerator FadeOut(this CanvasGroup group, float duration, bool useUnscaledTime = false)
        {
            group.interactable = false;
            yield return group.FadeTo(FullyHiddenAlpha, duration, useUnscaledTime);
            group.blocksRaycasts = false;
        }

        #endregion
    }
}