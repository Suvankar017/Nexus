namespace Nexus.Timers
{
    public enum TimerTimeMode
    {
        /// <summary>
        /// Advances using <see cref="Time.ITimeService.ScaledDeltaTime"/> — stops while paused.
        /// </summary>
        Scaled,

        /// <summary>
        /// Advances using <see cref="Time.ITimeService.UnscaledDeltaTime"/> — keeps
        /// running while paused. No third mode is offered: Unscaled already covers the
        /// "must keep running through pause" need, since Pause only zeroes Unity's time scale,
        /// which unscaled delta time ignores by definition.
        /// </summary>
        Unscaled
    }
}
