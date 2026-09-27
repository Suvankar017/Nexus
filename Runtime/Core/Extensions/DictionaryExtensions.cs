using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Nexus.Core.Extensions
{
    /// <summary>
    /// Unified null-safe helpers for Dictionary{TKey, TValue}.
    /// The merged API covers safe lookup, fallback access, add/replace, lazy population,
    /// removal, clearing, and value updates.
    /// </summary>
    public static class DictionaryExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNullOrEmpty<TKey, TValue>(this Dictionary<TKey, TValue> dictionary)
            => dictionary == null || dictionary.Count == 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ContainsKeySafe<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key)
            => dictionary != null && !IsNullKey(key) && dictionary.ContainsKey(key);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetValueSafe<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, out TValue value)
        {
            if (dictionary != null && !IsNullKey(key))
                return dictionary.TryGetValue(key, out value);

            value = default;
            return false;
        }

        /// <summary>Compatibility alias for TryGetValueSafe.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGet<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, out TValue value)
            => dictionary.TryGetValueSafe(key, out value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TValue TryGetOr<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, TValue fallback = default)
            => dictionary.TryGetValueSafe(key, out TValue value) ? value : fallback;

        /// <summary>Compatibility alias for the value-fallback overload of TryGetOr.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TValue GetValueOr<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, TValue fallback = default)
            => dictionary.TryGetOr(key, fallback);

        public static TValue TryGetOr<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, Func<TKey, TValue> fallbackFactory)
        {
            if (dictionary.TryGetValueSafe(key, out TValue value))
                return value;

            if (IsNullKey(key) || fallbackFactory == null)
                return default;

            return fallbackFactory(key);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryAdd<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, TValue value)
        {
            if (dictionary == null || IsNullKey(key) || dictionary.ContainsKey(key))
                return false;

            dictionary.Add(key, value);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddOrReplace<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, TValue value)
        {
            if (dictionary == null || IsNullKey(key))
                return;

            dictionary[key] = value;
        }

        /// <summary>Compatibility alias for AddOrReplace.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddOrUpdate<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, TValue value)
            => dictionary.AddOrReplace(key, value);

        public static TValue GetOrAdd<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, Func<TKey, TValue> valueFactory)
        {
            if (IsNullKey(key))
                return default;

            if (dictionary == null)
                return valueFactory != null ? valueFactory(key) : default;

            if (dictionary.TryGetValue(key, out TValue existing))
                return existing;

            if (valueFactory == null)
                return default;

            TValue created = valueFactory(key);

            // A factory may itself populate the same key. Respect that value.
            if (dictionary.TryGetValue(key, out existing))
                return existing;

            dictionary[key] = created;
            return created;
        }

        /// <summary>
        /// Compatibility overload matching the original GetOrAdd(TKey, TValue) implementation.
        /// Use GetOrAddValue when passing a null literal to avoid ambiguity with the factory overload.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TValue GetOrAdd<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, TValue value)
            => dictionary.GetOrAddValue(key, value);

        /// <summary>
        /// Value overload of GetOrAdd without a null-literal ambiguity against the factory overload.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TValue GetOrAddValue<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, TValue value)
        {
            if (IsNullKey(key))
                return default;

            if (dictionary == null)
                return value;

            if (dictionary.TryGetValue(key, out TValue existing))
                return existing;

            dictionary.Add(key, value);
            return value;
        }

        public static TValue GetOrAddDefault<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key)
            where TValue : new()
        {
            if (dictionary == null || IsNullKey(key))
                return default;

            if (dictionary.TryGetValue(key, out TValue existing))
                return existing;

            TValue created = new TValue();

            if (dictionary.TryGetValue(key, out existing))
                return existing;

            dictionary.Add(key, created);
            return created;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool RemoveSafe<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key)
            => dictionary != null && !IsNullKey(key) && dictionary.Remove(key);

        public static bool TryRemove<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, out TValue value)
        {
            if (dictionary != null && !IsNullKey(key) && dictionary.TryGetValue(key, out value))
            {
                dictionary.Remove(key);
                return true;
            }

            value = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ClearSafe<TKey, TValue>(this Dictionary<TKey, TValue> dictionary)
        {
            if (dictionary != null && dictionary.Count > 0)
                dictionary.Clear();
        }

        public static bool TryUpdate<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, Func<TValue, TValue> updater)
        {
            if (dictionary == null || IsNullKey(key) || updater == null)
                return false;

            if (!dictionary.TryGetValue(key, out TValue current))
                return false;

            TValue updated = updater(current);
            dictionary[key] = updated;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsNullKey<TKey>(TKey key) => key is null;
    }
}