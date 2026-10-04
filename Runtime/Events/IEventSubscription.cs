using System;

namespace Nexus.Events
{
    /// <summary>
    /// Token returned by <see cref="IEventService.Subscribe{TEvent}"/>. Disposing it unsubscribes
    /// the handler — the recommended cleanup pattern, since it can't be mismatched with the wrong
    /// delegate the way calling <see cref="IEventService.Unsubscribe{TEvent}"/> by hand can be.
    /// Disposing more than once is safe.
    /// </summary>
    public interface IEventSubscription : IDisposable
    {
    }
}
