using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using Nexus.Core.Extensions;
using Nexus.Core.Validation;
using Nexus.Time;
using Nexus.Services;
using Nexus.Diagnostics;

namespace Nexus.Timers
{
    public sealed class TimerService : ITimerService, IUpdatableService
    {
        private sealed class TimerEntry : ITimerHandle
        {
            public float Duration { get; set; }
            public float Elapsed { get; set; }
            public bool Repeating;
            public int RepeatsRemaining; // -1 = infinite
            public TimerTimeMode Mode;
            public Action OnFire;
            public Action<float> OnProgress;
            public Object Owner;

            public TimerState State { get; set; } = TimerState.Active;
            public bool IsActive => State == TimerState.Active;
            public bool IsPaused => State == TimerState.Paused;
            public bool IsCompleted => State == TimerState.Completed;
            public bool IsCancelled => State == TimerState.Cancelled;
            public float Remaining => Mathf.Max(0f, Duration - Elapsed);
            public float Progress => Duration <= 0f ? 1f : Mathf.Clamp01(Elapsed / Duration);

            public void Cancel()
            {
                if (State == TimerState.Active || State == TimerState.Paused)
                {
                    State = TimerState.Cancelled;
                }
            }

            public void Pause()
            {
                if (State == TimerState.Active)
                {
                    State = TimerState.Paused;
                }
            }

            public void Resume()
            {
                if (State == TimerState.Paused)
                {
                    State = TimerState.Active;
                }
            }
        }

        private readonly List<TimerEntry> _entries = new();
        private ITimeService _time;
        private ILoggingService _log;

        public void Initialize(IServiceRegistry registry)
        {
            _time = registry.Get<ITimeService>();
            registry.TryGet(out _log);
        }

        public void Shutdown()
        {
            CancelAll();
            _entries.Clear();
        }

        public ITimerHandle StartOneShot(
            float duration,
            Action onComplete,
            TimerTimeMode mode = TimerTimeMode.Scaled,
            Object owner = null)
        {
            Guard.NotNull(onComplete, nameof(onComplete));
            return StartEntry(
                duration,
                repeating: false,
                repeatCount: 0,
                mode,
                owner,
                onFire: onComplete,
                onProgress: null);
        }

        public ITimerHandle StartDelay(
            float delay,
            Action onElapsed,
            TimerTimeMode mode = TimerTimeMode.Scaled,
            Object owner = null)
        {
            return StartOneShot(delay, onElapsed, mode, owner);
        }

        public ITimerHandle StartRepeating(
            float interval,
            Action onTick,
            int repeatCount = 0,
            TimerTimeMode mode = TimerTimeMode.Scaled,
            Object owner = null)
        {
            Guard.NotNull(onTick, nameof(onTick));
            return StartEntry(
                interval,
                repeating: true,
                repeatCount,
                mode,
                owner,
                onFire: onTick,
                onProgress: null);
        }

        public ITimerHandle StartCountdown(
            float duration,
            Action<float> onTick,
            Action onComplete,
            TimerTimeMode mode = TimerTimeMode.Scaled,
            Object owner = null)
        {
            Guard.NotNull(onComplete, nameof(onComplete));
            return StartEntry(
                duration,
                repeating: false,
                repeatCount: 0,
                mode,
                owner,
                onFire: onComplete,
                onProgress: onTick);
        }

        public void CancelAll()
        {
            for (int i = 0; i < _entries.Count; i++)
                _entries[i].Cancel();
        }

        private ITimerHandle StartEntry(
            float duration,
            bool repeating,
            int repeatCount,
            TimerTimeMode mode,
            Object owner,
            Action onFire,
            Action<float> onProgress)
        {
            var entry = new TimerEntry()
            {
                Duration = Mathf.Max(0f, duration),
                Elapsed = 0f,
                Repeating = repeating,
                RepeatsRemaining = repeatCount > 0 ? repeatCount : -1,
                Mode = mode,
                OnFire = onFire,
                OnProgress = onProgress,
                Owner = owner
            };

            _entries.Add(entry);
            return entry;
        }

        public void Tick()
        {
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                TimerEntry entry = _entries[i];

                if (entry.State == TimerState.Completed || entry.State == TimerState.Cancelled)
                {
                    _entries.RemoveAt(i);
                    continue;
                }

                if (!ReferenceEquals(entry.Owner, null) && entry.Owner.IsNullOrDestroyed())
                {
                    entry.State = TimerState.Cancelled;
                    _entries.RemoveAt(i);
                    continue;
                }

                if (entry.State == TimerState.Paused)
                {
                    continue;
                }

                float delta = entry.Mode == TimerTimeMode.Scaled ? _time.ScaledDeltaTime : _time.UnscaledDeltaTime;
                entry.Elapsed += delta;

                if (entry.OnProgress != null)
                {
                    try
                    {
                        entry.OnProgress(entry.Remaining);
                    }
                    catch (Exception exception)
                    {
                        _log?.LogException(exception, "Timers");
                    }
                }

                if (entry.State == TimerState.Cancelled)
                {
                    _entries.RemoveAt(i);
                    continue;
                }

                if (entry.Elapsed < entry.Duration)
                {
                    continue;
                }

                if (entry.Repeating)
                {
                    entry.Elapsed -= entry.Duration;

                    if (entry.RepeatsRemaining > 0)
                    {
                        entry.RepeatsRemaining--;
                    }

                    InvokeFire(entry);

                    if (entry.State == TimerState.Cancelled || entry.RepeatsRemaining == 0)
                    {
                        entry.State = entry.State == TimerState.Cancelled ? TimerState.Cancelled : TimerState.Completed;
                        _entries.RemoveAt(i);
                    }
                }
                else
                {
                    entry.State = TimerState.Completed;
                    InvokeFire(entry);
                    _entries.RemoveAt(i);
                }
            }
        }

        private void InvokeFire(TimerEntry entry)
        {
            if (entry.OnFire == null)
            {
                return;
            }

            try
            {
                entry.OnFire();
            }
            catch (Exception exception)
            {
                _log?.LogException(exception, "Timers");
            }
        }
    }
}
