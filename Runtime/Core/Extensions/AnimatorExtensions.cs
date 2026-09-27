using System.Runtime.CompilerServices;
using UnityEngine;

namespace Nexus.Core.Extensions
{
    /// <summary>
    /// Extension methods for <see cref="Animator"/> covering safe parameter access, state
    /// queries, layer weight control, and playback speed.
    /// <para>Design rules:</para>
    /// <list type="bullet">
    /// <item>Callers are expected to cache parameter and state hashes (via
    /// <see cref="Animator.StringToHash(string)"/>) once, typically in <c>Awake</c>, and
    /// pass the resulting <c>int</c> into these methods. None of these methods perform
    /// string hashing internally, which would otherwise cost CPU time every call.</item>
    /// <item><see cref="Animator.parameters"/> allocates and returns a full copy of the
    /// parameter array on every access. All existence checks here instead iterate via
    /// <see cref="Animator.GetParameter(int)"/> and <see cref="Animator.parameterCount"/>,
    /// avoiding that hidden per-call allocation.</item>
    /// <item>Safe setters no-op instead of throwing or logging a console warning when a
    /// parameter is missing — essential when multiple character types share one
    /// Animator Controller but only some of them define a given parameter (e.g. a
    /// ranged-only "AimWeight" float).</item>
    /// <item>State and transition queries operate purely on hashes and
    /// <see cref="AnimatorStateInfo"/>; none of them allocate.</item>
    /// </list>
    /// </summary>
    public static class AnimatorExtensions
    {
        /// <summary>
        /// Default normalized-time threshold above which a non-looping clip is considered
        /// finished. Used by <see cref="HasFinishedClip"/> when no explicit threshold is
        /// supplied.
        /// </summary>
        private const float DefaultEndOfClipThreshold = 0.95f;

        #region Parameter Existence Checks

        /// <summary>
        /// True if <paramref name="animator"/> defines a parameter whose hash matches
        /// <paramref name="parameterHash"/>. <br/>
        /// Iterates via <see cref="Animator.GetParameter(int)"/> rather than the
        /// allocating <see cref="Animator.parameters"/> property, so it is safe to call
        /// during gameplay rather than only at initialization. Still, prefer caching the
        /// boolean result once per controller/character combination rather than calling
        /// this every frame for many characters.
        /// </summary>
        public static bool HasParameter(this Animator animator, int parameterHash)
        {
            int count = animator.parameterCount;
            for (int i = 0; i < count; i++)
            {
                if (animator.GetParameter(i).nameHash == parameterHash) return true;
            }

            return false;
        }

        /// <summary>
        /// True if <paramref name="animator"/> defines a parameter with hash
        /// <paramref name="parameterHash"/> and the exact <paramref name="type"/>. <br/>
        /// Prevents an <see cref="System.Exception"/> or silent no-op that occurs when
        /// calling, for example, <see cref="Animator.SetFloat(int, float)"/> on a
        /// parameter that actually exists as a <c>bool</c> — a common bug when several
        /// character variants share one Animator Controller with slightly different
        /// parameter schemas.
        /// </summary>
        public static bool HasParameterOfType(this Animator animator, int parameterHash, AnimatorControllerParameterType type)
        {
            int count = animator.parameterCount;
            for (int i = 0; i < count; i++)
            {
                AnimatorControllerParameter p = animator.GetParameter(i);
                if (p.nameHash == parameterHash) return p.type == type;
            }

            return false;
        }

        #endregion

        #region Safe Parameter Setters

        /// <summary>
        /// Sets a float parameter only if it exists on <paramref name="animator"/>. <br/>
        /// Avoids the Console warning ("Parameter ... does not exist") that spams the log
        /// when a shared Animator Controller is reused across character types that don't
        /// all define the same blend-tree inputs.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetFloatSafe(this Animator animator, int parameterHash, float value)
        {
            if (animator.HasParameter(parameterHash)) animator.SetFloat(parameterHash, value);
        }

        /// <summary>
        /// Sets a float parameter with damping only if it exists on
        /// <paramref name="animator"/>. <br/>
        /// The damped overload is the standard way to smooth locomotion blend values
        /// (e.g. speed, turn angle) without visible snapping between animation states.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetFloatSafe(this Animator animator, int parameterHash, float value, float dampTime, float deltaTime)
        {
            if (animator.HasParameter(parameterHash)) animator.SetFloat(parameterHash, value, dampTime, deltaTime);
        }

        /// <summary>
        /// Sets a bool parameter only if it exists on <paramref name="animator"/>. <br/>
        /// Lets gameplay code drive optional state flags (e.g. "IsBlocking") generically
        /// across character types without branching on which controller is currently assigned.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetBoolSafe(this Animator animator, int parameterHash, bool value)
        {
            if (animator.HasParameter(parameterHash)) animator.SetBool(parameterHash, value);
        }

        /// <summary>
        /// Sets an integer parameter only if it exists on <paramref name="animator"/>. <br/>
        /// Useful for combo-index or weapon-type selectors shared across a family of
        /// Animator Controllers that don't all implement the same combo depth.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetIntegerSafe(this Animator animator, int parameterHash, int value)
        {
            if (animator.HasParameter(parameterHash)) animator.SetInteger(parameterHash, value);
        }

        /// <summary>
        /// Fires a trigger parameter only if it exists on <paramref name="animator"/>. <br/>
        /// Prevents a missing "Attack" or "Hit" trigger on a stripped-down enemy variant
        /// from throwing off the rest of a shared ability system.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetTriggerSafe(this Animator animator, int parameterHash)
        {
            if (animator.HasParameter(parameterHash)) animator.SetTrigger(parameterHash);
        }

        /// <summary>
        /// Resets a trigger parameter only if it exists on <paramref name="animator"/>. <br/>
        /// Pair with <see cref="SetTriggerSafe"/> when cancelling a queued action — for
        /// example clearing a buffered "Attack" trigger the instant the player is staggered.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ResetTriggerSafe(this Animator animator, int parameterHash)
        {
            if (animator.HasParameter(parameterHash)) animator.ResetTrigger(parameterHash);
        }

        #endregion

        #region State & Transition Queries

        /// <summary>
        /// True if the state currently playing on <paramref name="layer"/> matches
        /// <paramref name="stateHash"/>. <br/>
        /// The standard gate for action-locking logic — e.g. "don't allow a dodge while
        /// already in the Stagger state" — without needing a separate bool flag mirrored
        /// from the Animator Controller.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsInState(this Animator animator, int stateHash, int layer = 0)
            => animator.GetCurrentAnimatorStateInfo(layer).shortNameHash == stateHash;

        /// <summary>
        /// True if <paramref name="animator"/> is currently transitioning into the state
        /// matching <paramref name="stateHash"/> on <paramref name="layer"/>. <br/>
        /// Lets gameplay code react a frame early — for example enabling a hit-box the
        /// instant a transition into an "Attack" state begins, rather than waiting for it
        /// to fully complete.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsTransitioningTo(this Animator animator, int stateHash, int layer = 0)
            => animator.IsInTransition(layer) && animator.GetNextAnimatorStateInfo(layer).shortNameHash == stateHash;

        /// <summary>
        /// Fractional playback position (<c>0</c>–<c>1</c>) of the current state on
        /// <paramref name="layer"/>, with any completed loops stripped out. <br/>
        /// Raw <see cref="AnimatorStateInfo.normalizedTime"/> keeps counting past <c>1</c>
        /// on looping clips (<c>2.35</c>, <c>7.1</c>, etc.), which breaks direct comparisons
        /// against a fixed threshold; this method makes that comparison safe.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetLoopedNormalizedTime(this Animator animator, int layer = 0)
        {
            float t = animator.GetCurrentAnimatorStateInfo(layer).normalizedTime;
            return t - Mathf.Floor(t);
        }

        /// <summary>
        /// True if the current state on <paramref name="layer"/> has played past
        /// <paramref name="threshold"/> normalized time and no transition is in progress. <br/>
        /// Standard way to detect "this attack/hit-reaction animation is effectively over"
        /// without relying on Animation Events, which require per-clip authoring and don't
        /// survive clip swaps well.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasFinishedClip(this Animator animator, int layer = 0, float threshold = DefaultEndOfClipThreshold)
            => !animator.IsInTransition(layer)
            && animator.GetCurrentAnimatorStateInfo(layer).normalizedTime >= threshold;

        #endregion

        #region Layer Weight Control

        /// <summary>
        /// Sets a layer's blend weight, clamped to <c>0</c>–<c>1</c> and guarded against an
        /// out-of-range <paramref name="layerIndex"/>. <br/>
        /// Raw <see cref="Animator.SetLayerWeight"/> throws when given an invalid index and
        /// silently accepts out-of-range weights, both of which are easy to trigger when
        /// layer counts vary across Animator Controller variants (e.g. an aim layer that
        /// only exists on ranged characters).
        /// </summary>
        public static void SetLayerWeightSafe(this Animator animator, int layerIndex, float weight)
        {
            if (layerIndex < 0 || layerIndex >= animator.layerCount) return;
            animator.SetLayerWeight(layerIndex, Mathf.Clamp01(weight));
        }

        /// <summary>
        /// Current weight of <paramref name="layerIndex"/>, or <c>0</c> if the index is out
        /// of range. <br/>
        /// Safe counterpart to <see cref="Animator.GetLayerWeight"/> for polling additive
        /// or override layers (aim, flinch, lean) whose presence varies by character rig.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetLayerWeightSafe(this Animator animator, int layerIndex)
            => (layerIndex >= 0 && layerIndex < animator.layerCount) ? animator.GetLayerWeight(layerIndex) : 0f;

        #endregion

        #region Playback Speed

        /// <summary>
        /// Sets <see cref="Animator.speed"/>, clamped to a non-negative value. <br/>
        /// A negative speed silently plays every clip on the controller in reverse, which
        /// is almost never intentional when driven from gameplay systems like hit-stop or
        /// slow-motion effects; this guard keeps those systems from accidentally reversing
        /// animation playback.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetSpeedSafe(this Animator animator, float speed)
            => animator.speed = Mathf.Max(0f, speed);

        /// <summary>
        /// Sets <see cref="Animator.speed"/> clamped between <paramref name="minSpeed"/>
        /// and <paramref name="maxSpeed"/>. <br/>
        /// Useful for hit-stop or bullet-time effects that need to slow animation playback
        /// without ever fully freezing or reversing it.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetSpeedClamped(this Animator animator, float speed, float minSpeed, float maxSpeed)
            => animator.speed = Mathf.Clamp(speed, minSpeed, maxSpeed);

        /// <summary>
        /// Restores <see cref="Animator.speed"/> to its default value of <c>1</c>. <br/>
        /// Pairs with <see cref="SetSpeedSafe"/> to guarantee a temporary slow-motion or
        /// hit-stop effect cannot be left permanently applied if the code that triggered
        /// it exits early.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ResetSpeed(this Animator animator) => animator.speed = 1f;

        #endregion
    }
}