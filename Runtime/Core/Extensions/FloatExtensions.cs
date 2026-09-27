using System.Runtime.CompilerServices;
using UnityEngine;

namespace Nexus.Core.Extensions
{
    /// <summary>
    /// Production-ready extension methods for <see cref="float"/> covering the recurring math
    /// needs of gameplay code: remapping, clamping, snapping, angle wrapping and lightweight easing.
    /// <para>Design rules:</para>
    /// <list type="bullet">
    ///     <item>All comparisons use an epsilon tolerance; direct <c>==</c> comparisons on floats are never safe after arithmetic.</item>
    ///     <item>Division-by-zero guards return a documented fallback instead of propagating <c>NaN</c> or <c>Infinity</c> into transforms, shaders or animation curves.</item>
    ///     <item>Angle helpers work in degrees at the public API per Unity convention, converting to radians only where required by <see cref="Mathf"/>.</item>
    ///     <item>Easing helpers are pure, allocation-free alternatives to <see cref="AnimationCurve"/> for simple in/out blends (UI transitions, camera lerps, damage falloff).</item>
    ///     <item>Zero allocation across the entire class. No boxing, no LINQ, no hot-path Sqrt beyond what <see cref="Mathf"/> itself requires.</item>
    /// </list>
    /// </summary>
    public static class FloatExtensions
    {
        #region Constants

        /// <summary>
        /// Default tolerance used by approximate-equality and near-zero checks to absorb
        /// floating-point noise accumulated from repeated arithmetic (physics integration,
        /// animation blending, accumulated delta time).
        /// </summary>
        private const float DefaultEpsilon = 1e-5f;

        #endregion

        #region Comparison & Validation

        /// <summary>
        /// Returns <c>true</c> if <paramref name="a"/> and <paramref name="b"/> differ by no more
        /// than <paramref name="epsilon"/>.
        /// <br/>
        /// A direct <c>a == b</c> comparison is almost never correct after any arithmetic
        /// (accumulated delta time, interpolation, trigonometry); this is the safe replacement
        /// for state checks like "has this timer finished counting down".
        /// </summary>
        /// <param name="a">First value.</param>
        /// <param name="b">Second value.</param>
        /// <param name="epsilon">Maximum allowed absolute difference. Defaults to <c>DefaultEpsilon</c>.</param>
        /// <returns><c>true</c> if the values are approximately equal.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsApproximately(this float a, float b, float epsilon = DefaultEpsilon)
            => Mathf.Abs(a - b) <= epsilon;

        /// <summary>
        /// Returns <c>true</c> if <paramref name="value"/> is within <paramref name="epsilon"/> of zero.
        /// <br/>
        /// Ideal for stopping velocity integration or input dead-zone checks where an exact
        /// <c>value == 0f</c> comparison would fail due to residual floating-point drift.
        /// </summary>
        /// <param name="value">Value to test.</param>
        /// <param name="epsilon">Tolerance around zero. Defaults to <c>DefaultEpsilon</c>.</param>
        /// <returns><c>true</c> if <paramref name="value"/> is effectively zero.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsZero(this float value, float epsilon = DefaultEpsilon)
            => Mathf.Abs(value) <= epsilon;

        /// <summary>
        /// Returns <c>true</c> if <paramref name="value"/> is inclusively between
        /// <paramref name="min"/> and <paramref name="max"/>.
        /// <br/>
        /// Common for health thresholds, normalized blend weights and stat range validation
        /// without manually chaining two comparisons at every call site.
        /// </summary>
        /// <param name="value">Value to test.</param>
        /// <param name="min">Inclusive lower bound.</param>
        /// <param name="max">Inclusive upper bound.</param>
        /// <returns><c>true</c> if <paramref name="min"/> &lt;= <paramref name="value"/> &lt;= <paramref name="max"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsInRange(this float value, float min, float max)
            => value >= min && value <= max;

        /// <summary>
        /// Returns <c>true</c> if <paramref name="value"/> is neither <c>NaN</c> nor <c>Infinity</c>.
        /// <br/>
        /// A single invalid float written into <see cref="Transform.position"/>, a shader property
        /// or a physics force call can corrupt the entire object's state for the rest of its
        /// lifetime. Validate any value computed from division or normalization before applying it.
        /// </summary>
        /// <param name="value">Value to validate.</param>
        /// <returns><c>true</c> if <paramref name="value"/> is finite.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsValid(this float value)
            => !float.IsNaN(value) && !float.IsInfinity(value);

        /// <summary>
        /// Returns <paramref name="value"/> if <see cref="IsValid(float)"/> is <c>true</c>,
        /// otherwise returns <paramref name="fallback"/>.
        /// <br/>
        /// Ideal for sanitizing values computed from external data (save files, network payloads,
        /// procedural generation seeds) before they reach gameplay or rendering systems.
        /// </summary>
        /// <param name="value">Candidate value.</param>
        /// <param name="fallback">Safe value returned when <paramref name="value"/> is invalid.</param>
        /// <returns>Validated value or <paramref name="fallback"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ValidOr(this float value, float fallback)
            => value.IsValid() ? value : fallback;

        #endregion

        #region Clamping

        /// <summary>
        /// Returns <paramref name="value"/> clamped to a minimum of <paramref name="min"/>, with no
        /// upper bound.
        /// <br/>
        /// Reads more intent-clearly than <c>Mathf.Max</c> at call sites enforcing a floor, such as
        /// preventing a cooldown timer or knockback force from going negative.
        /// </summary>
        /// <param name="value">Value to clamp.</param>
        /// <param name="min">Inclusive lower bound.</param>
        /// <returns><paramref name="value"/> if &gt;= <paramref name="min"/>; otherwise <paramref name="min"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ClampMin(this float value, float min)
            => value < min ? min : value;

        /// <summary>
        /// Returns <paramref name="value"/> clamped to a maximum of <paramref name="max"/>, with no
        /// lower bound.
        /// <br/>
        /// Reads more intent-clearly than <c>Mathf.Min</c> at call sites enforcing a ceiling, such
        /// as capping a projectile's speed after an additive buff stack.
        /// </summary>
        /// <param name="value">Value to clamp.</param>
        /// <param name="max">Inclusive upper bound.</param>
        /// <returns><paramref name="value"/> if &lt;= <paramref name="max"/>; otherwise <paramref name="max"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ClampMax(this float value, float max)
            => value > max ? max : value;

        #endregion

        #region Remapping & Normalization

        /// <summary>
        /// Remaps <paramref name="value"/> from the range [<paramref name="inMin"/>, <paramref name="inMax"/>]
        /// into the range [<paramref name="outMin"/>, <paramref name="outMax"/>], unclamped.
        /// <br/>
        /// Guards against a zero-width input range by returning <paramref name="outMin"/> instead of
        /// dividing by zero. Core utility for converting sensor/stat values into UI fill amounts,
        /// audio parameters or shader properties (e.g. health 0-100 to a health-bar fill 0-1).
        /// </summary>
        /// <param name="value">Value to remap.</param>
        /// <param name="inMin">Input range lower bound.</param>
        /// <param name="inMax">Input range upper bound.</param>
        /// <param name="outMin">Output range lower bound.</param>
        /// <param name="outMax">Output range upper bound.</param>
        /// <returns>The remapped value, or <paramref name="outMin"/> if the input range has zero width.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Remap(this float value, float inMin, float inMax, float outMin, float outMax)
        {
            float inRange = inMax - inMin;
            if (inRange.IsZero())
                return outMin;

            return outMin + (value - inMin) / inRange * (outMax - outMin);
        }

        /// <summary>
        /// Remaps <paramref name="value"/> from the range [<paramref name="inMin"/>, <paramref name="inMax"/>]
        /// into the range [<paramref name="outMin"/>, <paramref name="outMax"/>], clamping the result
        /// to stay within the output range.
        /// <br/>
        /// Prevents overshoot artifacts in UI fills and shader parameters when the input value
        /// exceeds its expected bounds (e.g. overhealed HP briefly exceeding the display max).
        /// </summary>
        /// <param name="value">Value to remap.</param>
        /// <param name="inMin">Input range lower bound.</param>
        /// <param name="inMax">Input range upper bound.</param>
        /// <param name="outMin">Output range lower bound.</param>
        /// <param name="outMax">Output range upper bound.</param>
        /// <returns>The remapped value, clamped to [<paramref name="outMin"/>, <paramref name="outMax"/>].</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float RemapClamped(this float value, float inMin, float inMax, float outMin, float outMax)
            => Mathf.Clamp(value.Remap(inMin, inMax, outMin, outMax), outMin, outMax);

        /// <summary>
        /// Remaps <paramref name="value"/> from [<paramref name="inMin"/>, <paramref name="inMax"/>]
        /// into the normalized [0, 1] range, unclamped.
        /// <br/>
        /// Common shorthand when feeding a raw stat directly into an <see cref="AnimationCurve"/>,
        /// blend tree parameter or shader lerp factor.
        /// </summary>
        /// <param name="value">Value to normalize.</param>
        /// <param name="inMin">Input range lower bound.</param>
        /// <param name="inMax">Input range upper bound.</param>
        /// <returns>Normalized value in roughly [0,1] for in-range input.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Remap01(this float value, float inMin, float inMax)
            => value.Remap(inMin, inMax, 0f, 1f);

        /// <summary>
        /// Returns what percentage <paramref name="value"/> represents of <paramref name="total"/>,
        /// in the range [0, 100] semantics (not pre-divided by 100).
        /// <br/>
        /// Guards against a zero <paramref name="total"/> by returning <c>0f</c> instead of
        /// producing <c>NaN</c>, which is critical for freshly-initialized stat bars (e.g. max
        /// stamina not yet assigned) that would otherwise silently corrupt UI layout calculations.
        /// </summary>
        /// <param name="value">Partial amount.</param>
        /// <param name="total">Whole amount that <paramref name="value"/> is measured against.</param>
        /// <returns>Percentage in [0,100] for in-range input, or <c>0f</c> if <paramref name="total"/> is zero.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float PercentOf(this float value, float total)
            => total.IsZero() ? 0f : value / total * 100f;

        #endregion

        #region Snapping

        /// <summary>
        /// Rounds <paramref name="value"/> to the nearest multiple of <paramref name="gridSize"/>.
        /// <br/>
        /// Guards against a zero grid size by returning <paramref name="value"/> unchanged instead
        /// of dividing by zero. Standard building-placement / level-editor snapping utility for
        /// grid-aligned construction systems.
        /// </summary>
        /// <param name="value">Value to snap.</param>
        /// <param name="gridSize">Grid spacing. Values &lt;= 0 disable snapping.</param>
        /// <returns>The snapped value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SnapTo(this float value, float gridSize)
            => gridSize <= 0f ? value : Mathf.Round(value / gridSize) * gridSize;

        #endregion

        #region Angles

        /// <summary>
        /// Converts <paramref name="degrees"/> to radians.
        /// <br/>
        /// Public gameplay APIs should stay in degrees per Unity convention; this exists for the
        /// boundary calls into trigonometric functions (<see cref="Mathf.Sin(float)"/>,
        /// <see cref="Mathf.Cos(float)"/>) that require radians internally.
        /// </summary>
        /// <param name="degrees">Angle in degrees.</param>
        /// <returns>Equivalent angle in radians.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ToRadians(this float degrees)
            => degrees * Mathf.Deg2Rad;

        /// <summary>
        /// Converts <paramref name="radians"/> to degrees.
        /// <br/>
        /// Use immediately after any radian-based trigonometric result (e.g.
        /// <see cref="Mathf.Atan2(float, float)"/>) before exposing the value to gameplay code or
        /// the inspector, where degrees are the expected unit.
        /// </summary>
        /// <param name="radians">Angle in radians.</param>
        /// <returns>Equivalent angle in degrees.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ToDegrees(this float radians)
            => radians * Mathf.Rad2Deg;

        /// <summary>
        /// Wraps <paramref name="degrees"/> into the range (-180, 180].
        /// <br/>
        /// Essential before comparing or lerping angles (turret aiming, character turning) — an
        /// unwrapped angle like <c>370°</c> or <c>-190°</c> will cause visible snapping or the
        /// wrong rotation direction when fed directly into a lerp or PID controller.
        /// </summary>
        /// <param name="degrees">Angle in degrees, any magnitude.</param>
        /// <returns>Equivalent angle wrapped into (-180, 180].</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float WrapAngle(this float degrees)
        {
            float wrapped = degrees % 360f;
            if (wrapped > 180f) wrapped -= 360f;
            else if (wrapped <= -180f) wrapped += 360f;
            return wrapped;
        }

        /// <summary>
        /// Returns the shortest signed angular difference from <paramref name="from"/> to
        /// <paramref name="to"/>, in degrees, wrapped into (-180, 180].
        /// <br/>
        /// The correct way to compute "how much and which direction to turn" for AI facing,
        /// turret tracking or steering behaviours — a naive <c>to - from</c> subtraction breaks at
        /// the 0°/360° wraparound boundary.
        /// </summary>
        /// <param name="from">Starting angle in degrees.</param>
        /// <param name="to">Target angle in degrees.</param>
        /// <returns>Signed shortest delta in degrees, positive for clockwise rotation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DeltaAngle(this float from, float to)
            => Mathf.DeltaAngle(from, to);

        #endregion

        #region Easing

        /// <summary>
        /// Applies a quadratic ease-in curve to <paramref name="t"/> (accelerating from zero velocity).
        /// <br/>
        /// Zero-allocation alternative to an <see cref="AnimationCurve"/> asset for simple wind-up
        /// motions — melee swing charge-up, camera zoom acceleration, UI element growing in.
        /// Expects <paramref name="t"/> pre-clamped to [0,1]; behavior outside that range is unclamped extrapolation.
        /// </summary>
        /// <param name="t">Normalized progress, expected in [0,1].</param>
        /// <returns>Eased progress value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float EaseIn(this float t)
            => t * t;

        /// <summary>
        /// Applies a quadratic ease-out curve to <paramref name="t"/> (decelerating to zero velocity).
        /// <br/>
        /// Ideal for motions that should settle smoothly: camera arriving at a target, UI panels
        /// sliding to rest, projectile impact recoil settling back to idle.
        /// Expects <paramref name="t"/> pre-clamped to [0,1]; behavior outside that range is unclamped extrapolation.
        /// </summary>
        /// <param name="t">Normalized progress, expected in [0,1].</param>
        /// <returns>Eased progress value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float EaseOut(this float t)
            => t * (2f - t);

        /// <summary>
        /// Applies a smoothstep-style ease-in-out curve to <paramref name="t"/> (slow start, fast
        /// middle, slow end).
        /// <br/>
        /// The standard curve for camera transitions, fade in/out sequences and blend-tree
        /// parameter smoothing where a linear lerp looks mechanical.
        /// Expects <paramref name="t"/> pre-clamped to [0,1]; behavior outside that range is unclamped extrapolation.
        /// </summary>
        /// <param name="t">Normalized progress, expected in [0,1].</param>
        /// <returns>Eased progress value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float EaseInOut(this float t)
            => t * t * (3f - 2f * t);

        #endregion

        #region Conversions

        /// <summary>
        /// Returns <paramref name="value"/> squared.
        /// <br/>
        /// Improves readability at call sites comparing against a precomputed
        /// <c>sqrMagnitude</c> or <c>sqrDistance</c> (e.g. <c>radius.Squared()</c>), keeping the
        /// intent explicit without a raw <c>radius * radius</c> expression repeated inline.
        /// </summary>
        /// <param name="value">Value to square.</param>
        /// <returns><paramref name="value"/> * <paramref name="value"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Squared(this float value)
            => value * value;

        /// <summary>
        /// Converts <paramref name="value"/> to an <see cref="int"/> by flooring.
        /// <br/>
        /// Explicit name over <see cref="Mathf.FloorToInt(float)"/> for fluent call chains, such
        /// as converting a continuous world position into a discrete tile-grid coordinate.
        /// </summary>
        /// <param name="value">Value to floor and convert.</param>
        /// <returns>The floored integer value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ToFloorInt(this float value)
            => Mathf.FloorToInt(value);

        /// <summary>
        /// Converts <paramref name="value"/> to an <see cref="int"/> by ceiling.
        /// <br/>
        /// Useful for computing the minimum number of discrete units needed to cover a continuous
        /// amount — e.g. how many ammo clips of size N are required to hold M bullets.
        /// </summary>
        /// <param name="value">Value to ceiling and convert.</param>
        /// <returns>The ceiled integer value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ToCeilInt(this float value)
            => Mathf.CeilToInt(value);

        /// <summary>
        /// Converts <paramref name="value"/> to an <see cref="int"/> by rounding to nearest.
        /// <br/>
        /// Explicit name over <see cref="Mathf.RoundToInt(float)"/> for fluent call chains, such
        /// as converting a computed damage-over-time float into a whole-number damage tick.
        /// </summary>
        /// <param name="value">Value to round and convert.</param>
        /// <returns>The rounded integer value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ToRoundInt(this float value)
            => Mathf.RoundToInt(value);

        #endregion
    }
}