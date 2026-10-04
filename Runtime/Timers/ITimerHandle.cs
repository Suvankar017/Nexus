namespace Nexus.Timers
{
    /// <summary>
    /// A handle to a running timer. This is the only thing callers get back from
    /// <see cref="ITimerService"/> — internal timer storage is never exposed directly.
    /// </summary>
    public interface ITimerHandle
    {
        TimerState State { get; }
        bool IsActive { get; }
        bool IsPaused { get; }
        bool IsCompleted { get; }
        bool IsCancelled { get; }

        float Duration { get; }
        float Elapsed { get; }
        float Remaining { get; }

        /// <summary>
        /// 0 at start, 1 at completion. For a repeating timer this is the progress of
        /// the current cycle, not overall.
        /// </summary>
        float Progress { get; }

        /// <summary>
        /// Stops the timer permanently; no further callbacks fire. Safe to call more
        /// than once, and safe to call from inside the timer's own callback.
        /// </summary>
        void Cancel();

        /// <summary>
        /// Stops the timer from advancing until <see cref="Resume"/>. No-op if not
        /// currently <see cref="TimerState.Active"/>.
        /// </summary>
        void Pause();

        /// <summary>
        /// Resumes a paused timer. No-op if not currently <see cref="TimerState.Paused"/>.
        /// </summary>
        void Resume();
    }
}
