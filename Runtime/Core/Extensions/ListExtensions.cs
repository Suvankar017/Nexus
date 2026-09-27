using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Nexus.Core.Extensions
{
    /// <summary>
    /// Extension methods for <see cref="List{T}"/> covering safe indexed access, O(1)
    /// unordered removal, in-place shuffling, weighted-free random selection, stack-style
    /// access, and capacity management.
    /// <para>Design rules:</para>
    /// <list type="bullet">
    /// <item>Every accessor treats a <c>null</c> list as "empty" rather than throwing,
    /// matching <c>ArrayExtensions</c>'s behavior for the same reason: a list backing a
    /// serialized field or an object pool is routinely <c>null</c> before its first
    /// population, and every method here degrades gracefully instead of throwing.</item>
    /// <item><see cref="List{T}.RemoveAt(int)"/> is <c>O(n)</c> because it shifts every
    /// following element down by one. <see cref="RemoveAtSwapBack{T}"/> and
    /// <see cref="RemoveSwapBack{T}"/> instead move the last element into the removed slot
    /// and pop the list's new tail — <c>O(1)</c> — at the cost of not preserving element
    /// order. This is the standard technique for pooled-object tracking lists (active enemies,
    /// live projectiles) where order never matters and per-frame removal cost does.</item>
    /// <item>Shuffling mirrors <c>ArrayExtensions.Shuffle</c> exactly: Fisher-Yates, strictly
    /// in place, with both a <see cref="UnityEngine.Random"/> overload for gameplay use and a
    /// <see cref="System.Random"/> overload for reproducible sequences.</item>
    /// <item>Capacity helpers exist because a <see cref="List{T}"/> that grows past its
    /// current <see cref="List{T}.Capacity"/> reallocates and copies its entire backing
    /// array — a hidden, easily-avoided allocation spike for a list built up one
    /// <see cref="List{T}.Add(T)"/> call at a time (e.g. collecting frame-by-frame query
    /// results) when the eventual size is known or reasonably estimable in advance.</item>
    /// </list>
    /// </summary>
    public static class ListExtensions
    {
        #region Validation & Safe Access

        /// <summary>
        /// True if the list is <c>null</c> or has zero elements. <br/>
        /// The standard precondition check before iterating, shuffling, or randomly
        /// selecting from a list whose population is not guaranteed.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNullOrEmpty<T>(this List<T> list) => list == null || list.Count == 0;

        /// <summary>
        /// True if <paramref name="index"/> falls within the list's valid bounds. <br/>
        /// Also returns <c>false</c> for a <c>null</c> list, so this single check covers
        /// both failure modes an unguarded <c>list[index]</c> access is vulnerable to.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsIndexValid<T>(this List<T> list, int index)
            => list != null && index >= 0 && index < list.Count;

        /// <summary>
        /// Outputs the element at <paramref name="index"/>, returning <c>false</c> instead of
        /// throwing if the list is <c>null</c> or the index is out of range. <br/>
        /// Turns an <see cref="ArgumentOutOfRangeException"/> risk — common when an index
        /// comes from UI selection state or a network payload — into an inspectable
        /// <c>bool</c> result.
        /// </summary>
        public static bool TryGet<T>(this List<T> list, int index, out T value)
        {
            if (list.IsIndexValid(index))
            {
                value = list[index];
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>
        /// Returns the element at <paramref name="index"/>, or <paramref name="fallback"/> if
        /// the list is <c>null</c> or the index is out of range.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T GetOrDefault<T>(this List<T> list, int index, T fallback = default)
            => list.TryGet(index, out T value) ? value : fallback;

        /// <summary>
        /// Returns the first element, or <paramref name="fallback"/> if the list is
        /// <c>null</c> or empty. <br/>
        /// Avoids the <see cref="ArgumentOutOfRangeException"/> a raw <c>list[0]</c> throws
        /// on an empty list.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T FirstOr<T>(this List<T> list, T fallback = default)
            => list.IsNullOrEmpty() ? fallback : list[0];

        /// <summary>
        /// Returns the last element, or <paramref name="fallback"/> if the list is
        /// <c>null</c> or empty. <br/>
        /// The correct way to read "the most recent entry" from a history or log list
        /// without separately checking count at every call site.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T LastOr<T>(this List<T> list, T fallback = default)
            => list.IsNullOrEmpty() ? fallback : list[list.Count - 1];

        #endregion

        #region Fast Unordered Removal

        /// <summary>
        /// Removes the element at <paramref name="index"/> in <c>O(1)</c> time by moving the
        /// list's last element into its place and popping the tail, doing nothing if the
        /// index is out of range. <br/>
        /// Element order is not preserved. This is the correct removal method for tracking
        /// lists where order never matters — active enemies, live projectiles, pooled
        /// particles — and using the standard <see cref="List{T}.RemoveAt(int)"/> there
        /// would otherwise cost an unnecessary <c>O(n)</c> shift on every single removal.
        /// </summary>
        public static void RemoveAtSwapBack<T>(this List<T> list, int index)
        {
            if (!list.IsIndexValid(index)) return;

            int lastIndex = list.Count - 1;
            list[index] = list[lastIndex];
            list.RemoveAt(lastIndex);
        }

        /// <summary>
        /// Finds and removes the first occurrence of <paramref name="item"/> using the
        /// <c>O(1)</c> swap-back technique from <see cref="RemoveAtSwapBack{T}"/>, returning
        /// <c>false</c> if the item is not found. <br/>
        /// Still an <c>O(n)</c> search to locate the item — the same cost as
        /// <see cref="List{T}.Remove(T)"/> — but avoids that method's subsequent <c>O(n)</c>
        /// shift once the item is found, which matters for large unordered tracking lists.
        /// </summary>
        public static bool RemoveSwapBack<T>(this List<T> list, T item)
        {
            if (list == null) return false;

            int index = list.IndexOf(item);
            if (index < 0) return false;

            list.RemoveAtSwapBack(index);
            return true;
        }

        #endregion

        #region Swapping, Shuffling & Reordering

        /// <summary>
        /// Swaps the elements at <paramref name="indexA"/> and <paramref name="indexB"/>,
        /// doing nothing if either index is out of range.
        /// </summary>
        public static void Swap<T>(this List<T> list, int indexA, int indexB)
        {
            if (!list.IsIndexValid(indexA) || !list.IsIndexValid(indexB)) return;
            (list[indexA], list[indexB]) = (list[indexB], list[indexA]);
        }

        /// <summary>
        /// Moves the element at <paramref name="fromIndex"/> to <paramref name="toIndex"/>,
        /// shifting the elements between them, doing nothing if either index is out of
        /// range. <br/>
        /// Unlike <see cref="Swap{T}"/>, this preserves the relative order of every other
        /// element — the correct operation for drag-and-drop reordering in an inventory or
        /// quest-log UI, where swapping would incorrectly displace a third item instead of
        /// sliding it over by one.
        /// </summary>
        public static void MoveItem<T>(this List<T> list, int fromIndex, int toIndex)
        {
            if (!list.IsIndexValid(fromIndex) || !list.IsIndexValid(toIndex) || fromIndex == toIndex) return;

            T item = list[fromIndex];
            list.RemoveAt(fromIndex);
            list.Insert(toIndex, item);
        }

        /// <summary>
        /// Randomizes element order in place using the Fisher-Yates algorithm and
        /// <see cref="UnityEngine.Random"/>. <br/>
        /// Matches <c>ArrayExtensions.Shuffle</c> exactly; use this overload for card decks,
        /// turn orders, or loot pools stored as a <see cref="List{T}"/> rather than an array.
        /// </summary>
        public static void Shuffle<T>(this List<T> list)
        {
            if (list == null) return;

            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        /// <summary>
        /// Randomizes element order in place using the Fisher-Yates algorithm and a
        /// caller-supplied <see cref="System.Random"/>. <br/>
        /// Use for reproducible shuffles — replays, deterministic lockstep netcode,
        /// seeded procedural generation — matching the corresponding
        /// <c>ArrayExtensions.Shuffle</c> overload.
        /// </summary>
        public static void Shuffle<T>(this List<T> list, System.Random rng)
        {
            if (list == null || rng == null) return;

            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        #endregion

        #region Random Selection

        /// <summary>
        /// Returns a uniformly random element, or <paramref name="fallback"/> if the list is
        /// <c>null</c> or empty.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T GetRandomElementOr<T>(this List<T> list, T fallback = default)
            => list.IsNullOrEmpty() ? fallback : list[UnityEngine.Random.Range(0, list.Count)];

        /// <summary>
        /// Outputs a uniformly random element, returning <c>false</c> instead of a default
        /// value if the list is <c>null</c> or empty. <br/>
        /// Preferred over <see cref="GetRandomElementOr{T}"/> when "no valid element exists"
        /// must be distinguishable from "the randomly selected element happened to equal the
        /// fallback."
        /// </summary>
        public static bool TryGetRandomElement<T>(this List<T> list, out T value)
        {
            if (list.IsNullOrEmpty())
            {
                value = default;
                return false;
            }

            value = list[UnityEngine.Random.Range(0, list.Count)];
            return true;
        }

        #endregion

        #region Stack-Style Access

        /// <summary>
        /// Removes and outputs the last element, returning <c>false</c> instead of throwing
        /// if the list is <c>null</c> or empty. <br/>
        /// Lets a <see cref="List{T}"/> double as a simple undo stack, action-queue buffer,
        /// or command history without a separate <see cref="Stack{T}"/> allocation, while
        /// still supporting indexed access to the rest of the collection when needed.
        /// </summary>
        public static bool PopBack<T>(this List<T> list, out T value)
        {
            if (list.IsNullOrEmpty())
            {
                value = default;
                return false;
            }

            int lastIndex = list.Count - 1;
            value = list[lastIndex];
            list.RemoveAt(lastIndex);
            return true;
        }

        /// <summary>
        /// Outputs the last element without removing it, returning <c>false</c> instead of
        /// throwing if the list is <c>null</c> or empty. <br/>
        /// The read-only counterpart to <see cref="PopBack{T}"/>, useful for peeking at the
        /// top of an undo stack or the most recent entry in a bounded history buffer before
        /// deciding whether to consume it.
        /// </summary>
        public static bool PeekBack<T>(this List<T> list, out T value)
        {
            if (list.IsNullOrEmpty())
            {
                value = default;
                return false;
            }

            value = list[list.Count - 1];
            return true;
        }

        #endregion

        #region Capacity & Population

        /// <summary>
        /// Grows the list's <see cref="List{T}.Capacity"/> to at least
        /// <paramref name="minCapacity"/> if it is not already that large, doing nothing
        /// otherwise. <br/>
        /// A <see cref="List{T}"/> built up one <see cref="List{T}.Add(T)"/> call at a time
        /// reallocates and copies its entire backing array every time it outgrows its current
        /// capacity — a hidden, easily-avoided allocation spike when the eventual size is
        /// known ahead of time (e.g. "this frame's query will return at most
        /// <c>maxTargets</c> results"). Implemented manually via the
        /// <see cref="List{T}.Capacity"/> setter rather than the newer
        /// <c>List&lt;T&gt;.EnsureCapacity</c> BCL method to remain source-compatible with
        /// older Unity scripting runtime targets.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EnsureCapacitySafe<T>(this List<T> list, int minCapacity)
        {
            if (list != null && list.Capacity < minCapacity) list.Capacity = minCapacity;
        }

        /// <summary>
        /// Shrinks the list's backing array to exactly match its current
        /// <see cref="List{T}.Count"/>, doing nothing if the list is <c>null</c>. <br/>
        /// Thin null-safe wrapper around <see cref="List{T}.TrimExcess"/>. Worth calling on a
        /// large list that has just shrunk dramatically and is expected to stay small for a
        /// long time — for example, a level-load scratch list that will sit dormant for the
        /// rest of the session — to release the otherwise-retained backing array memory.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void TrimExcessSafe<T>(this List<T> list)
        {
            list?.TrimExcess();
        }

        /// <summary>
        /// Adds <paramref name="item"/> only if the list does not already contain it,
        /// returning <c>false</c> without effect if it is already present. <br/>
        /// A straightforward <c>O(n)</c> uniqueness check via <see cref="List{T}.Contains(T)"/> —
        /// appropriate for small-to-medium lists such as an active-status-effects list or a
        /// party-member roster. For large collections requiring frequent uniqueness checks,
        /// a <see cref="HashSet{T}"/> is the correct data structure instead of this method.
        /// </summary>
        public static bool AddIfNotContains<T>(this List<T> list, T item)
        {
            if (list == null || list.Contains(item)) return false;
            list.Add(item);
            return true;
        }

        #endregion
    }
}