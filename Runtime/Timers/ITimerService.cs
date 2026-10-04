using System;
using Nexus.Services;
using Object = UnityEngine.Object;

namespace Nexus.Timers
{
    /// <summary>
    /// Centralized timer scheduling built on <see cref="Time.ITimeService"/> — it never reads
    /// <c>UnityEngine.Time</c> directly. All four factory methods below share one internal
    /// mechanism; they exist as separate names because their callback shapes and typical intent
    /// differ, not because they're separately implemented.
    ///
    /// Pass <paramref name="owner"/> to tie a timer to a Unity object's lifetime: once that object
    /// is destroyed, the timer is silently cancelled (no callback fires) the next tick rather than
    /// invoking a callback that might touch a destroyed object. Omit it for an application-lifetime
    /// timer.
    /// </summary>
    public interface ITimerService : IGameService
    {
        /// <summary>
        /// Fires <paramref name="onComplete"/> once after <paramref name="duration"/>.
        /// </summary>
        ITimerHandle StartOneShot(
            float duration,
            Action onComplete,
            TimerTimeMode mode = TimerTimeMode.Scaled,
            Object owner = null);

        /// <summary>
        /// Naming-only alias for <see cref="StartOneShot"/> — reads better at
        /// fire-and-forget call sites. Produces an identical handle.
        /// </summary>
        ITimerHandle StartDelay(
            float delay,
            Action onElapsed,
            TimerTimeMode mode = TimerTimeMode.Scaled,
            Object owner = null);

        /// <summary>
        /// Fires <paramref name="onTick"/> every <paramref name="interval"/> seconds.
        /// <paramref name="repeatCount"/> of 0 repeats indefinitely (until cancelled); otherwise
        /// the timer completes after that many fires.
        /// </summary>
        ITimerHandle StartRepeating(
            float interval,
            Action onTick,
            int repeatCount = 0,
            TimerTimeMode mode = TimerTimeMode.Scaled,
            Object owner = null);

        /// <summary>
        /// Like <see cref="StartOneShot"/>, but also calls <paramref name="onTick"/>
        /// every frame with the remaining time — intended for UI countdowns.
        /// </summary>
        ITimerHandle StartCountdown(
            float duration,
            Action<float> onTick,
            Action onComplete,
            TimerTimeMode mode = TimerTimeMode.Scaled,
            Object owner = null);

        /// <summary>
        /// Cancels every timer currently tracked by this service.
        /// </summary>
        void CancelAll();
    }
}
