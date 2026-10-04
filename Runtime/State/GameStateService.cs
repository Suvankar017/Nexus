using System;
using System.Collections.Generic;
using Nexus.Services;
using Nexus.Core.Validation;

namespace Nexus.State
{
    public sealed class GameStateService : IGameStateService
    {
        private readonly Dictionary<string, IGameState> _states = new();
        private bool _isTransitioning;

        public string CurrentState { get; private set; }
        public string PreviousState { get; private set; }
        public bool HasCurrentState { get; private set; }

        public event Action<string, string> StateChanged;

        public void Initialize(IServiceRegistry registry)
        {
        }

        public void Shutdown()
        {
            if (HasCurrentState && _states.TryGetValue(CurrentState, out IGameState current))
            {
                current.Exit();
            }

            HasCurrentState = false;
            CurrentState = null;
            PreviousState = null;
            _states.Clear();
        }

        public void RegisterState(string key, IGameState state)
        {
            Guard.NotNullOrEmpty(key, nameof(key));
            Guard.NotNull(state, nameof(state));

            if (_states.ContainsKey(key))
            {
                throw new InvalidOperationException($"State '{key}' is already registered.");
            }

            _states.Add(key, state);
        }

        public bool CanTransitionTo(string key)
        {
            return !string.IsNullOrEmpty(key)
                && _states.ContainsKey(key)
                && !_isTransitioning
                && (!HasCurrentState || key != CurrentState);
        }

        public void TransitionTo(string key)
        {
            Guard.NotNullOrEmpty(key, nameof(key));

            if (!_states.TryGetValue(key, out IGameState nextState))
            {
                throw new InvalidOperationException($"No state registered for key '{key}'.");
            }

            if (_isTransitioning)
            {
                throw new InvalidOperationException(
                    $"Cannot transition to '{key}' while another transition is already in progress.");
            }

            if (HasCurrentState && key == CurrentState)
            {
                throw new InvalidOperationException($"State '{key}' is already the current state.");
            }

            _isTransitioning = true;
            string previousKey = CurrentState;
            try
            {
                if (HasCurrentState)
                {
                    _states[CurrentState].Exit();
                }

                nextState.Enter();

                PreviousState = previousKey;
                CurrentState = key;
                HasCurrentState = true;
            }
            finally
            {
                _isTransitioning = false;
            }

            StateChanged?.Invoke(PreviousState, CurrentState);
        }
    }
}
