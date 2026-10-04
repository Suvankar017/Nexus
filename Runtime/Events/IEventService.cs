using System;
using Nexus.Services;

namespace Nexus.Events
{
    /// <summary>
    /// Lightweight strongly-typed publish/subscribe for decoupling systems that shouldn't
    /// reference each other directly. This is not a replacement for ordinary method calls —
    /// reach for it when the publisher genuinely shouldn't know who (if anyone) is listening.
    ///
    /// Event payloads are plain types the game defines (structs are recommended for
    /// high-frequency events to avoid per-publish heap allocation of the payload itself):
    /// <code>
    /// public readonly struct LevelCompletedEvent
    /// {
    ///     public readonly int LevelId;
    ///     public LevelCompletedEvent(int levelId) { LevelId = levelId; }
    /// }
    /// </code>
    /// </summary>
    public interface IEventService : IGameService
    {
        /// <summary>Subscribes <paramref name="handler"/> to <typeparamref name="TEvent"/>.
        /// Subscribing the same delegate twice is a no-op (it will only be invoked once per
        /// publish). Returns a token whose Dispose unsubscribes it.</summary>
        IEventSubscription Subscribe<TEvent>(Action<TEvent> handler);

        /// <summary>Removes a previously subscribed handler. Safe to call with a handler that
        /// isn't subscribed (no-op).</summary>
        void Unsubscribe<TEvent>(Action<TEvent> handler);

        /// <summary>
        /// Invokes every current subscriber of <typeparamref name="TEvent"/> with
        /// <paramref name="payload"/>, in subscription order. A no-op if there are no subscribers.
        /// If a subscriber throws, the exception is logged (category "Events") and the remaining
        /// subscribers still run — one broken listener must not stop others from receiving the
        /// event. Subscribing or unsubscribing (including a handler removing itself) from inside a
        /// handler is safe and never affects the in-progress publish.
        /// </summary>
        void Publish<TEvent>(TEvent payload);
    }
}
