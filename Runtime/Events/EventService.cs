using System;
using System.Buffers;
using System.Collections.Generic;
using Nexus.Core.Validation;
using Nexus.Services;
using Nexus.Diagnostics;

namespace Nexus.Events
{
    public sealed class EventService : IEventService
    {
        private sealed class Subscription<TEvent> : IEventSubscription
        {
            private EventService _owner;
            private Action<TEvent> _handler;

            public Subscription(EventService owner, Action<TEvent> handler)
            {
                _owner = owner;
                _handler = handler;
            }

            public void Dispose()
            {
                if (_handler == null)
                    return;

                _owner.Unsubscribe(_handler);
                _owner = null;
                _handler = null;
            }
        }

        private readonly Dictionary<Type, List<Delegate>> _handlers = new();
        private ILoggingService _log;

        public void Initialize(IServiceRegistry registry)
        {
            registry.TryGet(out _log);
        }

        public void Shutdown()
        {
            _handlers.Clear();
        }

        public IEventSubscription Subscribe<TEvent>(Action<TEvent> handler)
        {
            Guard.NotNull(handler, nameof(handler));

            Type type = typeof(TEvent);
            if (!_handlers.TryGetValue(type, out List<Delegate> list))
            {
                list = new List<Delegate>();
                _handlers.Add(type, list);
            }

            if (!list.Contains(handler))
            {
                list.Add(handler);
            }

            return new Subscription<TEvent>(this, handler);
        }

        public void Unsubscribe<TEvent>(Action<TEvent> handler)
        {
            if (handler == null)
            {
                return;
            }

            if (_handlers.TryGetValue(typeof(TEvent), out List<Delegate> list))
            {
                list.Remove(handler);
            }
        }

        public void Publish<TEvent>(TEvent payload)
        {
            if (!_handlers.TryGetValue(typeof(TEvent), out List<Delegate> list) || list.Count == 0)
            {
                return;
            }

            // Snapshot into a pooled array so Subscribe/Unsubscribe calls made from inside a
            // handler — including a handler removing itself, or a nested Publish of the same
            // event type — never mutate the sequence this call is iterating.
            int count = list.Count;
            Delegate[] snapshot = ArrayPool<Delegate>.Shared.Rent(count);
            list.CopyTo(snapshot);

            try
            {
                for (int i = 0; i < count; i++)
                {
                    try
                    {
                        ((Action<TEvent>)snapshot[i])(payload);
                    }
                    catch (Exception exception)
                    {
                        _log?.LogException(exception, "Events");
                    }
                }
            }
            finally
            {
                ArrayPool<Delegate>.Shared.Return(snapshot, clearArray: true);
            }
        }
    }
}
