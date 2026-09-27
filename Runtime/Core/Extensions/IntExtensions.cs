using System.Runtime.CompilerServices;

namespace Nexus.Core.Extensions
{
    /// <summary>
    /// Production-ready extension methods for <see cref="int"/> covering range checks, safe
    /// modulo/wrapping, bitmask flag operations and grid snapping.
    /// <para>Design rules:</para>
    /// <list type="bullet">
    ///     <item>Modulo operations use a mathematically correct wrap for negative operands; the raw C# <c>%</c> operator returns negative results that break array indexing and circular buffers.</item>
    ///     <item>Bitmask helpers operate on raw <see cref="int"/> values rather than <see cref="System.Enum"/> to avoid the boxing allocation of <see cref="System.Enum.HasFlag(System.Enum)"/>.</item>
    ///     <item>Zero allocation on every member except explicit string-formatting conversions, which are clearly scoped to UI/debug use.</item>
    ///     <item>Division-by-zero and degenerate-range guards return a documented fallback instead of throwing <see cref="System.DivideByZeroException"/>.</item>
    /// </list>
    /// </summary>
    public static class IntExtensions
    {
        #region Comparison & Range

        /// <summary>
        /// Returns <c>true</c> if <paramref name="value"/> is inclusively between
        /// <paramref name="min"/> and <paramref name="max"/>.
        /// <br/>
        /// Common for inventory slot indices, level/wave bounds and array index validation before
        /// a direct indexer access that would otherwise throw <see cref="System.IndexOutOfRangeException"/>.
        /// </summary>
        /// <param name="value">Value to test.</param>
        /// <param name="min">Inclusive lower bound.</param>
        /// <param name="max">Inclusive upper bound.</param>
        /// <returns><c>true</c> if <paramref name="min"/> &lt;= <paramref name="value"/> &lt;= <paramref name="max"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsInRange(this int value, int min, int max)
            => value >= min && value <= max;

        /// <summary>
        /// Returns <c>true</c> if <paramref name="value"/> is even.
        /// <br/>
        /// Uses a bitwise <c>&amp; 1</c> check rather than <c>% 2</c>, avoiding a division
        /// instruction. Useful for alternating row/column logic in procedural grid generation
        /// (checkerboard tiling, brick-pattern offsets).
        /// </summary>
        /// <param name="value">Value to test.</param>
        /// <returns><c>true</c> if <paramref name="value"/> is even.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsEven(this int value)
            => (value & 1) == 0;

        /// <summary>
        /// Returns <c>true</c> if <paramref name="value"/> is odd.
        /// <br/>
        /// Positive-phrased counterpart to <see cref="IsEven(int)"/> for readability at call sites
        /// that branch on odd-indexed elements (e.g. alternating UI row colors).
        /// </summary>
        /// <param name="value">Value to test.</param>
        /// <returns><c>true</c> if <paramref name="value"/> is odd.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsOdd(this int value)
            => (value & 1) != 0;

        /// <summary>
        /// Returns <c>1</c> if <paramref name="value"/> is positive, <c>-1</c> if negative, and
        /// <c>1</c> if exactly zero.
        /// <br/>
        /// Unlike <see cref="System.Math.Sign(int)"/>, this never returns <c>0</c>, which matters
        /// for facing-direction flips where a zero-length input must not collapse a character's
        /// orientation to an invalid non-direction.
        /// </summary>
        /// <param name="value">Value to evaluate.</param>
        /// <returns><c>-1</c> if negative; otherwise <c>1</c>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SignNonZero(this int value)
            => value < 0 ? -1 : 1;

        #endregion

        #region Clamping

        /// <summary>
        /// Returns <paramref name="value"/> clamped to a minimum of <paramref name="min"/>, with no
        /// upper bound.
        /// <br/>
        /// Reads more intent-clearly than <c>Mathf.Max</c> for enforcing a floor on stack counts,
        /// ammo reserves or currency values that must never go negative.
        /// </summary>
        /// <param name="value">Value to clamp.</param>
        /// <param name="min">Inclusive lower bound.</param>
        /// <returns><paramref name="value"/> if &gt;= <paramref name="min"/>; otherwise <paramref name="min"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ClampMin(this int value, int min)
            => value < min ? min : value;

        /// <summary>
        /// Returns <paramref name="value"/> clamped to a maximum of <paramref name="max"/>, with no
        /// lower bound.
        /// <br/>
        /// Reads more intent-clearly than <c>Mathf.Min</c> for capping stack sizes, party member
        /// counts or UI pool sizes against a configured limit.
        /// </summary>
        /// <param name="value">Value to clamp.</param>
        /// <param name="max">Inclusive upper bound.</param>
        /// <returns><paramref name="value"/> if &lt;= <paramref name="max"/>; otherwise <paramref name="max"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ClampMax(this int value, int max)
            => value > max ? max : value;

        #endregion

        #region Wrapping & Modulo

        /// <summary>
        /// Returns the mathematically correct non-negative modulo of <paramref name="value"/> by
        /// <paramref name="mod"/>.
        /// <br/>
        /// C#'s built-in <c>%</c> operator returns a negative result for negative operands
        /// (e.g. <c>-1 % 4 == -1</c>), which breaks direct use as an array or grid index. This
        /// guarantees a result in [0, <paramref name="mod"/>) for any input, safe for circular
        /// buffers, hotbar slot cycling and wrapped grid coordinates.
        /// </summary>
        /// <param name="value">Dividend, may be negative.</param>
        /// <param name="mod">Divisor. Must be positive; behavior is undefined for <paramref name="mod"/> &lt;= 0.</param>
        /// <returns>A value in [0, <paramref name="mod"/>).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Mod(this int value, int mod)
            => (value % mod + mod) % mod;

        /// <summary>
        /// Wraps <paramref name="value"/> into the inclusive-exclusive range
        /// [<paramref name="min"/>, <paramref name="max"/>), cycling past either bound.
        /// <br/>
        /// Ideal for cycling through hotbar slots, camera preset indices or day-of-week counters
        /// where incrementing past the last valid index should loop back to the first.
        /// </summary>
        /// <param name="value">Value to wrap.</param>
        /// <param name="min">Inclusive lower bound of the range.</param>
        /// <param name="max">Exclusive upper bound of the range.</param>
        /// <returns>The wrapped value within [<paramref name="min"/>, <paramref name="max"/>).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Wrap(this int value, int min, int max)
            => min + (value - min).Mod(max - min);

        #endregion

        #region Bit Flags

        /// <summary>
        /// Returns <c>true</c> if every bit set in <paramref name="flag"/> is also set in
        /// <paramref name="value"/>.
        /// <br/>
        /// Boxing-free alternative to <see cref="System.Enum.HasFlag(System.Enum)"/> for
        /// per-frame checks against gameplay bitmasks (status effects, input action states,
        /// interaction capability flags) where the enum-based API's allocation would add up.
        /// </summary>
        /// <param name="value">Bitmask to test.</param>
        /// <param name="flag">Flag bits to check for.</param>
        /// <returns><c>true</c> if all bits of <paramref name="flag"/> are present in <paramref name="value"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasFlagFast(this int value, int flag)
            => (value & flag) == flag;

        /// <summary>
        /// Returns a copy of <paramref name="value"/> with the bits of <paramref name="flag"/> set.
        /// <br/>
        /// Standard bitmask mutation for applying a status effect or unlocking a capability flag
        /// without disturbing any other bit in the mask.
        /// </summary>
        /// <param name="value">Original bitmask.</param>
        /// <param name="flag">Flag bits to set.</param>
        /// <returns>A new bitmask with <paramref name="flag"/> applied via bitwise OR.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SetFlag(this int value, int flag)
            => value | flag;

        /// <summary>
        /// Returns a copy of <paramref name="value"/> with the bits of <paramref name="flag"/> cleared.
        /// <br/>
        /// Standard bitmask mutation for removing a status effect or revoking a capability flag
        /// without disturbing any other bit in the mask.
        /// </summary>
        /// <param name="value">Original bitmask.</param>
        /// <param name="flag">Flag bits to clear.</param>
        /// <returns>A new bitmask with <paramref name="flag"/> removed via bitwise AND-NOT.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ClearFlag(this int value, int flag)
            => value & ~flag;

        /// <summary>
        /// Returns a copy of <paramref name="value"/> with the bits of <paramref name="flag"/> toggled.
        /// <br/>
        /// Ideal for one-button toggle actions (e.g. flipping a "blocking" or "aiming" input state
        /// bit) without branching on whether the flag is currently set.
        /// </summary>
        /// <param name="value">Original bitmask.</param>
        /// <param name="flag">Flag bits to toggle.</param>
        /// <returns>A new bitmask with <paramref name="flag"/> flipped via bitwise XOR.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ToggleFlag(this int value, int flag)
            => value ^ flag;

        #endregion

        #region Snapping

        /// <summary>
        /// Rounds <paramref name="value"/> to the nearest multiple of <paramref name="gridSize"/>.
        /// <br/>
        /// Guards against a non-positive grid size by returning <paramref name="value"/> unchanged.
        /// Useful for snapping discrete stat values (level, upgrade tier) to fixed increments
        /// enforced by game design.
        /// </summary>
        /// <param name="value">Value to snap.</param>
        /// <param name="gridSize">Grid spacing. Values &lt;= 0 disable snapping.</param>
        /// <returns>The snapped value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SnapTo(this int value, int gridSize)
        {
            if (gridSize <= 0) return value;
            int half = gridSize / 2;
            return (value + half).Mod(int.MaxValue) / gridSize * gridSize;
        }

        #endregion

        #region Conversions

        /// <summary>
        /// Returns what percentage <paramref name="value"/> represents of <paramref name="total"/>,
        /// in [0, 100] semantics.
        /// <br/>
        /// Guards against a zero <paramref name="total"/> by returning <c>0f</c> instead of
        /// producing <c>NaN</c> — important for percentage-complete displays (quest progress,
        /// loading bars) before the total is known.
        /// </summary>
        /// <param name="value">Partial amount.</param>
        /// <param name="total">Whole amount that <paramref name="value"/> is measured against.</param>
        /// <returns>Percentage in [0,100] for in-range input, or <c>0f</c> if <paramref name="total"/> is zero.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float PercentOf(this int value, int total)
            => total == 0 ? 0f : value / (float)total * 100f;

        /// <summary>
        /// Returns <c>true</c> if <paramref name="value"/> is non-zero.
        /// <br/>
        /// Convenience for interpreting legacy or serialized int-as-bool fields (common in
        /// save-data formats and older C-style APIs) without a raw <c>!= 0</c> comparison.
        /// </summary>
        /// <param name="value">Value to convert.</param>
        /// <returns><c>true</c> if <paramref name="value"/> is not zero.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ToBool(this int value)
            => value != 0;

        /// <summary>
        /// Converts <paramref name="value"/> to its English ordinal string representation
        /// (e.g. <c>1</c> → <c>"1st"</c>, <c>22</c> → <c>"22nd"</c>).
        /// <br/>
        /// Intended for leaderboard placement, wave counters and rank display text. Allocates a
        /// string by necessity and is scoped to UI/text formatting, not hot-path gameplay logic.
        /// </summary>
        /// <param name="value">Non-negative rank or count to format.</param>
        /// <returns>The ordinal string representation of <paramref name="value"/>.</returns>
        public static string ToOrdinalString(this int value)
        {
            int lastTwoDigits = value % 100;
            if (lastTwoDigits >= 11 && lastTwoDigits <= 13)
                return value + "th";

            int lastDigit = value % 10;
            string suffix = lastDigit switch
            {
                1 => "st",
                2 => "nd",
                3 => "rd",
                _ => "th"
            };

            return value + suffix;
        }

        #endregion
    }
}