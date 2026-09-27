using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.AI;

namespace Nexus.Core.Extensions
{
    /// <summary>
    /// Extension methods for <see cref="NavMeshAgent"/> covering safe destination setting,
    /// arrival detection, path status queries, off-mesh link handling, and speed/stopping control.
    /// <para>Design rules:</para>
    /// <list type="bullet">
    /// <item><see cref="NavMeshAgent.SetDestination(Vector3)"/> silently returns <c>false</c>
    /// and logs a console error when the agent is not currently placed on a NavMesh — a very
    /// common transient state right after spawning, after a teleport, or after being disabled
    /// and re-enabled off-mesh. Every destination-setting method here checks
    /// <see cref="NavMeshAgent.isOnNavMesh"/> first and returns an inspectable <c>bool</c>
    /// instead of relying on the console warning to surface the failure.</item>
    /// <item><see cref="NavMeshAgent.remainingDistance"/> is only meaningful once
    /// <see cref="NavMeshAgent.pathPending"/> is <c>false</c> — while a path is still being
    /// computed asynchronously it reports <see cref="float.PositiveInfinity"/>, which silently
    /// breaks any comparison against it. Every method that reads it checks
    /// <see cref="NavMeshAgent.pathPending"/> first rather than trusting the raw value.</item>
    /// <item><see cref="NavMeshAgent.Warp(Vector3)"/> fails silently (returns <c>false</c>)
    /// if the target point is not on or near a NavMesh. <see cref="WarpSafe"/> and
    /// <see cref="TrySetDestinationOnNavMesh"/> both route the target through
    /// <see cref="NavMesh.SamplePosition(Vector3, out NavMeshHit, float, int)"/> first, so a
    /// point that is merely close to the mesh (e.g. a hit point slightly above the walkable
    /// surface) still resolves correctly instead of failing outright.</item>
    /// <item>Arrival detection combines <see cref="NavMeshAgent.pathPending"/>,
    /// <see cref="NavMeshAgent.remainingDistance"/>, and velocity rather than checking
    /// remaining distance alone — an agent can be within its stopping distance while still
    /// decelerating, and checking velocity too avoids triggering "arrived" logic (playing an
    /// idle animation, opening a door) a few frames early.</item>
    /// </list>
    /// </summary>
    public static class NavMeshAgentExtensions
    {
        /// <summary>
        /// Squared speed threshold below which an agent is considered to have fully stopped
        /// moving, used alongside remaining-distance checks in <see cref="HasArrived"/> to
        /// avoid reporting arrival while the agent is still decelerating toward its destination.
        /// </summary>
        private const float StoppedSqrSpeedEpsilon = 0.0025f; // 0.05 m/s

        /// <summary>
        /// Default maximum distance, in world units, used when sampling the NavMesh for the
        /// nearest valid point to an arbitrary world position in <see cref="TrySetDestinationOnNavMesh"/>
        /// and <see cref="WarpSafe"/>.
        /// </summary>
        private const float DefaultSampleMaxDistance = 2f;

        /// <summary>
        /// Minimum valid value for <see cref="NavMeshAgent.avoidancePriority"/>, as documented
        /// by Unity.
        /// </summary>
        private const int MinAvoidancePriority = 0;

        /// <summary>
        /// Maximum valid value for <see cref="NavMeshAgent.avoidancePriority"/>, as documented
        /// by Unity.
        /// </summary>
        private const int MaxAvoidancePriority = 99;

        #region Destination Setting

        /// <summary>
        /// Sets the agent's destination only if it is currently placed on a NavMesh, returning
        /// <c>false</c> instead of logging a console error otherwise. <br/>
        /// Wraps the single most common <see cref="NavMeshAgent"/> failure: calling
        /// <see cref="NavMeshAgent.SetDestination(Vector3)"/> during the one-frame window after
        /// spawning, teleporting, or re-enabling an agent before it has been placed back on the
        /// mesh, which otherwise fails invisibly unless the console is being watched.
        /// </summary>
        public static bool SetDestinationSafe(this NavMeshAgent agent, Vector3 destination)
        {
            if (agent == null || !agent.isOnNavMesh) return false;
            return agent.SetDestination(destination);
        }

        /// <summary>
        /// Finds the nearest point on the NavMesh within <paramref name="maxDistance"/> of
        /// <paramref name="targetPosition"/> and sets it as the agent's destination, returning
        /// <c>false</c> if no such point exists or the agent is not currently on a NavMesh. <br/>
        /// The correct way to path an agent toward a point that is merely near the walkable
        /// surface rather than exactly on it — a raycast hit slightly above the floor mesh, a
        /// player's feet position with minor floating-point drift — which a raw
        /// <see cref="SetDestinationSafe"/> call would otherwise reject or misplace.
        /// </summary>
        public static bool TrySetDestinationOnNavMesh(this NavMeshAgent agent, Vector3 targetPosition, float maxDistance = DefaultSampleMaxDistance)
        {
            if (agent == null || !agent.isOnNavMesh) return false;
            if (!NavMesh.SamplePosition(targetPosition, out NavMeshHit hit, maxDistance, NavMesh.AllAreas)) return false;

            return agent.SetDestination(hit.position);
        }

        /// <summary>
        /// Stops the agent and clears its current path in one call. <br/>
        /// Setting <see cref="NavMeshAgent.isStopped"/> to <c>true</c> alone leaves the last
        /// computed path assigned; if <see cref="NavMeshAgent.isStopped"/> is later cleared
        /// without an explicit new destination, the agent silently resumes moving toward its
        /// stale target. This method removes that path in the same call, matching the intent
        /// of "cancel this agent's current order" used when interrupting an AI action.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StopAndClearPath(this NavMeshAgent agent)
        {
            if (agent == null || !agent.isOnNavMesh) return;
            agent.isStopped = true;
            agent.ResetPath();
        }

        #endregion

        #region Warp

        /// <summary>
        /// Teleports the agent to the nearest point on the NavMesh within
        /// <paramref name="maxDistance"/> of <paramref name="position"/>, returning
        /// <c>false</c> if no such point exists. <br/>
        /// Raw <see cref="NavMeshAgent.Warp(Vector3)"/> fails silently when
        /// <paramref name="position"/> is not close enough to a baked mesh — a frequent
        /// problem when teleporting an agent to a raycast hit point sitting slightly above
        /// the floor, or to a position authored in the editor before the NavMesh was last
        /// baked. Sampling first makes that failure both preventable and inspectable.
        /// </summary>
        public static bool WarpSafe(this NavMeshAgent agent, Vector3 position, float maxDistance = DefaultSampleMaxDistance)
        {
            if (agent == null) return false;
            if (!NavMesh.SamplePosition(position, out NavMeshHit hit, maxDistance, NavMesh.AllAreas)) return false;

            return agent.Warp(hit.position);
        }

        #endregion

        #region Arrival & Path Status

        /// <summary>
        /// True if the agent has a fully computed, complete path and has settled to a near-zero
        /// velocity within its stopping distance of the destination. <br/>
        /// This is the correct arrival check — simpler alternatives that only compare
        /// <see cref="NavMeshAgent.remainingDistance"/> to <see cref="NavMeshAgent.stoppingDistance"/>
        /// fire a frame or more early, while the agent is still visibly decelerating, which
        /// looks wrong when used to trigger an arrival animation or a "start attacking" state
        /// transition.
        /// </summary>
        public static bool HasArrived(this NavMeshAgent agent)
        {
            if (agent == null || !agent.isOnNavMesh) return false;
            if (agent.pathPending) return false;
            if (agent.pathStatus != NavMeshPathStatus.PathComplete) return false;
            if (agent.remainingDistance > agent.stoppingDistance) return false;

            return agent.velocity.sqrMagnitude <= StoppedSqrSpeedEpsilon;
        }

        /// <summary>
        /// Remaining distance to the current destination, or <paramref name="fallback"/> if a
        /// path is still being computed or the agent has no valid path. <br/>
        /// Reading <see cref="NavMeshAgent.remainingDistance"/> directly while
        /// <see cref="NavMeshAgent.pathPending"/> is <c>true</c> silently returns
        /// <see cref="float.PositiveInfinity"/>, which corrupts any arithmetic or UI display
        /// (a "distance to objective" HUD element) built on top of it without an explicit check.
        /// </summary>
        public static float GetRemainingDistanceOr(this NavMeshAgent agent, float fallback)
        {
            if (agent == null || agent.pathPending || !agent.hasPath) return fallback;
            return agent.remainingDistance;
        }

        /// <summary>
        /// True if the agent's current path could only be partially computed — it gets the
        /// agent closer to its destination but does not actually reach it. <br/>
        /// Detects the common "target is behind a wall with no valid route" case, letting AI
        /// logic fall back to a different behavior (search, give up, path to a different point)
        /// instead of endlessly walking toward a destination it can structurally never reach.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasPartialPath(this NavMeshAgent agent)
            => agent != null && agent.pathStatus == NavMeshPathStatus.PathPartial;

        /// <summary>
        /// True if the agent's current path is entirely invalid — no route to the destination
        /// exists at all. <br/>
        /// Distinguishes a fully unreachable destination from <see cref="HasPartialPath"/>'s
        /// "reachable, but not all the way there" case, which typically call for different
        /// fallback AI behaviors.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasInvalidPath(this NavMeshAgent agent)
            => agent != null && agent.pathStatus == NavMeshPathStatus.PathInvalid;

        /// <summary>
        /// Squared distance from the agent's current position to its destination, ignoring
        /// path length — a straight-line measurement, not the remaining distance along the
        /// computed route. <br/>
        /// Useful for cheap "is the target even worth pathing to" checks (e.g. skipping
        /// far-away patrol points) before committing to a full
        /// <see cref="NavMeshAgent.CalculatePath(Vector3, NavMeshPath)"/> call.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SqrDistanceToDestination(this NavMeshAgent agent)
        {
            Vector3 d = agent.destination - agent.transform.position;
            return d.sqrMagnitude;
        }

        #endregion

        #region Off-Mesh Links

        /// <summary>
        /// True if the agent is currently traversing an off-mesh link (a jump, a drop, a
        /// scripted connection between two disjoint NavMesh islands). <br/>
        /// Use to switch animation state (jump/climb animations) or temporarily suspend normal
        /// steering logic while Unity's own link-traversal movement takes over.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsOnOffMeshLink(this NavMeshAgent agent)
            => agent != null && agent.isOnOffMeshLink;

        /// <summary>
        /// Manually completes the agent's current off-mesh link traversal, only if it is
        /// actually on one. <br/>
        /// Required when <see cref="NavMeshAgent.autoTraverseOffMeshLink"/> is disabled to
        /// allow custom link-crossing logic (a scripted jump arc, a ladder-climb animation) —
        /// without an explicit call to <see cref="NavMeshAgent.CompleteOffMeshLink"/> once that
        /// custom movement finishes, the agent remains stuck in the link-traversal state
        /// indefinitely.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CompleteOffMeshLinkSafe(this NavMeshAgent agent)
        {
            if (agent != null && agent.isOnOffMeshLink) agent.CompleteOffMeshLink();
        }

        #endregion

        #region Speed & Avoidance

        /// <summary>
        /// Sets <see cref="NavMeshAgent.speed"/>, clamped to a non-negative value. <br/>
        /// A negative speed is accepted by the underlying property but produces undefined,
        /// visually broken movement; this guard keeps a runtime-computed speed (a slow status
        /// effect, a terrain speed modifier) from ever going negative due to a stacking bug.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetSpeedSafe(this NavMeshAgent agent, float speed)
        {
            if (agent != null) agent.speed = Mathf.Max(0f, speed);
        }

        /// <summary>
        /// Sets <see cref="NavMeshAgent.avoidancePriority"/>, clamped to Unity's supported
        /// <c>0</c>–<c>99</c> range (lower values yield right-of-way). <br/>
        /// An out-of-range priority is silently clamped internally by Unity with no error;
        /// this guard makes that boundary explicit for runtime-assigned priorities, such as
        /// temporarily raising a fleeing enemy's priority so pursuing allies path around it.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetAvoidancePrioritySafe(this NavMeshAgent agent, int priority)
        {
            if (agent != null) agent.avoidancePriority = Mathf.Clamp(priority, MinAvoidancePriority, MaxAvoidancePriority);
        }

        #endregion

        #region Pausing & Enable State

        /// <summary>
        /// Pauses or resumes the agent's movement without discarding its current path. <br/>
        /// The correct way to freeze an agent temporarily — a cutscene, a stagger effect, a
        /// dialogue interaction — since setting <see cref="NavMeshAgent.isStopped"/> alone
        /// (unlike <see cref="StopAndClearPath"/>) preserves the destination, letting movement
        /// resume exactly where it left off once the pause ends.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetPaused(this NavMeshAgent agent, bool paused)
        {
            if (agent != null && agent.isOnNavMesh) agent.isStopped = paused;
        }

        /// <summary>
        /// True if the agent exists, is enabled, and is currently placed on a NavMesh. <br/>
        /// The standard precondition check before calling any other method in this class from
        /// code that cannot guarantee the agent's current state — for example an AI behavior
        /// tree node that runs every tick against a reference cached earlier.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsUsable(this NavMeshAgent agent)
            => agent != null && agent.enabled && agent.isOnNavMesh;

        #endregion
    }
}