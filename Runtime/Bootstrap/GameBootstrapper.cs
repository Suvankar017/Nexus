using System;
using System.Collections.Generic;
using UnityEngine;
using Nexus.Time;
using Nexus.State;
using Nexus.Events;
using Nexus.Timers;
using Nexus.Services;
using Nexus.Settings;
using Nexus.Persistence;
using Nexus.Diagnostics;
using Nexus.SceneManagement;

namespace Nexus.Bootstrap
{
    [DisallowMultipleComponent]
    public class GameBootstrapper : MonoBehaviour
    {
        public static GameBootstrapper Instance { get; private set; }

        public BootstrapState State { get; private set; } = BootstrapState.Created;

        /// <summary>Only safe to use once <see cref="State"/> is <see cref="BootstrapState.Ready"/>.</summary>
        public IServiceRegistry Services => _registry;

        public event Action<BootstrapState> StateChanged;

        private readonly ServiceRegistry _registry = new();
        private readonly List<IUpdatableService> _updatableServices = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning(
                    $"[Bootstrap] Duplicate {nameof(GameBootstrapper)} on '{gameObject.name}' was destroyed; " +
                    $"'{Instance.gameObject.name}' remains the active instance.",
                    this);
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            Initialize();
        }

        private void OnDestroy()
        {
            if (Instance != this)
                return;
            
            if (State == BootstrapState.Ready)
                Shutdown();
            
            Instance = null;
        }

        private void OnApplicationQuit()
        {
            if (State == BootstrapState.Ready)
                Shutdown();
        }

        private void Update()
        {
            if (State != BootstrapState.Ready)
                return;
            
            for (int i = 0; i < _updatableServices.Count; i++)
                _updatableServices[i].Tick();
        }

        /// <summary>
        /// Shuts down every registered service in reverse registration order and clears the
        /// registry. Safe to call more than once, or when the bootstrap never reached
        /// <see cref="BootstrapState.Ready"/> — both are treated as a no-op rather than an error,
        /// since shutdown commonly runs from application-quit/teardown paths where being strict
        /// would just add noise.
        /// </summary>
        public void Shutdown()
        {
            if (State != BootstrapState.Ready)
                return;
            
            SetState(BootstrapState.ShuttingDown);

            IReadOnlyList<Type> order = _registry.RegistrationOrder;
            for (int i = order.Count - 1; i >= 0; i--)
            {
                object instance = _registry.GetInstance(order[i]);
                if (instance is IGameService gameService)
                    gameService.Shutdown();
            }

            _registry.Clear();
            _updatableServices.Clear();

            SetState(BootstrapState.Shutdown);
        }

        // Internal (rather than private) so tests can exercise the "already initialized" guard
        // directly; Awake is the only caller in normal use and Unity only invokes it once.
        internal void Initialize()
        {
            if (State != BootstrapState.Created)
            {
                throw new InvalidOperationException($"{nameof(GameBootstrapper)} has already been initialized.");
            }

            SetState(BootstrapState.Initializing);

            RegisterServices(_registry);

            foreach (Type serviceType in _registry.RegistrationOrder)
            {
                var service = _registry.GetInstance(serviceType);

                if (service is IGameService gameService)
                    gameService.Initialize(_registry);

                _registry.MarkInitialized(serviceType);

                if (service is IUpdatableService updatable)
                    _updatableServices.Add(updatable);
            }

            SetState(BootstrapState.Ready);
        }

        private void SetState(BootstrapState state)
        {
            State = state;
            StateChanged?.Invoke(state);
        }

        /// <summary>
        /// Registers the services exposed through <see cref="Services"/>, in the order they will
        /// be initialized (and shut down in reverse). Override to add game-specific services;
        /// call <c>base.RegisterServices(registry)</c> first to keep the framework's own services
        /// available to them.<br/><br/>
        ///
        /// Order here is load-bearing, not cosmetic: <see cref="ITimerService"/> requires
        /// <see cref="ITimeService"/> to already be initialized, and <see cref="ISettingsService"/>
        /// requires both <see cref="IPersistenceService"/> and <see cref="IEventService"/> to be —
        /// each is registered only after what it depends on.
        /// </summary>
        protected virtual void RegisterServices(IServiceRegistry registry)
        {
            registry.Register<ILoggingService>(new LoggingService());
            registry.Register<IEventService>(new EventService());

            registry.Register<ITimeService>(new TimeService());
            registry.Register<ITimerService>(new TimerService());

            registry.Register<ISceneService>(new SceneService());
            registry.Register<IGameStateService>(new GameStateService());

            registry.Register<IPersistenceService>(
                new PersistenceService(
                    new FilePersistenceStorage(),
                    new JSONPersistenceSerializer()));

            registry.Register<ISettingsService>(new SettingsService());
        }
    }
}
