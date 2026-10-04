using Nexus.Services;

namespace Nexus.Time
{
    /// <summary>
    /// Centralized wrapper around <see cref="UnityEngine.Time"/>. Nothing outside this service
    /// should write <c>UnityEngine.Time.timeScale</c> directly — go through
    /// <see cref="SetTimeScale"/>/<see cref="Pause"/>/<see cref="Resume"/> so time-scale ownership
    /// stays in one place and is never silently overwritten by an unrelated system.
    /// </summary>
    public interface ITimeService : IGameService
    {
        /// <summary>
        /// Frame delta time, affected by <see cref="TimeScale"/> — use for gameplay.
        /// </summary>
        float ScaledDeltaTime { get; }

        /// <summary>
        /// Frame delta time, never affected by time scale — use for UI/pause/tutorial
        /// overlays that must keep moving while gameplay is paused or slowed.
        /// </summary>
        float UnscaledDeltaTime { get; }

        /// <summary>
        /// Fixed-step delta time, affected by <see cref="TimeScale"/>.
        /// </summary>
        float FixedDeltaTime { get; }

        /// <summary>
        /// Fixed-step delta time, never affected by time scale.
        /// </summary>
        float UnscaledFixedDeltaTime { get; }

        /// <summary>
        /// Accumulated time since startup, affected by <see cref="TimeScale"/>.
        /// </summary>
        float ScaledTime { get; }

        /// <summary>
        /// Accumulated time since startup, never affected by time scale.
        /// </summary>
        float UnscaledTime { get; }

        /// <summary>
        /// Real (wall-clock) time since startup — keeps advancing even while paused.
        /// </summary>
        float Realtime { get; }

        /// <summary>
        /// The scale currently applied to gameplay time. Always 0 while
        /// <see cref="IsPaused"/> is true, regardless of what was last requested via
        /// <see cref="SetTimeScale"/> — see that method's remarks.
        /// </summary>
        float TimeScale { get; }

        /// <summary>
        /// True while at least one unmatched <see cref="Pause"/> call is outstanding.
        /// </summary>
        bool IsPaused { get; }

        /// <summary>
        /// Sets the time scale to use while not paused. If currently paused, this only updates
        /// what will be restored on the last matching <see cref="Resume"/> — the engine's actual
        /// time scale stays at 0 until then. This is the one place non-zero time scale changes
        /// (e.g. slow motion) should be requested; the most recent call wins for that resolved
        /// scale, which is safe specifically because <see cref="Pause"/>/<see cref="Resume"/> (the
        /// case where "last caller wins" would be dangerous — one system's Resume silently undoing
        /// another's Pause) is handled separately below via reference counting.
        /// </summary>
        void SetTimeScale(float scale);

        /// <summary>
        /// Equivalent to <c>SetTimeScale(1f)</c>.
        /// </summary>
        void ResetTimeScale();

        /// <summary>
        /// Requests a pause. Reference-counted: multiple independent callers (e.g. a pause menu
        /// and a cutscene) can each call Pause/Resume without one's Resume cancelling another's
        /// still-active Pause. The engine's time scale is forced to 0 for as long as any Pause is
        /// outstanding, and restored to the last value requested via <see cref="SetTimeScale"/>
        /// once the last matching <see cref="Resume"/> is called.
        /// </summary>
        void Pause();

        /// <summary>
        /// Releases one <see cref="Pause"/> request. A no-op if none are outstanding.
        /// </summary>
        void Resume();
    }
}
