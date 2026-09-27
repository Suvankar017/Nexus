using System.Collections;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Nexus.Core.Extensions
{
    /// <summary>
    /// Extension methods for <see cref="AudioSource"/> covering safe playback, volume and
    /// pitch control, fades, spatial configuration, and pooling support.
    /// <para>Design rules:</para>
    /// <list type="bullet">
    /// <item>Every play method guards against a <c>null</c> <see cref="AudioSource.clip"/>.
    /// Calling <see cref="AudioSource.Play()"/> with no clip assigned throws no exception
    /// but silently does nothing; these guards make that failure explicit and inspectable
    /// via a returned <c>bool</c> instead of a mysteriously silent sound effect.</item>
    /// <item>Volume and pitch setters clamp to Unity's valid ranges
    /// (<c>0</c>–<c>1</c> for volume, <c>-3</c>–<c>3</c> for pitch) and reject <c>NaN</c>,
    /// since either value silently breaks audio output with no console warning.</item>
    /// <item>The <c>FadeVolumeTo</c>/<c>FadeIn</c>/<c>FadeOut</c>/<c>CrossFadeTo</c> methods
    /// return <see cref="IEnumerator"/> for use with
    /// <see cref="MonoBehaviour.StartCoroutine(IEnumerator)"/>. Like the fade helpers in
    /// <c>CanvasGroupExtensions</c>, this is a deliberate exception to the zero-allocation
    /// rule: audio fades run once per transition, not per frame across hundreds of sources.</item>
    /// <item>Pitch-variance helpers exist because identical pitch on every repeated sound
    /// effect (footsteps, gunshots, hits) reads as fake and repetitive; a small random
    /// range makes an audio pool sound organic at negligible cost.</item>
    /// <item>Pooling helpers fully reset a source's transient state (volume, pitch, clip)
    /// so a reused <see cref="AudioSource"/> never leaks configuration from its previous owner.</item>
    /// </list>
    /// </summary>
    public static class AudioSourceExtensions
    {
        /// <summary>
        /// Minimum valid value for <see cref="AudioSource.pitch"/>, as enforced by Unity.
        /// </summary>
        private const float MinPitch = -3f;

        /// <summary>
        /// Maximum valid value for <see cref="AudioSource.pitch"/>, as enforced by Unity.
        /// </summary>
        private const float MaxPitch = 3f;

        /// <summary>
        /// Unity's default, unmodified playback pitch. Used to restore a source after
        /// temporary pitch effects such as slow-motion or randomized variance.
        /// </summary>
        private const float DefaultPitch = 1f;

        /// <summary>
        /// Spatial blend value representing a fully 2D (non-positional) sound, such as UI
        /// clicks or music.
        /// </summary>
        private const float SpatialBlend2D = 0f;

        /// <summary>
        /// Spatial blend value representing a fully 3D (positional) sound, such as a
        /// footstep or an explosion in the world.
        /// </summary>
        private const float SpatialBlend3D = 1f;

        #region Safe Playback

        /// <summary>
        /// Plays <see cref="AudioSource.clip"/> from the start, returning <c>false</c>
        /// instead of silently doing nothing if no clip is assigned. <br/>
        /// Turns a mysteriously missing sound effect into an inspectable return value that
        /// calling code (or an automated test) can assert on, rather than a bug that only
        /// shows up as "I didn't hear anything" in playtesting.
        /// </summary>
        public static bool PlaySafe(this AudioSource source)
        {
            if (source.clip == null) return false;
            source.Play();
            return true;
        }

        /// <summary>
        /// Assigns <paramref name="clip"/> and plays it from the start, returning
        /// <c>false</c> instead of throwing if <paramref name="clip"/> is <c>null</c>. <br/>
        /// The standard way to trigger a one-off voice line or ability sound where the clip
        /// is chosen at runtime (e.g. from a randomized barks array).
        /// </summary>
        public static bool PlaySafe(this AudioSource source, AudioClip clip)
        {
            if (clip == null) return false;
            source.clip = clip;
            source.Play();
            return true;
        }

        /// <summary>
        /// Plays <paramref name="clip"/> once via <see cref="AudioSource.PlayOneShot(AudioClip, float)"/>
        /// without disturbing <see cref="AudioSource.clip"/> or looping state, returning
        /// <c>false</c> if <paramref name="clip"/> is <c>null</c>. <br/>
        /// Safe for overlapping impact and hit sounds fired from a single shared source,
        /// since one-shots do not interrupt each other or the source's main clip.
        /// </summary>
        public static bool PlayOneShotSafe(this AudioSource source, AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null) return false;
            source.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
            return true;
        }

        /// <summary>
        /// Plays <see cref="AudioSource.clip"/> only if the source is not already playing it. <br/>
        /// Prevents a looping ambience or engine-hum sound from restarting from the
        /// beginning every time an update loop calls <c>Play</c>, which would otherwise
        /// produce an audible stutter or click each frame.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void PlayIfNotPlaying(this AudioSource source)
        {
            if (!source.isPlaying) source.PlaySafe();
        }

        /// <summary>
        /// Stops the source and plays its clip again from the start. <br/>
        /// Unlike a raw <see cref="AudioSource.Play()"/> call, which restarts a clip
        /// already in progress without issue, this method exists for readability at call
        /// sites that specifically intend to interrupt and restart — for example re-triggering
        /// a hit-reaction voice line when a new hit lands mid-clip.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Restart(this AudioSource source)
        {
            source.Stop();
            source.PlaySafe();
        }

        #endregion

        #region Volume & Pitch Control

        /// <summary>
        /// Sets <see cref="AudioSource.volume"/>, clamped to <c>0</c>–<c>1</c> and rejecting
        /// <c>NaN</c>. <br/>
        /// A raw <c>NaN</c> volume silently mutes the source with no console warning;
        /// guarding here stops a bad interpolation value (e.g. from a divide-by-zero in a
        /// custom mixer curve) from ever reaching audio output.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetVolumeClamped(this AudioSource source, float volume)
        {
            if (float.IsNaN(volume)) return;
            source.volume = Mathf.Clamp01(volume);
        }

        /// <summary>
        /// Adds <paramref name="delta"/> to the source's current volume, clamped to
        /// <c>0</c>–<c>1</c>. <br/>
        /// Drives manual per-frame fades (<c>source.AddVolume(fadeSpeed * Time.deltaTime)</c>)
        /// without external tweening libraries, while guaranteeing the result never leaves
        /// the valid volume range.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddVolume(this AudioSource source, float delta)
        {
            if (float.IsNaN(delta)) return;
            source.volume = Mathf.Clamp01(source.volume + delta);
        }

        /// <summary>
        /// Sets <see cref="AudioSource.pitch"/>, clamped to Unity's supported
        /// <c>-3</c>–<c>3</c> range and rejecting <c>NaN</c>. <br/>
        /// An out-of-range or <c>NaN</c> pitch is silently clamped or ignored by the audio
        /// engine with no error; this guard makes that boundary explicit in code instead of
        /// relying on undocumented native behavior.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetPitchClamped(this AudioSource source, float pitch)
        {
            if (float.IsNaN(pitch)) return;
            source.pitch = Mathf.Clamp(pitch, MinPitch, MaxPitch);
        }

        /// <summary>
        /// Sets <see cref="AudioSource.pitch"/> to a random value within
        /// <paramref name="minPitch"/>–<paramref name="maxPitch"/>. <br/>
        /// Identical pitch on every repeated footstep, gunshot, or impact sound reads as
        /// fake and repetitive; a small random range (e.g. <c>0.95</c>–<c>1.05</c>) makes a
        /// pooled sound effect feel organic at negligible cost.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RandomizePitch(this AudioSource source, float minPitch = 0.95f, float maxPitch = 1.05f)
            => source.pitch = Random.Range(minPitch, maxPitch);

        /// <summary>
        /// Restores <see cref="AudioSource.pitch"/> to <c>1</c>. <br/>
        /// Pairs with <see cref="RandomizePitch"/> or a temporary slow-motion effect to
        /// guarantee pitch cannot be left permanently altered if the code that changed it
        /// exits early or the source is returned to a pool.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ResetPitch(this AudioSource source) => source.pitch = DefaultPitch;

        /// <summary>
        /// Mutes or unmutes the source without altering its stored
        /// <see cref="AudioSource.volume"/>. <br/>
        /// Preferred over manually zeroing and restoring volume, since that approach loses
        /// the original value if two systems (e.g. a settings menu and a cutscene) both try
        /// to mute the same source independently.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetMuted(this AudioSource source, bool muted) => source.mute = muted;

        #endregion

        #region Fade Transitions

        /// <summary>
        /// Coroutine that linearly interpolates <see cref="AudioSource.volume"/> from its
        /// current value to <paramref name="targetVolume"/> over <paramref name="duration"/>
        /// seconds. <br/>
        /// Run with <see cref="MonoBehaviour.StartCoroutine(IEnumerator)"/>. Non-finite or
        /// non-positive durations, and non-finite target values, resolve the volume
        /// immediately instead of dividing by zero or looping forever.
        /// </summary>
        public static IEnumerator FadeVolumeTo(this AudioSource source, float targetVolume, float duration, bool useUnscaledTime = false)
        {
            float target = float.IsNaN(targetVolume) ? source.volume : Mathf.Clamp01(targetVolume);

            if (float.IsNaN(duration) || duration <= 0f)
            {
                source.volume = target;
                yield break;
            }

            float start = source.volume;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                source.volume = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            source.volume = target;
        }

        /// <summary>
        /// Coroutine that starts playback immediately at zero volume, then fades up to
        /// <paramref name="targetVolume"/> over <paramref name="duration"/> seconds. <br/>
        /// The standard technique for music and ambience that should ease in rather than
        /// snap to full volume, avoiding a jarring pop when a new track or zone loop begins.
        /// </summary>
        public static IEnumerator FadeIn(this AudioSource source, float targetVolume, float duration, bool useUnscaledTime = false)
        {
            source.volume = 0f;
            source.PlaySafe();
            yield return source.FadeVolumeTo(targetVolume, duration, useUnscaledTime);
        }

        /// <summary>
        /// Coroutine that fades <see cref="AudioSource.volume"/> to <c>0</c> over
        /// <paramref name="duration"/> seconds, then stops playback and restores the
        /// source's original volume. <br/>
        /// Restoring the original volume after stopping — rather than leaving it at zero —
        /// is what makes this method safe to call on a pooled source: the next reuse starts
        /// clean instead of inheriting a silent volume from the previous fade-out.
        /// </summary>
        public static IEnumerator FadeOut(this AudioSource source, float duration, bool useUnscaledTime = false)
        {
            float originalVolume = source.volume;
            yield return source.FadeVolumeTo(0f, duration, useUnscaledTime);
            source.Stop();
            source.volume = originalVolume;
        }

        /// <summary>
        /// Coroutine that simultaneously fades <paramref name="from"/> out and
        /// <paramref name="to"/> in over <paramref name="duration"/> seconds. <br/>
        /// <paramref name="to"/> is started at volume <c>0</c> before the fade begins. The
        /// standard way to blend between two music tracks or ambience layers (e.g. combat
        /// music taking over from exploration music) without an audible gap or overlap spike.
        /// </summary>
        public static IEnumerator CrossFadeTo(this AudioSource from, AudioSource to, float duration, bool useUnscaledTime = false)
        {
            float targetVolume = to.volume;
            to.volume = 0f;
            to.PlaySafe();

            if (float.IsNaN(duration) || duration <= 0f)
            {
                from.volume = 0f;
                from.Stop();
                to.volume = targetVolume;
                yield break;
            }

            float fromStart = from.volume;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                from.volume = Mathf.Lerp(fromStart, 0f, t);
                to.volume = Mathf.Lerp(0f, targetVolume, t);
                yield return null;
            }

            from.volume = 0f;
            from.Stop();
            to.volume = targetVolume;
        }

        #endregion

        #region Spatial & 3D Configuration

        /// <summary>
        /// Sets <see cref="AudioSource.spatialBlend"/> to fully 2D (non-positional). <br/>
        /// The correct setting for UI feedback, music, and announcer voice lines that
        /// should play at constant volume regardless of listener position.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Set2D(this AudioSource source) => source.spatialBlend = SpatialBlend2D;

        /// <summary>
        /// Sets <see cref="AudioSource.spatialBlend"/> to fully 3D (positional). <br/>
        /// The correct setting for footsteps, impacts, and any sound whose volume and
        /// stereo panning should change with distance and direction from the listener.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Set3D(this AudioSource source) => source.spatialBlend = SpatialBlend3D;

        /// <summary>
        /// Sets <see cref="AudioSource.minDistance"/> and <see cref="AudioSource.maxDistance"/>,
        /// guaranteeing <c>min &lt;= max</c> regardless of argument order. <br/>
        /// Unity does not enforce this ordering itself; passing them reversed silently
        /// produces broken attenuation where the sound is loud everywhere or fades
        /// immediately. Swapping here removes that entire class of configuration bug.
        /// </summary>
        public static void SetDistanceRange(this AudioSource source, float minDistance, float maxDistance)
        {
            if (minDistance > maxDistance) (minDistance, maxDistance) = (maxDistance, minDistance);
            source.minDistance = Mathf.Max(0f, minDistance);
            source.maxDistance = Mathf.Max(source.minDistance, maxDistance);
        }

        /// <summary>
        /// Sets <see cref="AudioSource.dopplerLevel"/>, clamped to Unity's supported
        /// <c>0</c>–<c>5</c> range. <br/>
        /// Zeroing this out is a common fix for high-speed projectiles or vehicles whose
        /// pitch otherwise warps unpleasantly as they pass the listener.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetDopplerLevel(this AudioSource source, float dopplerLevel)
            => source.dopplerLevel = Mathf.Clamp(dopplerLevel, 0f, 5f);

        #endregion

        #region One-Shot Variation

        /// <summary>
        /// Plays <paramref name="clip"/> once at a randomized pitch, then restores the
        /// source's original pitch afterward. <br/>
        /// Because <see cref="AudioSource.PlayOneShot(AudioClip, float)"/> shares the
        /// source's single <see cref="AudioSource.pitch"/> value rather than accepting its
        /// own, this method captures and restores that value so the randomization applied
        /// to one impact sound cannot leak into an unrelated sound played moments later on
        /// the same source.
        /// </summary>
        public static bool PlayOneShotWithPitchVariance(this AudioSource source, AudioClip clip, float minPitch = 0.95f, float maxPitch = 1.05f, float volumeScale = 1f)
        {
            if (clip == null) return false;

            float originalPitch = source.pitch;
            source.pitch = Random.Range(minPitch, maxPitch);
            source.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
            source.pitch = originalPitch;
            return true;
        }

        #endregion

        #region Queries & State

        /// <summary>
        /// True if <see cref="AudioSource.clip"/> is assigned. <br/>
        /// Cheap precondition check before calling any playback method, avoiding a wasted
        /// call to <see cref="PlaySafe()"/> whose <c>bool</c> result would otherwise need
        /// to be checked anyway.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasClip(this AudioSource source) => source.clip != null;

        /// <summary>
        /// True if the source is currently playing <paramref name="clip"/> specifically,
        /// rather than any clip. <br/>
        /// Distinguishes "this exact sound is already playing" (useful to avoid restarting
        /// a looping ambience) from the coarser <see cref="AudioSource.isPlaying"/>, which
        /// is true for whatever clip happens to be assigned.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsPlayingClip(this AudioSource source, AudioClip clip)
            => source.isPlaying && source.clip == clip;

        /// <summary>
        /// Seconds remaining before the current clip finishes, or <c>0</c> if nothing is
        /// playing or the pitch is non-positive. <br/>
        /// Divides by <see cref="AudioSource.pitch"/> so the estimate stays accurate when a
        /// clip is being played back slowed down or sped up, which shortens or lengthens
        /// its real-world remaining duration. Lets pooling code schedule a return timer
        /// instead of polling <see cref="AudioSource.isPlaying"/> every frame.
        /// </summary>
        public static float GetRemainingTime(this AudioSource source)
        {
            if (!source.isPlaying || source.clip == null || source.pitch <= 0f) return 0f;

            float remainingAtNormalSpeed = source.clip.length - source.time;
            return remainingAtNormalSpeed / source.pitch;
        }

        /// <summary>
        /// True if the source is not playing and not looping. <br/>
        /// The correct "safe to return to pool" check: a looping source reports
        /// <see cref="AudioSource.isPlaying"/> as <c>true</c> forever, but the inverse case
        /// — a non-looping source that has simply reached the end of its clip — also
        /// correctly reports <c>false</c> and should not be confused with a source that was
        /// deliberately stopped early for reuse elsewhere.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFinished(this AudioSource source) => !source.isPlaying && !source.loop;

        #endregion

        #region Pooling

        /// <summary>
        /// Stops playback and resets clip, volume, pitch, and loop state to their defaults. <br/>
        /// Required before returning an <see cref="AudioSource"/> to an object pool.
        /// Without this, a reused source can silently inherit a previous sound's clip,
        /// leftover fade volume, or randomized pitch, producing a mysteriously wrong-sounding
        /// effect the next time it plays.
        /// </summary>
        public static void ResetForPool(this AudioSource source)
        {
            source.Stop();
            source.clip = null;
            source.volume = 1f;
            source.pitch = DefaultPitch;
            source.loop = false;
            source.mute = false;
        }

        #endregion
    }
}