namespace Nexus.Services
{
    /// <summary>
    /// Opt-in for a service that needs a per-frame callback (e.g. the Timer service).<br/>
    /// This is a minimal internal driver — <see cref="Bootstrap.GameBootstrapper"/>
    /// calls <see cref="Tick"/> on every initialized service that implements this,
    /// in registration order, from its own Update().<br/>
    /// It is deliberately not a general-purpose scheduling/ordering/tick-group system;
    /// </summary>
    public interface IUpdatableService : IGameService
    {
        void Tick();
    }
}
