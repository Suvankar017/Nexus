using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Nexus.Core.Extensions
{
    /// <summary>
    /// Extension methods for arrays (<c>T[]</c>) covering safe indexed access, in-place
    /// swapping and shuffling, weighted random selection, and null-safe wrappers around
    /// common <see cref="Array"/> operations.
    /// <para>Design rules:</para>
    /// <list type="bullet">
    /// <item>Every accessor treats a <c>null</c> array as "empty" rather than throwing.
    /// Arrays sourced from serialized fields, deserialized save data, or a not-yet-populated
    /// object pool are frequently <c>null</c> before first use; a single unguarded index
    /// access on any of those is an immediate <see cref="NullReferenceException"/> that these
    /// wrappers turn into a graceful fallback instead.</item>
    /// <item>Shuffling and random selection use <see cref="UnityEngine.Random"/>, matching
    /// the rest of the gameplay codebase's RNG. A <see cref="System.Random"/> overload is
    /// provided alongside it for the cases that specifically need a seeded, reproducible
    /// sequence — replays, procedural generation, deterministic netcode — where the global
    /// <see cref="UnityEngine.Random"/> state cannot be isolated per-caller.</item>
    /// <item>Shuffling is Fisher-Yates performed strictly in place. No new array is ever
    /// allocated by a shuffle, matching the zero-allocation rule even though the operation
    /// mutates the caller's existing array.</item>
    /// <item>Weighted random selection treats negative weights as zero rather than throwing,
    /// since a weight table populated from designer data (a loot table, a spawn table) is far
    /// more likely to contain an accidental negative typo than to require the caller to
    /// pre-validate every entry.</item>
    /// <item>No LINQ. <see cref="Array.FindIndex{T}(T[], Predicate{T})"/>,
    /// <see cref="Array.IndexOf{T}(T[], T)"/>, <see cref="Array.Reverse(Array)"/>, and
    /// <see cref="Array.Clear(Array, int, int)"/> are ordinary <see cref="System.Array"/>
    /// static methods, not <c>System.Linq</c> extensions, and are used freely where they
    /// avoid reimplementing well-tested BCL logic.</item>
    /// </list>
    /// </summary>
    public static class ArrayExtensions
    {
        /// <summary>
        /// Sentinel returned by search and weighted-selection methods when no matching or
        /// selectable element exists, matching the convention of <see cref="Array.IndexOf(Array, object)"/>.
        /// </summary>
        private const int NotFoundIndex = -1;

        /// <summary>
        /// Minimum total weight below which <see cref="GetRandomWeightedIndex"/> treats a
        /// weight table as empty rather than attempting a selection. Guards against a
        /// divide-by-zero-equivalent infinite loop when every weight is zero or negative.
        /// </summary>
        private const float WeightSumEpsilon = 1e-6f;

        #region Validation & Safe Access

        /// <summary>
        /// True if the array is <c>null</c> or has zero elements. <br/>
        /// The standard precondition check before iterating, shuffling, or randomly
        /// selecting from an array whose population is not guaranteed — a serialized
        /// Inspector array the designer never filled in, or a pooled buffer awaiting its
        /// first write.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNullOrEmpty<T>(this T[] array) => array == null || array.Length == 0;

        /// <summary>
        /// True if <paramref name="index"/> falls within the array's valid bounds. <br/>
        /// Also returns <c>false</c> for a <c>null</c> array, so this single check covers
        /// both failure modes an unguarded <c>array[index]</c> access is vulnerable to.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsIndexValid<T>(this T[] array, int index)
            => array != null && index >= 0 && index < array.Length;

        /// <summary>
        /// Outputs the element at <paramref name="index"/>, returning <c>false</c> instead of
        /// throwing if the array is <c>null</c> or the index is out of range. <br/>
        /// Turns an <see cref="IndexOutOfRangeException"/> risk — common when an index comes
        /// from user input, a save file, or a network payload — into an inspectable
        /// <c>bool</c> result.
        /// </summary>
        public static bool TryGet<T>(this T[] array, int index, out T value)
        {
            if (array.IsIndexValid(index))
            {
                value = array[index];
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>
        /// Returns the element at <paramref name="index"/>, or <paramref name="fallback"/> if
        /// the array is <c>null</c> or the index is out of range. <br/>
        /// Convenient one-line alternative to <see cref="TryGet{T}"/> when a sensible default
        /// makes an <c>out</c> parameter unnecessary — for example reading an optional
        /// difficulty-tier override from a settings array.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T GetOrDefault<T>(this T[] array, int index, T fallback = default)
            => array.TryGet(index, out T value) ? value : fallback;

        /// <summary>
        /// Returns the first element, or <paramref name="fallback"/> if the array is
        /// <c>null</c> or empty. <br/>
        /// Avoids the <see cref="IndexOutOfRangeException"/> a raw <c>array[0]</c> throws on
        /// an empty array — a frequent bug when a "first item" assumption (e.g. "the first
        /// waypoint") is applied to a list that a designer left empty.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T FirstOr<T>(this T[] array, T fallback = default)
            => array.IsNullOrEmpty() ? fallback : array[0];

        /// <summary>
        /// Returns the last element, or <paramref name="fallback"/> if the array is
        /// <c>null</c> or empty. <br/>
        /// The correct way to read "the most recent entry" from a history or log array
        /// without separately checking length at every call site.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T LastOr<T>(this T[] array, T fallback = default)
            => array.IsNullOrEmpty() ? fallback : array[array.Length - 1];

        #endregion

        #region Swapping & Shuffling

        /// <summary>
        /// Swaps the elements at <paramref name="indexA"/> and <paramref name="indexB"/>,
        /// doing nothing if either index is out of range. <br/>
        /// The building block for in-place algorithms (shuffling, partitioning, sorting
        /// variants) that must never throw on a caller-supplied index it does not fully
        /// control.
        /// </summary>
        public static void Swap<T>(this T[] array, int indexA, int indexB)
        {
            if (!array.IsIndexValid(indexA) || !array.IsIndexValid(indexB)) return;
            (array[indexA], array[indexB]) = (array[indexB], array[indexA]);
        }

        /// <summary>
        /// Randomizes element order in place using the Fisher-Yates algorithm and
        /// <see cref="UnityEngine.Random"/>. <br/>
        /// The standard, provably unbiased way to shuffle a loot table, a card deck, or a
        /// spawn-order array — a naive "sort by random key" shuffle is both slower and
        /// measurably biased. Operates entirely in place; no new array is allocated.
        /// </summary>
        public static void Shuffle<T>(this T[] array)
        {
            if (array == null) return;

            for (int i = array.Length - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (array[i], array[j]) = (array[j], array[i]);
            }
        }

        /// <summary>
        /// Randomizes element order in place using the Fisher-Yates algorithm and a
        /// caller-supplied <see cref="System.Random"/>. <br/>
        /// Use this overload instead of the parameterless <see cref="Shuffle{T}(T[])"/>
        /// whenever the shuffle must be reproducible — replay systems, deterministic
        /// lockstep netcode, or procedural generation seeded from a level number — since
        /// <see cref="UnityEngine.Random"/>'s global state cannot be isolated or
        /// deterministically advanced per caller.
        /// </summary>
        public static void Shuffle<T>(this T[] array, System.Random rng)
        {
            if (array == null || rng == null) return;

            for (int i = array.Length - 1; i > 0; i--)
            {
                int j = rng.Next(0, i + 1);
                (array[i], array[j]) = (array[j], array[i]);
            }
        }

        #endregion

        #region Random Selection

        /// <summary>
        /// Returns a uniformly random element, or <paramref name="fallback"/> if the array
        /// is <c>null</c> or empty. <br/>
        /// Common in ability and loot systems: "pick a random hit reaction," "pick a random
        /// idle bark" — without a separate empty-check at every call site.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T GetRandomElementOr<T>(this T[] array, T fallback = default)
            => array.IsNullOrEmpty() ? fallback : array[UnityEngine.Random.Range(0, array.Length)];

        /// <summary>
        /// Outputs a uniformly random element, returning <c>false</c> instead of a default
        /// value if the array is <c>null</c> or empty. <br/>
        /// Preferred over <see cref="GetRandomElementOr{T}"/> when "no valid element exists"
        /// must be distinguishable from "the randomly selected element happened to equal the
        /// fallback" — for example when <typeparamref name="T"/> is a reference type and
        /// <c>null</c> is itself a meaningful array entry.
        /// </summary>
        public static bool TryGetRandomElement<T>(this T[] array, out T value)
        {
            if (array.IsNullOrEmpty())
            {
                value = default;
                return false;
            }

            value = array[UnityEngine.Random.Range(0, array.Length)];
            return true;
        }

        /// <summary>
        /// Selects a random index with probability proportional to
        /// <paramref name="weights"/>, returning <see cref="NotFoundIndex"/> (<c>-1</c>) if
        /// the array is <c>null</c>, empty, or every weight is zero or negative. <br/>
        /// This is the standard technique behind loot tables and weighted spawn tables.
        /// Negative weights are treated as zero rather than throwing, since a weight table
        /// authored by a designer in a spreadsheet is far more likely to contain an
        /// accidental negative value than to warrant a hard failure over it.
        /// </summary>
        public static int GetRandomWeightedIndex(this float[] weights)
        {
            if (weights.IsNullOrEmpty()) return NotFoundIndex;

            float total = 0f;
            for (int i = 0; i < weights.Length; i++) total += Mathf.Max(0f, weights[i]);

            if (total < WeightSumEpsilon) return NotFoundIndex;

            float roll = UnityEngine.Random.Range(0f, total);
            float cumulative = 0f;

            for (int i = 0; i < weights.Length; i++)
            {
                cumulative += Mathf.Max(0f, weights[i]);
                if (roll <= cumulative) return i;
            }

            // Floating-point rounding can leave a hair's-width gap at the very end of the
            // range; falling back to the last index guarantees a valid result is always
            // returned when total >= WeightSumEpsilon.
            return weights.Length - 1;
        }

        #endregion

        #region Search

        /// <summary>
        /// Returns the index of the first element matching <paramref name="predicate"/>, or
        /// <see cref="NotFoundIndex"/> (<c>-1</c>) if the array or predicate is <c>null</c>. <br/>
        /// Null-safe wrapper around <see cref="Array.FindIndex{T}(T[], Predicate{T})"/>
        /// (an ordinary <see cref="System.Array"/> method, not LINQ) for search code that
        /// cannot guarantee either argument is populated ahead of time.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FindIndexSafe<T>(this T[] array, Predicate<T> predicate)
            => (array == null || predicate == null) ? NotFoundIndex : Array.FindIndex(array, predicate);

        /// <summary>
        /// True if the array contains an element equal to <paramref name="value"/>, using
        /// <see cref="Array.IndexOf{T}(T[], T)"/> internally. <br/>
        /// Null-safe wrapper that avoids a <see cref="NullReferenceException"/> on a
        /// not-yet-initialized array, a common state for a field awaiting a later
        /// <c>Initialize</c> call.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ContainsSafe<T>(this T[] array, T value)
            => array != null && Array.IndexOf(array, value) >= 0;

        #endregion

        #region Fill, Clear & Reverse

        /// <summary>
        /// Sets every element to <paramref name="value"/>, doing nothing if the array is
        /// <c>null</c>. <br/>
        /// Implemented as an explicit loop rather than relying on <see cref="Array.Fill{T}(T[], T)"/>
        /// to remain source-compatible with older Unity scripting runtime targets that
        /// predate its introduction, and to fold in the null guard other methods in this
        /// file share.
        /// </summary>
        public static void FillSafe<T>(this T[] array, T value)
        {
            if (array == null) return;
            for (int i = 0; i < array.Length; i++) array[i] = value;
        }

        /// <summary>
        /// Resets every element to <c>default(T)</c>, doing nothing if the array is
        /// <c>null</c>. <br/>
        /// Thin null-safe wrapper around <see cref="Array.Clear(Array, int, int)"/>, useful
        /// for resetting a pooled buffer array (e.g. a per-frame raycast results array)
        /// between uses without allocating a fresh array.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ClearSafe<T>(this T[] array)
        {
            if (array != null) Array.Clear(array, 0, array.Length);
        }

        /// <summary>
        /// Reverses element order in place, doing nothing if the array is <c>null</c>. <br/>
        /// Thin null-safe wrapper around <see cref="Array.Reverse(Array)"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReverseSafe<T>(this T[] array)
        {
            if (array != null) Array.Reverse(array);
        }

        #endregion
    }
}