namespace Nexus.Services
{
    /// <summary>
    /// Lifecycle contract for an application-level service owned by an
    /// <see cref="IServiceRegistry"/>.<br/>
    /// The bootstrap that registers a service calls <see cref="Initialize"/> exactly once
    /// (in registration order) and <see cref="Shutdown"/> exactly once
    /// (in reverse registration order);<br/>
    /// A service must not call either method on itself.
    /// </summary>
    public interface IGameService
    {
        /// <summary>
        /// Called once by the bootstrap after registration.<br/>
        /// Other services registered earlier in the same sequence are safe to look up on
        /// <paramref name="registry"/> here;<br/>
        /// Services registered later are not yet initialized and
        /// <see cref="IServiceRegistry.Get{TService}"/> will throw for them.
        /// </summary>
        /// <param name="registry">Service registry</param>
        void Initialize(IServiceRegistry registry);

        /// <summary>
        /// Called once by the bootstrap during shutdown.<br/>
        /// Must leave the service safe to be garbage collected;<br/>
        /// Do not assume other services are still initialized at this point.
        /// </summary>
        void Shutdown();
    }
}
