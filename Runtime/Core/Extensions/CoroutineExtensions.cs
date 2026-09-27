using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Nexus.Core.Extensions
{
    /// <summary>
    /// Extension methods and static helpers for coroutine <see cref="IEnumerator"/> routines:
    /// cached yield instructions, sequential/parallel composition, and timeout wrapping.
    /// <para>Design rules:</para>
    /// <list type="bullet">
    /// <item>A running <see cref="Coroutine"/> handle is an opaque Unity type with no public
    /// members of its own — it cannot be queried for completion, progress, or its original
    /// routine. Every method in this class therefore operates on the <see cref="IEnumerator"/>
    /// passed to <see cref="MonoBehaviour.StartCoroutine(IEnumerator)"/> <b>before</b> it
    /// starts running, not on the handle returned afterward. Handle-level safety
    /// (start/stop/null-out) lives in <c>MonoBehaviourExtensions</c> instead.</item>
    /// <item><see cref="Sequence"/> and <see cref="ThenRun"/> rely on Unity's built-in support
    /// for yielding a nested <see cref="IEnumerator"/> from within a coroutine — the coroutine
    /// runner recognizes it and runs it to completion before resuming the outer routine. This
    /// is standard, documented Unity behavior and requires no manual pumping.</item>
    /// <item><see cref="WithTimeout"/> is the one exception: racing a routine against a clock
    /// cannot be expressed as a single nested yield, since only one of the two can be "yielded
    /// to" at a time. It works around this by manually driving the inner enumerator's
    /// <see cref="IEnumerator.MoveNext"/> one step per frame instead of yielding it directly.</item>
    /// <item>Composition and timing methods return a freshly allocated <see cref="IEnumerator"/>
    /// state machine, the same deliberate, documented exception to the zero-allocation rule
    /// already made for coroutine-driven fades elsewhere in this library. They run once per
    /// triggered event (a delay, a cutscene beat), not per frame across many instances.</item>
    /// <item>The cached yield instruction dictionaries are plain, non-thread-safe
    /// <see cref="Dictionary{TKey, TValue}"/> instances. Coroutines only ever run on Unity's
    /// main thread, so no locking is required.</item>
    /// </list>
    /// </summary>
    public static class CoroutineExtensions
    {
        /// <summary>
        /// Number of milliseconds in one second, used to convert a wait duration into an
        /// integer cache key so cached <see cref="WaitForSeconds"/> instances can be looked up
        /// without the precision hazards of using a raw <see cref="float"/> as a dictionary key.
        /// </summary>
        private const int MillisecondsPerSecond = 1000;

        #region Cached Yield Instructions

        /// <summary>
        /// A single, permanently reused <see cref="WaitForEndOfFrame"/> instance. <br/>
        /// This yield instruction carries no parameters and is identical on every use, so
        /// there is never a reason to allocate a new one; Unity's own samples reuse a single
        /// instance for exactly this reason.
        /// </summary>
        public static readonly WaitForEndOfFrame EndOfFrame = new WaitForEndOfFrame();

        /// <summary>
        /// A single, permanently reused <see cref="WaitForFixedUpdate"/> instance, for the
        /// same reason as <see cref="EndOfFrame"/>.
        /// </summary>
        public static readonly WaitForFixedUpdate FixedUpdateStep = new WaitForFixedUpdate();

        /// <summary>
        /// Per-duration cache of <see cref="WaitForSeconds"/> instances, keyed by whole
        /// milliseconds. <br/>
        /// Allocating a <c>new WaitForSeconds(x)</c> every time a coroutine loops (e.g. a
        /// spawner waiting the same interval repeatedly) is one of the most common,
        /// completely avoidable sources of steady GC pressure in Unity gameplay code.
        /// </summary>
        private static readonly Dictionary<int, WaitForSeconds> WaitForSecondsCache = new Dictionary<int, WaitForSeconds>();

        /// <summary>
        /// Per-duration cache of <see cref="WaitForSecondsRealtime"/> instances, keyed by
        /// whole milliseconds, mirroring <see cref="WaitForSecondsCache"/>.
        /// </summary>
        private static readonly Dictionary<int, WaitForSecondsRealtime> WaitForSecondsRealtimeCache = new Dictionary<int, WaitForSecondsRealtime>();

        /// <summary>
        /// Returns a cached <see cref="WaitForSeconds"/> for approximately
        /// <paramref name="seconds"/>, allocating and caching a new one only the first time a
        /// given duration (rounded to the nearest millisecond) is requested. <br/>
        /// The millisecond rounding means two calls with, say, <c>1.0001</c> and
        /// <c>1.0004</c> seconds share the same cached instance — an intentional, negligible
        /// precision trade in exchange for eliminating repeated allocation for the overwhelmingly
        /// common case of a fixed or near-fixed wait duration (spawn intervals, cooldown ticks).
        /// <para>
        /// <see cref="WaitForSeconds"/> always uses scaled <see cref="Time.deltaTime"/> and
        /// cannot be paused independently via <see cref="Time.timeScale"/>. For a wait that
        /// must ignore pause/slow-motion, use <see cref="GetCachedWaitForSecondsRealtime"/> or
        /// a manual delta-time accumulation loop (see <c>MonoBehaviourExtensions.RunAfterDelay</c>).
        /// </para>
        /// </summary>
        public static WaitForSeconds GetCachedWaitForSeconds(float seconds)
        {
            int key = Mathf.RoundToInt(Mathf.Max(0f, seconds) * MillisecondsPerSecond);
            if (!WaitForSecondsCache.TryGetValue(key, out WaitForSeconds cached))
            {
                cached = new WaitForSeconds(key / (float)MillisecondsPerSecond);
                WaitForSecondsCache[key] = cached;
            }

            return cached;
        }

        /// <summary>
        /// Returns a cached <see cref="WaitForSecondsRealtime"/> for approximately
        /// <paramref name="seconds"/>, using the same millisecond-rounded caching strategy as
        /// <see cref="GetCachedWaitForSeconds"/>. <br/>
        /// Use for UI countdowns, pause-menu timers, or any wait that must keep counting down
        /// while <see cref="Time.timeScale"/> is zero.
        /// </summary>
        public static WaitForSecondsRealtime GetCachedWaitForSecondsRealtime(float seconds)
        {
            int key = Mathf.RoundToInt(Mathf.Max(0f, seconds) * MillisecondsPerSecond);
            if (!WaitForSecondsRealtimeCache.TryGetValue(key, out WaitForSecondsRealtime cached))
            {
                cached = new WaitForSecondsRealtime(key / (float)MillisecondsPerSecond);
                WaitForSecondsRealtimeCache[key] = cached;
            }

            cached.Reset();
            return cached;
        }

        #endregion

        #region Sequential Composition

        /// <summary>
        /// Wraps <paramref name="routine"/> so that <paramref name="callback"/> runs the
        /// instant it completes. <br/>
        /// Unity coroutines have no built-in completion callback — <see cref="MonoBehaviour.StartCoroutine(IEnumerator)"/>
        /// returns an opaque handle with no "on finished" event. This is the standard way to
        /// bridge that gap: chain a follow-up action (spawn loot after a death animation
        /// finishes, enable input after an intro cutscene ends) without polling a separate
        /// "is it done yet" flag every frame.
        /// </summary>
        public static IEnumerator ThenRun(this IEnumerator routine, Action callback)
        {
            yield return routine;
            callback?.Invoke();
        }

        /// <summary>
        /// Runs each non-null routine in <paramref name="routines"/> to completion, one after
        /// another, in array order. <br/>
        /// The standard technique for scripting a multi-beat cutscene or ability sequence
        /// (move, then attack, then retreat) as a flat, readable list rather than deeply
        /// nested coroutine calls. Relies on Unity's native support for yielding a nested
        /// <see cref="IEnumerator"/>, so no manual pumping is required.
        /// <para>
        /// Accepts a plain array rather than a <c>params</c> parameter so the allocation of
        /// the routines array is always visible at the call site rather than hidden behind
        /// implicit <c>params</c> array construction. This method itself allocates only the
        /// wrapping iterator, matching the documented coroutine-composition exception to the
        /// zero-allocation rule.
        /// </para>
        /// </summary>
        public static IEnumerator Sequence(IEnumerator[] routines)
        {
            if (routines == null) yield break;

            for (int i = 0; i < routines.Length; i++)
            {
                if (routines[i] != null) yield return routines[i];
            }
        }

        #endregion

        #region Timeout Wrapping

        /// <summary>
        /// Runs <paramref name="routine"/> but abandons it if it has not completed within
        /// <paramref name="timeoutSeconds"/>, invoking <paramref name="onTimeout"/> instead. <br/>
        /// Essential for any wait tied to an external or unreliable condition — waiting for a
        /// network response, a matchmaking result, or an asset to finish loading — where the
        /// underlying operation might simply never complete and would otherwise leave the
        /// coroutine (and anything waiting on it) stuck forever.
        /// <para>
        /// Because only one enumerator can be yielded to at a time, this cannot be expressed
        /// as a simple nested <c>yield return routine</c> the way <see cref="Sequence"/> and
        /// <see cref="ThenRun"/> are. Instead it manually calls <paramref name="routine"/>'s
        /// <see cref="IEnumerator.MoveNext"/> once per frame, accumulating elapsed time
        /// alongside it, and re-yields whatever the inner routine yielded so its own internal
        /// wait behavior (waiting a number of frames, waiting for seconds, waiting for a
        /// nested routine) is preserved.
        /// </para>
        /// </summary>
        public static IEnumerator WithTimeout(this IEnumerator routine, float timeoutSeconds, Action onTimeout = null, bool useUnscaledTime = false)
        {
            float elapsed = 0f;

            while (routine.MoveNext())
            {
                elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                if (elapsed >= timeoutSeconds)
                {
                    onTimeout?.Invoke();
                    yield break;
                }

                yield return routine.Current;
            }
        }

        #endregion
    }
}