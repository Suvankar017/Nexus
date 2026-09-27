using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Nexus.Core.Extensions
{
    /// <summary>
    /// Unified null-safe helpers for HashSet{T}.
    /// The merged API covers membership, mutation, batch operations, set relations,
    /// extraction, clearing, and capacity trimming.
    /// </summary>
    public static class HashSetExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNullOrEmpty<T>(this HashSet<T> set)
            => set == null || set.Count == 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ContainsSafe<T>(this HashSet<T> set, T value)
            => set != null && set.Contains(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool AddSafe<T>(this HashSet<T> set, T value)
            => set != null && set.Add(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool RemoveSafe<T>(this HashSet<T> set, T value)
            => set != null && set.Remove(value);

        public static bool Toggle<T>(this HashSet<T> set, T value)
        {
            if (set == null)
                return false;

            if (set.Contains(value))
            {
                set.Remove(value);
                return false;
            }

            set.Add(value);
            return true;
        }

        public static int AddRangeUnique<T>(this HashSet<T> set, T[] values)
        {
            if (set == null || values == null)
                return 0;

            int added = 0;
            for (int i = 0; i < values.Length; i++)
            {
                if (set.Add(values[i]))
                    added++;
            }

            return added;
        }

        public static int AddRangeUnique<T>(this HashSet<T> set, List<T> values)
        {
            if (set == null || values == null)
                return 0;

            int added = 0;
            int count = values.Count;
            for (int i = 0; i < count; i++)
            {
                if (set.Add(values[i]))
                    added++;
            }

            return added;
        }

        public static int AddRangeUnique<T>(this HashSet<T> set, IEnumerable<T> values)
        {
            if (set == null || values == null || ReferenceEquals(set, values))
                return 0;

            int added = 0;
            foreach (T value in values)
            {
                if (set.Add(value))
                    added++;
            }

            return added;
        }

        /// <summary>Compatibility array overload.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddRange<T>(this HashSet<T> set, T[] values)
            => set.AddRangeUnique(values);

        /// <summary>Compatibility List overload.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddRange<T>(this HashSet<T> set, List<T> values)
            => set.AddRangeUnique(values);

        public static int RemoveRangeSafe<T>(this HashSet<T> set, T[] values)
        {
            if (set == null || values == null)
                return 0;

            int removed = 0;
            for (int i = 0; i < values.Length; i++)
            {
                if (set.Remove(values[i]))
                    removed++;
            }

            return removed;
        }

        public static int RemoveRangeSafe<T>(this HashSet<T> set, List<T> values)
        {
            if (set == null || values == null)
                return 0;

            int removed = 0;
            int count = values.Count;
            for (int i = 0; i < count; i++)
            {
                if (set.Remove(values[i]))
                    removed++;
            }

            return removed;
        }

        public static int RemoveRangeSafe<T>(this HashSet<T> set, IEnumerable<T> values)
        {
            if (set == null || values == null || ReferenceEquals(set, values))
                return 0;

            int removed = 0;
            foreach (T value in values)
            {
                if (set.Remove(value))
                    removed++;
            }

            return removed;
        }

        /// <summary>Compatibility array overload.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RemoveRange<T>(this HashSet<T> set, T[] values)
            => set.RemoveRangeSafe(values);

        /// <summary>Compatibility List overload.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RemoveRange<T>(this HashSet<T> set, List<T> values)
            => set.RemoveRangeSafe(values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ContainsAll<T>(this HashSet<T> set, IEnumerable<T> other)
        {
            if (other == null)
                return true;

            return set != null && set.IsSupersetOf(other);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool OverlapsSafe<T>(this HashSet<T> set, IEnumerable<T> other)
            => set != null && other != null && set.Overlaps(other);

        /// <summary>
        /// Treats null and empty collections as equivalent. This matches the intended contract
        /// from the source implementation while avoiding false negatives for null-vs-empty.
        /// </summary>
        public static bool SetEqualsSafe<T>(this HashSet<T> set, IEnumerable<T> other)
        {
            if (set == null)
                return IsEnumerableEmpty(other);

            if (other == null)
                return set.Count == 0;

            return set.SetEquals(other);
        }

        public static T FirstOr<T>(this HashSet<T> set, T fallback = default)
        {
            if (set == null || set.Count == 0)
                return fallback;

            using (HashSet<T>.Enumerator enumerator = set.GetEnumerator())
            {
                return enumerator.MoveNext() ? enumerator.Current : fallback;
            }
        }

        public static bool PopFirst<T>(this HashSet<T> set, out T item)
        {
            if (set == null || set.Count == 0)
            {
                item = default;
                return false;
            }

            using (HashSet<T>.Enumerator enumerator = set.GetEnumerator())
            {
                if (!enumerator.MoveNext())
                {
                    item = default;
                    return false;
                }

                item = enumerator.Current;
            }

            return set.Remove(item);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ClearSafe<T>(this HashSet<T> set)
        {
            if (set != null && set.Count > 0)
                set.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ClearAndTrimExcessSafe<T>(this HashSet<T> set)
        {
            if (set == null)
                return;

            set.Clear();
            set.TrimExcess();
        }

        private static bool IsEnumerableEmpty<T>(IEnumerable<T> values)
        {
            if (values == null)
                return true;

            if (values is ICollection<T> collection)
                return collection.Count == 0;

            using (IEnumerator<T> enumerator = values.GetEnumerator())
                return !enumerator.MoveNext();
        }
    }
}
