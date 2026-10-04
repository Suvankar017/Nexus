using UnityEngine;
using Nexus.Services;

namespace Nexus.Time
{
    public sealed class TimeService : ITimeService
    {
        private float _desiredScale = 1f;
        private int _pauseCount;

        public float ScaledDeltaTime => UnityEngine.Time.deltaTime;
        public float UnscaledDeltaTime => UnityEngine.Time.unscaledDeltaTime;
        public float FixedDeltaTime => UnityEngine.Time.fixedDeltaTime;
        public float UnscaledFixedDeltaTime => UnityEngine.Time.fixedUnscaledDeltaTime;
        public float ScaledTime => UnityEngine.Time.time;
        public float UnscaledTime => UnityEngine.Time.unscaledTime;
        public float Realtime => UnityEngine.Time.realtimeSinceStartup;

        public float TimeScale => UnityEngine.Time.timeScale;
        public bool IsPaused => _pauseCount > 0;

        public void Initialize(IServiceRegistry registry)
        {
            // Establish a known baseline: whatever Time.timeScale was before Bootstrap ran is
            // overwritten, since from this point on this service is the sole owner of it.
            _desiredScale = 1f;
            _pauseCount = 0;
            UnityEngine.Time.timeScale = _desiredScale;
        }

        public void Shutdown()
        {
            _pauseCount = 0;
            UnityEngine.Time.timeScale = 1f;
        }

        public void SetTimeScale(float scale)
        {
            _desiredScale = Mathf.Max(0f, scale);

            if (!IsPaused)
                UnityEngine.Time.timeScale = _desiredScale;
        }

        public void ResetTimeScale()
        {
            SetTimeScale(1f);
        }

        public void Pause()
        {
            _pauseCount++;

            if (_pauseCount == 1)
                UnityEngine.Time.timeScale = 0f;
        }

        public void Resume()
        {
            if (_pauseCount == 0)
                return;
            
            _pauseCount--;

            if (_pauseCount == 0)
                UnityEngine.Time.timeScale = _desiredScale;
        }
    }
}
