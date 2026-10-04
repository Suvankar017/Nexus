using System;
using Nexus.Services;

namespace Nexus.State
{
    /// <summary>
    /// Generic application-level state machine, keyed by string identifiers the game defines
    /// (e.g. "MainMenu", "Gameplay"). Deliberately not coupled to scenes — a state entering does
    /// not imply any particular scene is loaded unless the game's <see cref="IGameState"/>
    /// implementation chooses to call the scene service itself.
    /// </summary>
    public interface IGameStateService : IGameService
    {
        /// <summary>
        /// Key of the current state, or null if no transition has happened yet.
        /// </summary>
        string CurrentState { get; }

        /// <summary>
        /// Key of the previously active state, or null before the first transition.
        /// </summary>
        string PreviousState { get; }

        bool HasCurrentState { get; }

        /// <summary>
        /// Raised after a transition completes, as (previous, current).
        /// </summary>
        event Action<string, string> StateChanged;

        /// <summary>
        /// Registers a state under <paramref name="key"/>. Throws if the key is already
        /// registered. Registering does not enter the state.
        /// </summary>
        void RegisterState(string key, IGameState state);

        /// <summary>
        /// True if <paramref name="key"/> is registered and is not already the current state.
        /// </summary>
        bool CanTransitionTo(string key);

        /// <summary>
        /// Exits the current state (if any) and enters the state registered under
        /// <paramref name="key"/>. Throws if the key is not registered or is already current.
        /// </summary>
        void TransitionTo(string key);
    }
}
