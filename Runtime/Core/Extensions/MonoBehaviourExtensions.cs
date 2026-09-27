using System;
using System.Collections;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Nexus.Core.Extensions
{
    /// <summary>
    /// Extension methods for <see cref="MonoBehaviour"/> covering destroyed-object safety,
    /// safe coroutine start/stop, delayed and conditional scheduling, parallel coroutine
    /// composition, and guarded <see cref="MonoBehaviour.Invoke(string, float)"/> access.
    /// <para>Design rules:</para>
    /// <list type="bullet">
    /// <item>Unity overloads <c>==</c> on <see cref="UnityEngine.Object"/> so that a component
    /// whose native object has been destroyed compares equal to <c>null</c>, even though the
    /// managed C# reference itself is not actually <c>null</c>
    /// (<see cref="object.ReferenceEquals(object, object)"/> on it would return <c>false</c>).
    /// <see cref="IsDestroyed"/> exists to make that Unity-specific check self-documenting at
    /// call sites, instead of a bare <c>mb == null</c> that reads as an ordinary reference check.</item>
    /// <item><see cref="MonoBehaviour.StartCoroutine(IEnumerator)"/> throws if the
    /// <see cref="GameObject"/> is inactive in the hierarchy, but — unlike many assume — does
    /// <b>not</b> require the component itself to be <see cref="Behaviour.enabled"/>. Every
    /// scheduling method here checks <see cref="GameObject.activeInHierarchy"/> specifically,
    /// not <see cref="Behaviour.isActiveAndEnabled"/>, to avoid rejecting a perfectly valid
    /// coroutine start on a disabled script.</item>
    /// <item>A stopped or naturally-completed <see cref="Coroutine"/> handle does not become
    /// <c>null</c> on its own — the field holding it stays set to a stale, non-null reference
    /// forever unless the caller clears it. Checking <c>myCoroutine != null</c> to mean "is
    /// currently running" is a common resulting bug. <see cref="StopCoroutineSafe"/> takes the
    /// handle <c>by ref</c> specifically so it can null the field out as part of stopping it.</item>
    /// <item>Delay-based scheduling methods use a manual per-frame accumulation loop rather
    /// than <see cref="WaitForSeconds"/>, matching the fade-coroutine pattern used elsewhere in
    /// this library, specifically so an optional <c>useUnscaledTime</c> parameter can be
    /// honored — something <see cref="WaitForSeconds"/> cannot do.</item>
    /// <item><see cref="MonoBehaviour.Invoke(string, float)"/> and
    /// <see cref="MonoBehaviour.InvokeRepeating(string, float, float)"/> resolve their target
    /// method by string via reflection every call, which is markedly slower than a coroutine
    /// or a direct delegate call. The safe wrappers here guard the common failure modes
    /// (negative delay, missing method name) but do not remove that underlying cost —
    /// prefer <see cref="RunAfterDelay"/> for new code.</item>
    /// </list>
    /// </summary>
    public static class MonoBehaviourExtensions
    {
        #region Lifecycle & Destruction Safety

        /// <summary>
        /// True if this component's underlying native Unity object has been destroyed. <br/>
        /// See the class-level remarks for why this differs from a plain reference-null
        /// check: it exploits (and documents) Unity's overridden <c>==</c> operator rather
        /// than relying on callers to know that nuance themselves.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsDestroyed(this MonoBehaviour mb) => mb == null;

        /// <summary>
        /// True if this component still exists, is enabled, and its <see cref="GameObject"/>
        /// is active in the hierarchy. <br/>
        /// Combines the destroyed-check with <see cref="Behaviour.isActiveAndEnabled"/> into
        /// the single question most gameplay code actually means when it asks "can I safely
        /// interact with this behaviour right now" — for example, before invoking a public
        /// method on a reference cached earlier in the frame that may have been destroyed or
        /// disabled since.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsAvailable(this MonoBehaviour mb) => mb != null && mb.isActiveAndEnabled;

        /// <summary>
        /// True if this component exists and its <see cref="GameObject"/> is active in the
        /// hierarchy, without requiring the component itself to be enabled. <br/>
        /// This is the exact precondition <see cref="MonoBehaviour.StartCoroutine(IEnumerator)"/>
        /// requires — a coroutine can be started on a disabled script as long as its
        /// GameObject is active — which is why every scheduling method in this class checks
        /// this instead of <see cref="IsAvailable"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CanStartCoroutine(this MonoBehaviour mb) => mb != null && mb.gameObject.activeInHierarchy;

        #endregion

        #region Safe Coroutine Start & Stop

        /// <summary>
        /// Starts <paramref name="routine"/>, returning <c>null</c> instead of throwing if the
        /// component is destroyed, <paramref name="routine"/> is <c>null</c>, or the
        /// <see cref="GameObject"/> is inactive. <br/>
        /// Turns the "Coroutine couldn't be started because the game object is inactive"
        /// exception — a frequent runtime crash when starting a coroutine from code that
        /// doesn't control the object's active state, such as an event callback firing after
        /// the object was deactivated earlier the same frame — into an inspectable <c>null</c> return.
        /// </summary>
        public static Coroutine StartCoroutineSafe(this MonoBehaviour mb, IEnumerator routine)
        {
            if (!mb.CanStartCoroutine() || routine == null) return null;
            return mb.StartCoroutine(routine);
        }

        /// <summary>
        /// Stops <paramref name="coroutine"/> if it is currently non-null and the component
        /// still exists, then sets <paramref name="coroutine"/> to <c>null</c>. <br/>
        /// Taking the handle <c>by ref</c> and clearing it is the key detail: without it, the
        /// field would keep referencing a now-stopped (or already-completed) coroutine,
        /// and later code checking <c>coroutine != null</c> to mean "still running" would be
        /// silently wrong. This is the standard pattern for a cancellable single-shot
        /// coroutine field on a component (an active ability cooldown, a queued respawn).
        /// </summary>
        public static void StopCoroutineSafe(this MonoBehaviour mb, ref Coroutine coroutine)
        {
            if (coroutine != null && mb != null) mb.StopCoroutine(coroutine);
            coroutine = null;
        }

        /// <summary>
        /// Stops every coroutine running on this component, doing nothing if it has already
        /// been destroyed. <br/>
        /// Thin null-safe wrapper around <see cref="MonoBehaviour.StopAllCoroutines"/> for
        /// cleanup paths (pooling, scene teardown) that cannot guarantee the target component
        /// is still alive when cleanup runs.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StopAllCoroutinesSafe(this MonoBehaviour mb)
        {
            if (mb != null) mb.StopAllCoroutines();
        }

        #endregion

        #region Delayed & Scheduled Execution

        /// <summary>
        /// Invokes <paramref name="callback"/> after <paramref name="seconds"/> have elapsed,
        /// returning <c>null</c> instead of starting anything if the component cannot
        /// currently start a coroutine or <paramref name="callback"/> is <c>null</c>. <br/>
        /// Uses a manual per-frame accumulation loop rather than <see cref="WaitForSeconds"/>
        /// specifically so <paramref name="useUnscaledTime"/> can be honored — a hit-stop
        /// effect or a pause-menu countdown needs to keep ticking while
        /// <see cref="Time.timeScale"/> is altered or zeroed, which <see cref="WaitForSeconds"/>
        /// cannot do.
        /// </summary>
        public static Coroutine RunAfterDelay(this MonoBehaviour mb, float seconds, Action callback, bool useUnscaledTime = false)
        {
            if (!mb.CanStartCoroutine() || callback == null) return null;
            return mb.StartCoroutine(DelayRoutine(seconds, callback, useUnscaledTime));
        }

        private static IEnumerator DelayRoutine(float seconds, Action callback, bool useUnscaledTime)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                yield return null;
            }

            callback();
        }

        /// <summary>
        /// Invokes <paramref name="callback"/> after <paramref name="frameCount"/> frames have
        /// passed (minimum <c>1</c>), returning <c>null</c> if the component cannot currently
        /// start a coroutine or <paramref name="callback"/> is <c>null</c>. <br/>
        /// Useful for logic that must wait for a fixed number of render frames rather than a
        /// wall-clock duration — for example waiting exactly one frame for a
        /// <see cref="Canvas"/> layout rebuild to finish before measuring a
        /// <see cref="RectTransform"/>.
        /// </summary>
        public static Coroutine RunAfterFrames(this MonoBehaviour mb, int frameCount, Action callback)
        {
            if (!mb.CanStartCoroutine() || callback == null) return null;
            return mb.StartCoroutine(FramesRoutine(Mathf.Max(1, frameCount), callback));
        }

        private static IEnumerator FramesRoutine(int frameCount, Action callback)
        {
            for (int i = 0; i < frameCount; i++) yield return null;
            callback();
        }

        /// <summary>
        /// Invokes <paramref name="callback"/> on the very next frame. <br/>
        /// Shorthand for <c>RunAfterFrames(1, callback)</c>, kept as its own named method
        /// because "run this next frame" is common enough (deferring a UI refresh, breaking a
        /// same-frame re-entrancy loop) to deserve a self-explanatory call site.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Coroutine RunNextFrame(this MonoBehaviour mb, Action callback)
            => mb.RunAfterFrames(1, callback);

        /// <summary>
        /// Invokes <paramref name="callback"/> after the current frame has finished rendering,
        /// via <see cref="CoroutineExtensions.EndOfFrame"/>. <br/>
        /// The standard timing point for screenshot capture, per-frame texture readback, or
        /// any logic that must run after all cameras have rendered but before the next
        /// <c>Update</c> begins.
        /// </summary>
        public static Coroutine RunAfterEndOfFrame(this MonoBehaviour mb, Action callback)
        {
            if (!mb.CanStartCoroutine() || callback == null) return null;
            return mb.StartCoroutine(EndOfFrameRoutine(callback));
        }

        private static IEnumerator EndOfFrameRoutine(Action callback)
        {
            yield return CoroutineExtensions.EndOfFrame;
            callback();
        }

        /// <summary>
        /// Polls <paramref name="condition"/> once per frame and invokes
        /// <paramref name="onConditionMet"/> the moment it returns <c>true</c>, optionally
        /// giving up and invoking <paramref name="onTimeout"/> if
        /// <paramref name="timeoutSeconds"/> elapses first. <br/>
        /// The standard way to wait for an external state change without a hard dependency on
        /// an event — "wait until the door is unlocked," "wait until the loading screen
        /// reports ready" — while still guarding against waiting forever if that condition
        /// legitimately never becomes true (a missing event hookup, an unreachable objective).
        /// A <paramref name="timeoutSeconds"/> of <c>0</c> or less disables the timeout entirely.
        /// </summary>
        public static Coroutine RunWhen(this MonoBehaviour mb, Func<bool> condition, Action onConditionMet, float timeoutSeconds = -1f, Action onTimeout = null, bool useUnscaledTime = false)
        {
            if (!mb.CanStartCoroutine() || condition == null) return null;
            return mb.StartCoroutine(RunWhenRoutine(condition, onConditionMet, timeoutSeconds, onTimeout, useUnscaledTime));
        }

        private static IEnumerator RunWhenRoutine(Func<bool> condition, Action onConditionMet, float timeoutSeconds, Action onTimeout, bool useUnscaledTime)
        {
            float elapsed = 0f;

            while (!condition())
            {
                if (timeoutSeconds > 0f)
                {
                    elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                    if (elapsed >= timeoutSeconds)
                    {
                        onTimeout?.Invoke();
                        yield break;
                    }
                }

                yield return null;
            }

            onConditionMet?.Invoke();
        }

        #endregion

        #region Parallel Composition

        /// <summary>
        /// Starts every non-null routine in <paramref name="routines"/> simultaneously and
        /// invokes <paramref name="onAllComplete"/> once every one of them has finished. <br/>
        /// The standard way to script a cutscene beat or ability where several things must
        /// happen at once and the next step depends on all of them finishing — for example
        /// three enemies each playing a death animation before a victory fanfare plays. <br/>
        /// If <paramref name="routines"/> is <c>null</c>, empty, or contains only <c>null</c>
        /// entries, <paramref name="onAllComplete"/> is invoked immediately and synchronously.
        /// <para>
        /// Internally wraps each routine with <see cref="CoroutineExtensions.ThenRun"/> and a
        /// shared closure-captured counter; this is a deliberate, one-time-per-call allocation
        /// exception consistent with the rest of this class's scheduling methods.
        /// </para>
        /// </summary>
        public static void RunParallel(this MonoBehaviour mb, IEnumerator[] routines, Action onAllComplete)
        {
            if (!mb.CanStartCoroutine() || routines == null || routines.Length == 0)
            {
                onAllComplete?.Invoke();
                return;
            }

            int remaining = routines.Length;

            for (int i = 0; i < routines.Length; i++)
            {
                if (routines[i] == null)
                {
                    remaining--;
                    continue;
                }

                mb.StartCoroutine(routines[i].ThenRun(() =>
                {
                    remaining--;
                    if (remaining == 0) onAllComplete?.Invoke();
                }));
            }

            if (remaining == 0) onAllComplete?.Invoke();
        }

        #endregion

        #region Safe Invoke

        /// <summary>
        /// Calls <see cref="MonoBehaviour.Invoke(string, float)"/> only if
        /// <paramref name="methodName"/> is non-empty, clamping a negative
        /// <paramref name="delaySeconds"/> to zero. <br/>
        /// Raw <see cref="MonoBehaviour.Invoke(string, float)"/> throws
        /// <see cref="ArgumentException"/> on a negative delay; this guard removes that crash
        /// risk for a delay value computed at runtime (e.g. a cooldown that could theoretically
        /// go negative due to a buff calculation edge case).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void InvokeSafe(this MonoBehaviour mb, string methodName, float delaySeconds)
        {
            if (mb != null && !string.IsNullOrEmpty(methodName)) mb.Invoke(methodName, Mathf.Max(0f, delaySeconds));
        }

        /// <summary>
        /// Calls <see cref="MonoBehaviour.InvokeRepeating(string, float, float)"/> only if
        /// <paramref name="methodName"/> is non-empty and <paramref name="repeatRateSeconds"/>
        /// is positive, clamping a negative initial delay to zero. <br/>
        /// A <paramref name="repeatRateSeconds"/> of zero or less makes Unity's native method
        /// throw; guarding it here turns a data-driven misconfiguration (a designer-authored
        /// tick rate of <c>0</c>) into a silent no-op rather than a startup crash.
        /// </summary>
        public static void InvokeRepeatingSafe(this MonoBehaviour mb, string methodName, float initialDelaySeconds, float repeatRateSeconds)
        {
            if (mb == null || string.IsNullOrEmpty(methodName) || repeatRateSeconds <= 0f) return;
            mb.InvokeRepeating(methodName, Mathf.Max(0f, initialDelaySeconds), repeatRateSeconds);
        }

        /// <summary>
        /// True if <paramref name="methodName"/> currently has a pending
        /// <see cref="MonoBehaviour.Invoke(string, float)"/> or
        /// <see cref="MonoBehaviour.InvokeRepeating(string, float, float)"/> call scheduled on
        /// this component, guarded against a destroyed component or empty method name. <br/>
        /// Thin null-safe wrapper around <see cref="MonoBehaviour.IsInvoking(string)"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsInvokingSafe(this MonoBehaviour mb, string methodName)
            => mb != null && !string.IsNullOrEmpty(methodName) && mb.IsInvoking(methodName);

        /// <summary>
        /// Cancels a pending <see cref="MonoBehaviour.Invoke(string, float)"/> or
        /// <see cref="MonoBehaviour.InvokeRepeating(string, float, float)"/> call for
        /// <paramref name="methodName"/>, guarded against a destroyed component or empty
        /// method name. <br/>
        /// Safe to call even if nothing is currently scheduled under that name — Unity's own
        /// <see cref="MonoBehaviour.CancelInvoke(string)"/> already no-ops in that case — this
        /// wrapper exists purely to avoid a <see cref="NullReferenceException"/> on an already
        /// destroyed component during teardown.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CancelInvokeSafe(this MonoBehaviour mb, string methodName)
        {
            if (mb != null && !string.IsNullOrEmpty(methodName)) mb.CancelInvoke(methodName);
        }

        /// <summary>
        /// Cancels every pending <see cref="MonoBehaviour.Invoke(string, float)"/> and
        /// <see cref="MonoBehaviour.InvokeRepeating(string, float, float)"/> call on this
        /// component, guarded against a destroyed component. <br/>
        /// The correct cleanup call before returning a component to an object pool — without
        /// it, a previously scheduled <c>InvokeRepeating</c> (a periodic status-effect tick, an
        /// AI decision timer) keeps firing against the pooled object's new, unrelated state
        /// after it is reactivated elsewhere.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CancelAllInvokesSafe(this MonoBehaviour mb)
        {
            if (mb != null) mb.CancelInvoke();
        }

        #endregion
    }
}