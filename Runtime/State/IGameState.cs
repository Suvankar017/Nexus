namespace Nexus.State
{
    /// <summary>
    /// A single application-level state managed by <see cref="IGameStateService"/>. The framework
    /// defines no concrete states (Boot, MainMenu, Gameplay, ...) — those are game-specific and are
    /// registered by the game under whatever string keys it chooses.
    /// </summary>
    public interface IGameState
    {
        /// <summary>
        /// Called once when this state becomes current. Never called twice in a row
        /// without a matching <see cref="Exit"/> between calls.
        /// </summary>
        void Enter();

        /// <summary>
        /// Called once when this state stops being current.
        /// </summary>
        void Exit();
    }
}
