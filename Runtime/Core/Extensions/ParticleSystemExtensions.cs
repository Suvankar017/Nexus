using System.Runtime.CompilerServices;
using UnityEngine;

namespace Nexus.Core.Extensions
{
    /// <summary>
    /// Extension methods for <see cref="ParticleSystem"/> covering playback control,
    /// allocation-free emission, module configuration, and particle buffer access.
    /// <para>Design rules:</para>
    /// <list type="bullet">
    /// <item>Module properties such as <see cref="ParticleSystem.main"/> return wrapper structs
    /// that must be stored in a local before assignment (<c>ps.main.loop = false</c> is compile
    /// error CS1612). These helpers hide that boilerplate; writes to the local go straight
    /// through to the native system, so no reassignment is needed.</item>
    /// <item>Setters write through the <c>*Multiplier</c> properties instead of reading the
    /// full <see cref="ParticleSystem.MinMaxCurve"/> or <see cref="ParticleSystem.MinMaxGradient"/>.
    /// Reading those structs in curve or gradient mode marshals managed
    /// <see cref="AnimationCurve"/>/<see cref="Gradient"/> copies, a hidden GC allocation.</item>
    /// <item><c>Start*</c> setters only affect particles emitted afterwards. Live particles keep
    /// the values they spawned with, which is usually what you want for throttle-driven
    /// thrusters and damage-scaled impacts.</item>
    /// <item>Setters no-op on <c>NaN</c> or <c>Infinity</c> input. A single non-finite particle
    /// position or size corrupts the system's bounds, spamming "Invalid AABB" errors and
    /// breaking culling for the whole renderer.</item>
    /// <item>Emission helpers use <see cref="ParticleSystem.EmitParams"/> (a struct) and caller-owned
    /// buffers only. Zero allocation, no LINQ, no <c>GetComponent</c> in hot paths.</item>
    /// </list>
    /// </summary>
    public static class ParticleSystemExtensions
    {
        /// <summary>
        /// Smallest duration, in seconds, that Unity accepts for <see cref="ParticleSystem.MainModule.duration"/>.
        /// Values below this are clamped up, so an accidental <c>0</c> never produces a system
        /// that finishes its cycle instantly and never visibly emits.
        /// </summary>
        private const float MinDuration = 0.05f;

        /// <summary>
        /// Squared-length threshold below which a surface normal is treated as degenerate.
        /// Guards <see cref="EmitFromSurface"/> against the "Look rotation viewing vector is zero"
        /// warning and an undefined emission orientation.
        /// </summary>
        private const float DirectionSqrEpsilon = 1e-8f;

        /// <summary>
        /// Absolute Y component above which a normal is considered parallel to world up. Past this
        /// point <see cref="Vector3.up"/> can no longer serve as the look-rotation up hint, so
        /// <see cref="Vector3.forward"/> is used instead to keep floor and ceiling impacts stable.
        /// </summary>
        private const float ParallelUpThreshold = 0.999f;

        /// <summary>
        /// Maximum cone angle, in degrees, supported by <see cref="ParticleSystem.ShapeModule.angle"/>.
        /// </summary>
        private const float MaxConeAngle = 90f;

        /// <summary>
        /// Maximum arc, in degrees, supported by <see cref="ParticleSystem.ShapeModule.arc"/>.
        /// </summary>
        private const float MaxArcAngle = 360f;

        #region Playback Control

        /// <summary>
        /// Stops and clears the system, then plays it again from the start. <br/>
        /// <see cref="ParticleSystem.Play()"/> does nothing on a system that is already playing.
        /// A pooled muzzle flash or hit spark reused before its previous cycle ends would
        /// otherwise silently fail to fire.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Restart(this ParticleSystem ps, bool withChildren = true)
        {
            ps.Stop(withChildren, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Play(withChildren);
        }

        /// <summary>
        /// Stops spawning new particles but lets live ones finish their lifetime naturally. <br/>
        /// The correct way to end a projectile's smoke trail or an extinguished fire. Existing
        /// particles fade out instead of vanishing in a single frame.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StopEmitting(this ParticleSystem ps, bool withChildren = true)
            => ps.Stop(withChildren, ParticleSystemStopBehavior.StopEmitting);

        /// <summary>
        /// Stops the system and removes every live particle immediately. <br/>
        /// Required before returning an effect to an object pool. Otherwise stale particles
        /// flash at the old location for one frame when the pooled instance is reactivated
        /// somewhere else.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StopAndClear(this ParticleSystem ps, bool withChildren = true)
            => ps.Stop(withChildren, ParticleSystemStopBehavior.StopEmittingAndClear);

        /// <summary>
        /// Pauses the simulation when <paramref name="paused"/> is <c>true</c>. When it is
        /// <c>false</c>, resumes the simulation, but only if the system is currently paused. <br/>
        /// Needed for pause menus when effects use <see cref="ParticleSystem.MainModule.useUnscaledTime"/>,
        /// because those keep simulating while <see cref="Time.timeScale"/> is <c>0</c>. The resume
        /// guard prevents the call from accidentally starting a system that was intentionally stopped.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetPaused(this ParticleSystem ps, bool paused, bool withChildren = true)
        {
            if (paused) ps.Pause(withChildren);
            else if (ps.isPaused) ps.Play(withChildren);
        }

        /// <summary>
        /// Restarts the system, fast-forwards it by <paramref name="seconds"/>, and then plays it. <br/>
        /// Use for effects that should appear already in progress, such as a campfire that is
        /// burning when the player arrives. The built-in Prewarm option only works on looping
        /// systems; this method also works on one-shot effects.
        /// <para>
        /// <see cref="ParticleSystem.Simulate(float, bool, bool, bool)"/> runs synchronously on the
        /// main thread in fixed steps. Long prewarms on many systems cause a frame spike, so keep
        /// <paramref name="seconds"/> short or prewarm during a loading screen. Non-positive or
        /// non-finite values fall back to a plain <see cref="Restart"/>.
        /// </para>
        /// </summary>
        public static void PlayPrewarmed(this ParticleSystem ps, float seconds, bool withChildren = true)
        {
            if (!IsFinite(seconds) || seconds <= 0f)
            {
                ps.Restart(withChildren);
                return;
            }

            ps.Simulate(seconds, withChildren, restart: true, fixedTimeStep: true);
            ps.Play(withChildren);
        }

        #endregion

        #region Emission

        /// <summary>
        /// Emits <paramref name="count"/> particles, ignoring zero or negative counts. <br/>
        /// Guards emission counts computed at runtime (for example, blood scaled by damage, or
        /// debris scaled by a quality tier) against an underflow reaching the native emitter.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EmitSafe(this ParticleSystem ps, int count)
        {
            if (count > 0) ps.Emit(count);
        }

        /// <summary>
        /// Emits <paramref name="count"/> particles at <paramref name="position"/>. The shape
        /// module's spread is still applied around that point. <br/>
        /// This enables the shared-system pattern: one pooled system handles every bullet impact
        /// in the scene, instead of one prefab instance per impact.
        /// <para>
        /// <paramref name="position"/> is interpreted in the system's simulation space. Use
        /// <see cref="ParticleSystemSimulationSpace.World"/> (see <see cref="IsWorldSpace"/>);
        /// otherwise the position is treated as a local offset from the system's transform.
        /// Non-finite positions are rejected so they cannot corrupt the system bounds.
        /// </para>
        /// </summary>
        public static void EmitAt(this ParticleSystem ps, Vector3 position, int count)
        {
            if (count <= 0 || !IsFinite(position)) return;

            ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
            {
                position = position,
                applyShapeToPosition = true
            };
            ps.Emit(emitParams, count);
        }

        /// <summary>
        /// Emits <paramref name="count"/> particles at <paramref name="position"/>, overriding
        /// their initial velocity with <paramref name="velocity"/>. <br/>
        /// Every particle in the call receives this exact velocity, which suits directed streams
        /// such as tracers, drips, or a single ejected shell. For a spray that fans out, use
        /// <see cref="EmitFromSurface"/> so the shape module's cone supplies the spread.
        /// Non-finite input is rejected.
        /// </summary>
        public static void EmitAt(this ParticleSystem ps, Vector3 position, Vector3 velocity, int count)
        {
            if (count <= 0 || !IsFinite(position) || !IsFinite(velocity)) return;

            ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = velocity,
                applyShapeToPosition = true
            };
            ps.Emit(emitParams, count);
        }

        /// <summary>
        /// Emits <paramref name="count"/> particles at <paramref name="position"/>, tinted with
        /// <paramref name="color"/>. <br/>
        /// Lets one shared impact system produce per-surface results (grey dust on concrete,
        /// brown chips on wood, green blood on aliens) without authoring a separate system for
        /// each material.
        /// </summary>
        public static void EmitAt(this ParticleSystem ps, Vector3 position, Color32 color, int count)
        {
            if (count <= 0 || !IsFinite(position)) return;

            ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
            {
                position = position,
                startColor = color,
                applyShapeToPosition = true
            };
            ps.Emit(emitParams, count);
        }

        /// <summary>
        /// Moves the system to <paramref name="point"/> and aims its local <c>+Z</c> along
        /// <paramref name="normal"/>, then emits <paramref name="count"/> particles. <br/>
        /// This is the standard way to make sparks and debris spray out of a wall from a single
        /// shared cone- or hemisphere-shaped system.
        /// <para>
        /// Requires <see cref="ParticleSystemSimulationSpace.World"/>. In local space, particles
        /// from earlier impacts would be dragged along to the new location. A degenerate normal
        /// falls back to <see cref="Vector3.up"/>. Normals parallel to world up switch to a
        /// <see cref="Vector3.forward"/> up hint, which keeps the rotation stable for floor and
        /// ceiling hits.
        /// </para>
        /// </summary>
        public static void EmitFromSurface(this ParticleSystem ps, Vector3 point, Vector3 normal, int count)
        {
            if (count <= 0 || !IsFinite(point) || !IsFinite(normal)) return;

            float sqrLen = normal.sqrMagnitude;
            Vector3 n = sqrLen > DirectionSqrEpsilon ? normal * (1f / Mathf.Sqrt(sqrLen)) : Vector3.up;
            Vector3 upHint = Mathf.Abs(n.y) > ParallelUpThreshold ? Vector3.forward : Vector3.up;

            ps.transform.SetPositionAndRotation(point, Quaternion.LookRotation(n, upHint));
            ps.Emit(count);
        }

        /// <summary>
        /// Convenience overload of <see cref="EmitFromSurface"/> that reads the point and normal
        /// from <paramref name="hit"/>. <br/>
        /// Ignores default <see cref="RaycastHit"/> entries with a <c>null</c> collider, such as the
        /// unused trailing slots of a <c>Physics.RaycastNonAlloc</c> buffer. Without this guard,
        /// sparks would appear at the world origin.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EmitFromHit(this ParticleSystem ps, RaycastHit hit, int count)
        {
            if (hit.collider == null) return;
            ps.EmitFromSurface(hit.point, hit.normal, count);
        }

        /// <summary>
        /// Enables or disables the emission module without stopping the system. <br/>
        /// Unlike a <c>Stop</c>/<c>Play</c> pair, toggling emission keeps the system's timeline
        /// running. Resuming is instant, and <see cref="ParticleSystem.MainModule.startDelay"/>
        /// and bursts do not replay. Ideal for jetpack thrust or a flamethrower bound to a held
        /// input.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetEmissionEnabled(this ParticleSystem ps, bool enabled)
        {
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = enabled;
        }

        /// <summary>
        /// Sets particles emitted per second, clamped to be non-negative. <br/>
        /// Writes <see cref="ParticleSystem.EmissionModule.rateOverTimeMultiplier"/> directly, so
        /// no <see cref="ParticleSystem.MinMaxCurve"/> is read or rebuilt. This makes it safe to
        /// call every frame to scale engine exhaust with throttle or a fire with its intensity.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetEmissionRate(this ParticleSystem ps, float particlesPerSecond)
        {
            if (!IsFinite(particlesPerSecond)) return;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTimeMultiplier = Mathf.Max(0f, particlesPerSecond);
        }

        /// <summary>
        /// Sets particles emitted per world unit travelled, clamped to be non-negative. <br/>
        /// Drives tire smoke, footstep dust, and ski spray that should scale with movement
        /// rather than time, so a stationary vehicle stops smoking automatically.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetEmissionRateOverDistance(this ParticleSystem ps, float particlesPerUnit)
        {
            if (!IsFinite(particlesPerUnit)) return;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverDistanceMultiplier = Mathf.Max(0f, particlesPerUnit);
        }

        /// <summary>
        /// Replaces the particle count of the burst at <paramref name="burstIndex"/>, clamped to
        /// be non-negative. Out-of-range indices are ignored. <br/>
        /// Scales explosion debris by quality tier or by explosion size, without allocating a
        /// replacement <c>Burst[]</c> via <see cref="ParticleSystem.EmissionModule.SetBursts(ParticleSystem.Burst[])"/>.
        /// </summary>
        public static void SetBurstCount(this ParticleSystem ps, int burstIndex, int count)
        {
            ParticleSystem.EmissionModule emission = ps.emission;
            if (burstIndex < 0 || burstIndex >= emission.burstCount) return;

            ParticleSystem.Burst burst = emission.GetBurst(burstIndex);
            burst.count = new ParticleSystem.MinMaxCurve(Mathf.Max(0, count));
            emission.SetBurst(burstIndex, burst);
        }

        #endregion

        #region Main Module Configuration

        /// <summary>
        /// Sets the lifetime, in seconds, of newly emitted particles, clamped to be non-negative. <br/>
        /// Useful for stretching a smoke column in high wind, or shortening effects on low-end
        /// hardware to reduce overdraw.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetStartLifetime(this ParticleSystem ps, float seconds)
        {
            if (!IsFinite(seconds)) return;
            ParticleSystem.MainModule main = ps.main;
            main.startLifetimeMultiplier = Mathf.Max(0f, seconds);
        }

        /// <summary>
        /// Sets the initial speed of newly emitted particles. <br/>
        /// Negative values are allowed on purpose: they pull particles inward toward the emitter
        /// shape, which is the classic "charging energy" or implosion look. Only non-finite
        /// input is rejected.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetStartSpeed(this ParticleSystem ps, float speed)
        {
            if (!IsFinite(speed)) return;
            ParticleSystem.MainModule main = ps.main;
            main.startSpeedMultiplier = speed;
        }

        /// <summary>
        /// Sets the initial size of newly emitted particles, clamped to be non-negative. <br/>
        /// Scales an explosion's visuals to match its gameplay damage radius, so a bigger blast
        /// both looks and plays bigger.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetStartSize(this ParticleSystem ps, float size)
        {
            if (!IsFinite(size)) return;
            ParticleSystem.MainModule main = ps.main;
            main.startSizeMultiplier = Mathf.Max(0f, size);
        }

        /// <summary>
        /// Sets the initial rotation of newly emitted particles, in <b>degrees</b>. <br/>
        /// <see cref="ParticleSystem.MainModule.startRotation"/> expects radians, even though the
        /// Inspector displays degrees. Passing degrees straight through makes particles spin to
        /// seemingly random angles; this method converts internally.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetStartRotation(this ParticleSystem ps, float degrees)
        {
            if (!IsFinite(degrees)) return;
            ParticleSystem.MainModule main = ps.main;
            main.startRotationMultiplier = degrees * Mathf.Deg2Rad;
        }

        /// <summary>
        /// Sets a constant start color for newly emitted particles. <br/>
        /// This is write-only by design. It replaces any gradient mode with a single color and
        /// never reads the existing <see cref="ParticleSystem.MinMaxGradient"/>, which would
        /// allocate a managed <see cref="Gradient"/> copy. Ideal for team-colored muzzle flashes
        /// or element-tinted spell effects.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetStartColor(this ParticleSystem ps, Color color)
        {
            ParticleSystem.MainModule main = ps.main;
            main.startColor = color;
        }

        /// <summary>
        /// Sets how strongly <see cref="Physics.gravity"/> affects particles. <br/>
        /// Negative values make particles rise, as with embers, bubbles, or anti-gravity zones.
        /// Only non-finite input is rejected.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetGravityModifier(this ParticleSystem ps, float modifier)
        {
            if (!IsFinite(modifier)) return;
            ParticleSystem.MainModule main = ps.main;
            main.gravityModifierMultiplier = modifier;
        }

        /// <summary>
        /// Sets this system's playback speed multiplier, clamped to be non-negative. <br/>
        /// Enables per-effect slow motion (bullet-time sparks, a frozen explosion during a kill
        /// cam) without touching <see cref="Time.timeScale"/>, which would slow the entire game.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetSimulationSpeed(this ParticleSystem ps, float speed)
        {
            if (!IsFinite(speed)) return;
            ParticleSystem.MainModule main = ps.main;
            main.simulationSpeed = Mathf.Max(0f, speed);
        }

        /// <summary>
        /// Enables or disables looping. <br/>
        /// Turning looping off on a playing system lets it finish its current cycle and then stop
        /// on its own. This gives a graceful wind-down, such as a torch sputtering out, instead
        /// of an abrupt <c>Stop</c>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetLooping(this ParticleSystem ps, bool loop)
        {
            ParticleSystem.MainModule main = ps.main;
            main.loop = loop;
        }

        /// <summary>
        /// Caps the number of simultaneously live particles, clamped to be non-negative. <br/>
        /// The primary lever for particle quality tiers on mobile and low-end hardware, since the
        /// native particle buffer and fill-rate cost both scale with this value.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetMaxParticles(this ParticleSystem ps, int maxParticles)
        {
            ParticleSystem.MainModule main = ps.main;
            main.maxParticles = Mathf.Max(0, maxParticles);
        }

        /// <summary>
        /// Sets <see cref="ParticleSystem.MainModule.duration"/> only if the system is fully
        /// stopped with no live particles. The value is clamped to Unity's minimum of
        /// <c>0.05</c> seconds. <br/>
        /// Unity logs an error and ignores the change if the duration is set while the system is
        /// playing. This method returns <c>false</c> instead, so pooling code can call
        /// <see cref="StopAndClear"/> first and retry, rather than silently keeping the old duration.
        /// </summary>
        public static bool TrySetDuration(this ParticleSystem ps, float seconds)
        {
            if (!IsFinite(seconds) || !IsSafeToReconfigure(ps)) return false;

            ParticleSystem.MainModule main = ps.main;
            main.duration = Mathf.Max(MinDuration, seconds);
            return true;
        }

        /// <summary>
        /// Disables auto-seeding and sets a fixed <see cref="ParticleSystem.randomSeed"/>, but
        /// only if the system is fully stopped with no live particles. <br/>
        /// Fixed seeds make effects deterministic. Replays, kill cams, and lockstep netcode can
        /// then reproduce an explosion particle-for-particle instead of showing a different
        /// random pattern on every playback.
        /// </summary>
        public static bool TrySetRandomSeed(this ParticleSystem ps, uint seed)
        {
            if (!IsSafeToReconfigure(ps)) return false;

            ps.useAutoRandomSeed = false;
            ps.randomSeed = seed;
            return true;
        }

        #endregion

        #region Shape Module Configuration

        /// <summary>
        /// Sets the emitter shape radius, clamped to be non-negative. <br/>
        /// Keeps an AoE spell's ground effect or a healing aura exactly matched to its gameplay
        /// radius, so players can trust the visual as a readable hit area.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetShapeRadius(this ParticleSystem ps, float radius)
        {
            if (!IsFinite(radius)) return;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.radius = Mathf.Max(0f, radius);
        }

        /// <summary>
        /// Sets the cone angle in degrees, clamped to the supported <c>0</c>–<c>90</c> range. <br/>
        /// Syncs a shotgun or flamethrower's visual cone to the weapon's actual spread angle, so
        /// the effect never suggests more or less coverage than the hitscan logic provides.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetShapeAngle(this ParticleSystem ps, float degrees)
        {
            if (!IsFinite(degrees)) return;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.angle = Mathf.Clamp(degrees, 0f, MaxConeAngle);
        }

        /// <summary>
        /// Sets the emission arc in degrees, clamped to the supported <c>0</c>–<c>360</c> range. <br/>
        /// Matches sweep-attack trails, shield arcs, and partial-ring shockwaves to the arc used
        /// by the underlying hit detection.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetShapeArc(this ParticleSystem ps, float degrees)
        {
            if (!IsFinite(degrees)) return;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.arc = Mathf.Clamp(degrees, 0f, MaxArcAngle);
        }

        #endregion

        #region Queries & State

        /// <summary>
        /// True once the system has stopped emitting and every particle, including those on
        /// child sub-emitters, has died. <br/>
        /// This is the correct check before returning a pooled effect. Checking only the root's
        /// <see cref="ParticleSystem.isPlaying"/> can recycle the object while child sparks are
        /// still in flight, cutting them off mid-air.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFinished(this ParticleSystem ps, bool withChildren = true)
            => !ps.IsAlive(withChildren);

        /// <summary>
        /// True if at least one particle is currently alive on this system (children excluded). <br/>
        /// A cheap early-out before particle buffer reads, avoiding a
        /// <see cref="ParticleSystem.GetParticles(ParticleSystem.Particle[])"/> round trip when
        /// there is nothing to process.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasLiveParticles(this ParticleSystem ps) => ps.particleCount > 0;

        /// <summary>
        /// True if particles simulate in world space. <br/>
        /// Shared impact systems driven by <see cref="EmitAt(ParticleSystem, Vector3, int)"/> or
        /// <see cref="EmitFromSurface"/> require world space. Assert this at startup to catch
        /// a misconfigured prefab before it sends every impact to the wrong place.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsWorldSpace(this ParticleSystem ps)
            => ps.main.simulationSpace == ParticleSystemSimulationSpace.World;

        /// <summary>
        /// Playback position within the current cycle, normalized to <c>0</c>–<c>1</c>. <br/>
        /// Syncs companion effects, such as a light flash intensity curve or a camera shake
        /// envelope, to the particle system's own timeline instead of maintaining a parallel timer.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetNormalizedTime(this ParticleSystem ps)
        {
            float duration = ps.main.duration;
            return duration > 0f ? Mathf.Clamp01(ps.time / duration) : 0f;
        }

        /// <summary>
        /// Conservative upper bound, in seconds, on how long a one-shot effect stays visible:
        /// start delay plus duration plus maximum particle lifetime, scaled by
        /// <see cref="ParticleSystem.MainModule.simulationSpeed"/>. <br/>
        /// Lets a pool schedule a single return timer instead of polling
        /// <see cref="IsFinished"/> on every active effect every frame.
        /// <list type="bullet">
        /// <item>Returns <c>false</c> for looping systems and for a simulation speed of zero,
        /// since neither ever finishes.</item>
        /// <item>The estimate is exact for Constant and Random Between Two Constants lifetimes,
        /// and for curves normalized to <c>0</c>–<c>1</c> (the editor default).</item>
        /// <item>Sub-emitter lifetimes are not included; use <see cref="IsFinished"/> for
        /// effects with long-lived children.</item>
        /// </list>
        /// </summary>
        public static bool TryGetMaxEffectDuration(this ParticleSystem ps, out float seconds)
        {
            ParticleSystem.MainModule main = ps.main;
            float speed = main.simulationSpeed;
            if (main.loop || speed <= 0f)
            {
                seconds = 0f;
                return false;
            }

            seconds = (main.startDelayMultiplier + main.duration + main.startLifetimeMultiplier) / speed;
            return true;
        }

        /// <summary>
        /// Safe wrapper around <see cref="TryGetMaxEffectDuration"/> that returns
        /// <paramref name="fallback"/> for looping or frozen systems. <br/>
        /// Convenient for one-line pool timers, where a sensible cap (for example <c>5</c>
        /// seconds) is better than an effect that never returns to the pool.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetMaxEffectDurationOr(this ParticleSystem ps, float fallback)
            => ps.TryGetMaxEffectDuration(out float seconds) ? seconds : fallback;

        #endregion

        #region Particle Buffer Access

        /// <summary>
        /// Minimum buffer length needed to read every particle this system can hold. <br/>
        /// Allocate your <c>ParticleSystem.Particle[]</c> once, in <c>Awake</c>, using this size.
        /// Then reuse it for every <see cref="ReadParticles"/> call so per-frame particle
        /// manipulation stays allocation-free.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetRequiredParticleBufferSize(this ParticleSystem ps) => ps.main.maxParticles;

        /// <summary>
        /// True if <paramref name="buffer"/> is non-null and large enough to hold every particle
        /// the system can contain. <br/>
        /// Assert this after changing <see cref="ParticleSystem.MainModule.maxParticles"/> at
        /// runtime, such as when a quality tier changes. An undersized buffer silently truncates
        /// reads, and writing it back deletes the missing particles.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsBufferLargeEnough(this ParticleSystem ps, ParticleSystem.Particle[] buffer)
            => buffer != null && buffer.Length >= ps.main.maxParticles;

        /// <summary>
        /// Copies live particles into a caller-owned <paramref name="buffer"/> and returns how
        /// many were read, or <c>0</c> if the buffer is <c>null</c>. <br/>
        /// This is the foundation for particle attractors, such as souls or XP orbs that home in
        /// on the player, and for custom per-particle collision.
        /// <para>
        /// If the buffer is smaller than <see cref="ParticleSystem.particleCount"/>, only the
        /// first particles are read. <see cref="WriteParticles"/> replaces the entire particle
        /// set, so writing back a truncated read destroys the remainder. Size the buffer with
        /// <see cref="GetRequiredParticleBufferSize"/>.
        /// </para>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReadParticles(this ParticleSystem ps, ParticleSystem.Particle[] buffer)
            => buffer == null ? 0 : ps.GetParticles(buffer);

        /// <summary>
        /// Writes the first <paramref name="count"/> particles of <paramref name="buffer"/> back
        /// to the system, replacing all live particles. <br/>
        /// <paramref name="count"/> is clamped to <c>0</c>–<c>buffer.Length</c>, so an off-by-one
        /// in attractor or culling code cannot read past the array or pass a negative size to
        /// the native API. A <c>null</c> buffer is ignored.
        /// </summary>
        public static void WriteParticles(this ParticleSystem ps, ParticleSystem.Particle[] buffer, int count)
        {
            if (buffer == null) return;
            ps.SetParticles(buffer, Mathf.Clamp(count, 0, buffer.Length));
        }

        #endregion

        #region Internal Guards

        /// <summary>
        /// True if <paramref name="value"/> is neither <c>NaN</c> nor <c>Infinity</c>. <br/>
        /// Implemented manually rather than with <c>float.IsFinite</c> so it compiles against
        /// every Unity API compatibility level.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFinite(float value)
            => !float.IsNaN(value) && !float.IsInfinity(value);

        /// <summary>
        /// True if every component of <paramref name="value"/> is finite. <br/>
        /// Guards emission positions and velocities, since one non-finite particle invalidates
        /// the renderer's bounds and breaks culling for the whole system.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFinite(Vector3 value)
            => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        /// <summary>
        /// True if the system is not playing and has no live particles. <br/>
        /// Properties such as <see cref="ParticleSystem.MainModule.duration"/> and
        /// <see cref="ParticleSystem.randomSeed"/> can only be changed in this state; attempting
        /// the change earlier logs an error and leaves the old value in place.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsSafeToReconfigure(ParticleSystem ps)
            => !ps.isPlaying && ps.particleCount == 0;

        #endregion
    }
}