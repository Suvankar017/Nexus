using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Nexus.Core.Extensions;

namespace Nexus.Core.Validation
{
    /// <summary>
    /// Argument and precondition validation for public API boundaries.
    /// Every method throws immediately on failure with the offending parameter name.
    /// Intended for entry points (public methods, initialization), not for use inside
    /// per-frame hot paths where the branch and string-building cost is unwanted.
    /// </summary>
    public static class Guard
    {
        #region Null & Reference

        /// <summary>
        /// Throws <see cref="ArgumentNullException"/> if <paramref name="value"/> is <see langword="null"/>.
        /// For <see cref="UnityEngine.Object"/> references, this only detects a true C# null
        /// (the reference itself is null), not a destroyed Unity object, because generic code
        /// cannot invoke Unity's overloaded == operator. Use
        /// <see cref="UnityObjectExtensions.IsNullOrDestroyed"/>
        /// when the value is a UnityEngine.Object and destroyed instances must also be treated as null.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T NotNull<T>(T value, string parameterName) where T : class
        {
            if (value == null) throw new ArgumentNullException(parameterName);
            return value;
        }

        /// <summary>
        /// Throws <see cref="ArgumentNullException"/> if <paramref name="value"/> has no value. <br/>
        /// Overload for nullable value types (<c>int?</c>, <c>float?</c>), which the
        /// reference-type-constrained <see cref="NotNull{T}"/> cannot accept, returning the
        /// unwrapped value on success so callers don't need a separate <c>.Value</c> access.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T NotNull<T>(T? value, string parameterName) where T : struct
        {
            if (!value.HasValue) throw new ArgumentNullException(parameterName);
            return value.Value;
        }

        #endregion

        #region Unity Object

        /// <summary>
        /// Throws <see cref="ArgumentNullException"/> if <paramref name="value"/> is null or has
        /// been destroyed. <br/>
        /// The correct null check for any <see cref="UnityEngine.Object"/>-derived parameter
        /// (<see cref="UnityEngine.Transform"/>, <see cref="UnityEngine.Rigidbody"/>, a <see cref="UnityEngine.MonoBehaviour"/>
        /// reference). Delegates to <see cref="UnityObjectExtensions.IsNullOrDestroyed"/>, which
        /// invokes Unity's overloaded <c>==</c> operator — exactly the check the generic
        /// <see cref="NotNull{T}"/> path is structurally unable to perform.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T NotDestroyed<T>(T value, string parameterName) where T : UnityEngine.Object
        {
            if (value.IsNullOrDestroyed())
                throw new ArgumentNullException(parameterName, "Object is null or has been destroyed.");

            return value;
        }

        #endregion

        #region Strings

        /// <summary>
        /// Throws <see cref="ArgumentException"/> if <paramref name="value"/> is null or empty.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string NotNullOrEmpty(string value, string parameterName)
        {
            if (string.IsNullOrEmpty(value))
                throw new ArgumentException("Value cannot be null or empty.", parameterName);

            return value;
        }

        /// <summary>
        /// Throws <see cref="ArgumentException"/> if <paramref name="value"/> is null, empty, or
        /// consists only of whitespace. <br/>
        /// Stricter than <see cref="NotNullOrEmpty(string, string)"/>; use for identifiers, save
        /// keys, and display names where a string of only spaces is functionally as invalid as an
        /// empty one but would silently pass the weaker check.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string NotNullOrWhiteSpace(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Value cannot be null, empty, or whitespace.", parameterName);

            return value;
        }

        #endregion

        #region Collections

        /// <summary>
        /// Throws <see cref="ArgumentException"/> if <paramref name="collection"/> is null or
        /// contains no elements. <br/>
        /// Catches an unpopulated designer-authored collection (an empty waypoint list, an empty
        /// loot table) at the API boundary, well before it fails as a confusing
        /// <see cref="IndexOutOfRangeException"/> or <see cref="InvalidOperationException"/> deep
        /// inside unrelated logic that assumed at least one entry.
        /// </summary>
        public static ICollection<T> NotNullOrEmpty<T>(ICollection<T> collection, string parameterName)
        {
            if (collection == null || collection.Count == 0)
                throw new ArgumentException("Collection cannot be null or empty.", parameterName);

            return collection;
        }

        /// <summary>
        /// Throws <see cref="ArgumentException"/> if <paramref name="array"/> is null or has zero
        /// length. <br/>
        /// Array-specific overload provided alongside the <see cref="ICollection{T}"/> version so
        /// a plain <c>T[]</c> parameter does not require an implicit interface conversion at the
        /// call site.
        /// </summary>
        public static T[] NotNullOrEmpty<T>(T[] array, string parameterName)
        {
            if (array == null || array.Length == 0)
                throw new ArgumentException("Array cannot be null or empty.", parameterName);

            return array;
        }

        /// <summary>
        /// Throws <see cref="ArgumentOutOfRangeException"/> if <paramref name="index"/> does not
        /// fall within <c>[0, count)</c>. <br/>
        /// The standard bounds check for a public API that accepts an index into a caller-owned
        /// array or list — a save-slot index, an inventory-slot index, a dialogue-choice index —
        /// where the resulting exception names both the offending parameter and the valid range,
        /// unlike the raw indexer's own generic out-of-range message.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int IndexInRange(int index, int count, string parameterName)
        {
            if (index < 0 || index >= count)
                throw new ArgumentOutOfRangeException(parameterName, index, $"Index must be between 0 and {count - 1}.");

            return index;
        }

        #endregion

        #region Numeric Range & Comparison

        /// <summary>
        /// Throws <see cref="ArgumentOutOfRangeException"/> if <paramref name="value"/> is
        /// outside the inclusive range [<paramref name="min"/>, <paramref name="max"/>]. <br/>
        /// For floating-point values that might be <c>NaN</c>, combine with <see cref="NotNaN"/>
        /// first: <see cref="IComparable{T}.CompareTo"/> orders <c>NaN</c> as less than every
        /// other value, so it will fail this check, but not with a message that says why.
        /// </summary>
        public static T InRange<T>(T value, T min, T max, string parameterName) where T : IComparable<T>
        {
            if (value.CompareTo(min) < 0 || value.CompareTo(max) > 0)
                throw new ArgumentOutOfRangeException(parameterName, value, $"Value must be between {min} and {max}.");

            return value;
        }

        /// <summary>
        /// Throws <see cref="ArgumentOutOfRangeException"/> if <paramref name="value"/> is not
        /// strictly greater than zero. <br/>
        /// Use for quantities only meaningful as strictly positive — a radius, a speed
        /// multiplier, a duration — where zero would silently produce a degenerate result (an
        /// invisible AoE, a stationary projectile) rather than throwing on its own.
        /// </summary>
        public static T Positive<T>(T value, string parameterName) where T : IComparable<T>
        {
            if (value.CompareTo(default) <= 0)
                throw new ArgumentOutOfRangeException(parameterName, value, "Value must be greater than zero.");

            return value;
        }

        /// <summary>
        /// Throws <see cref="ArgumentOutOfRangeException"/> if <paramref name="value"/> is less
        /// than zero. <br/>
        /// Use for quantities that allow zero but never a negative value — a health amount, a
        /// stack count, a cooldown timer — a more permissive constraint than <see cref="Positive{T}"/>.
        /// </summary>
        public static T NotNegative<T>(T value, string parameterName) where T : IComparable<T>
        {
            if (value.CompareTo(default) < 0)
                throw new ArgumentOutOfRangeException(parameterName, value, "Value must not be negative.");

            return value;
        }

        /// <summary>
        /// Throws <see cref="ArgumentOutOfRangeException"/> if <paramref name="value"/> is not
        /// strictly greater than <paramref name="threshold"/>.
        /// </summary>
        public static T GreaterThan<T>(T value, T threshold, string parameterName) where T : IComparable<T>
        {
            if (value.CompareTo(threshold) <= 0)
                throw new ArgumentOutOfRangeException(parameterName, value, $"Value must be greater than {threshold}.");

            return value;
        }

        /// <summary>
        /// Throws <see cref="ArgumentOutOfRangeException"/> if <paramref name="value"/> is not
        /// strictly less than <paramref name="threshold"/>.
        /// </summary>
        public static T LessThan<T>(T value, T threshold, string parameterName) where T : IComparable<T>
        {
            if (value.CompareTo(threshold) >= 0)
                throw new ArgumentOutOfRangeException(parameterName, value, $"Value must be less than {threshold}.");

            return value;
        }

        /// <summary>
        /// Throws <see cref="ArgumentOutOfRangeException"/> if <paramref name="value"/> is less
        /// than <paramref name="minimum"/>. <br/>
        /// The inclusive-lower-bound counterpart to <see cref="GreaterThan{T}"/>, for values that
        /// are allowed to equal their floor exactly (a minimum stat roll, a minimum party size).
        /// </summary>
        public static T AtLeast<T>(T value, T minimum, string parameterName) where T : IComparable<T>
        {
            if (value.CompareTo(minimum) < 0)
                throw new ArgumentOutOfRangeException(parameterName, value, $"Value must be at least {minimum}.");

            return value;
        }

        /// <summary>
        /// Throws <see cref="ArgumentOutOfRangeException"/> if <paramref name="value"/> is
        /// greater than <paramref name="maximum"/>. <br/>
        /// The inclusive-upper-bound counterpart to <see cref="LessThan{T}"/>.
        /// </summary>
        public static T AtMost<T>(T value, T maximum, string parameterName) where T : IComparable<T>
        {
            if (value.CompareTo(maximum) > 0)
                throw new ArgumentOutOfRangeException(parameterName, value, $"Value must be at most {maximum}.");

            return value;
        }

        #endregion

        #region Numeric Validity

        /// <summary>
        /// Throws <see cref="ArgumentException"/> if <paramref name="value"/> is
        /// <see cref="float.NaN"/>. <br/>
        /// A <c>NaN</c> rarely throws anywhere on its own — it silently propagates through
        /// vector math and corrupts a <see cref="Transform"/> or physics body long before it
        /// surfaces as an "Invalid AABB" render warning far from where it actually entered.
        /// Guard public entry points (a constructor taking a speed or damage value) with this.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float NotNaN(float value, string parameterName)
        {
            if (float.IsNaN(value)) throw new ArgumentException("Value cannot be NaN.", parameterName);
            return value;
        }

        /// <summary>
        /// Throws <see cref="ArgumentException"/> if <paramref name="value"/> is
        /// <see cref="float.NaN"/> or infinite. <br/>
        /// The stricter counterpart to <see cref="NotNaN"/> for values that must be usable in
        /// further arithmetic — a divisor, a normalization input, a lerp factor — where
        /// <see cref="float.PositiveInfinity"/> is just as destructive as <c>NaN</c> but would
        /// pass that weaker check.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Finite(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentException("Value must be a finite number.", parameterName);

            return value;
        }

        /// <summary>
        /// Throws <see cref="ArgumentException"/> if any component of <paramref name="value"/> is
        /// <c>NaN</c> or infinite. <br/>
        /// The <see cref="UnityEngine.Vector3"/> counterpart to <see cref="Finite(float, string)"/>, for
        /// guarding public APIs that accept a world position, direction, or scale — the most
        /// common vector-shaped entry points where a single bad component silently corrupts a
        /// <see cref="UnityEngine.Transform"/> or a physics body.
        /// </summary>
        public static UnityEngine.Vector3 Finite(UnityEngine.Vector3 value, string parameterName)
        {
            bool anyNaN = float.IsNaN(value.x) || float.IsNaN(value.y) || float.IsNaN(value.z);
            bool anyInfinite = float.IsInfinity(value.x) || float.IsInfinity(value.y) || float.IsInfinity(value.z);

            if (anyNaN || anyInfinite)
                throw new ArgumentException("Vector components must all be finite numbers.", parameterName);

            return value;
        }

        #endregion

        #region Value Type Defaults

        /// <summary>
        /// Throws <see cref="ArgumentException"/> if <paramref name="value"/> equals
        /// <c>default(T)</c>. <br/>
        /// Catches an unassigned identifier struct — an entity ID, an item ID, a
        /// <see cref="Guid"/> — slipping through a public API as its zeroed default value
        /// instead of a real, intentionally assigned identifier. This is the value-type
        /// equivalent of the mistake <see cref="NotNull{T}"/> already catches for reference types.
        /// </summary>
        public static T NotDefault<T>(T value, string parameterName) where T : struct, IEquatable<T>
        {
            if (value.Equals(default(T)))
                throw new ArgumentException("Value must not be the default value.", parameterName);

            return value;
        }

        #endregion

        #region Enum

        /// <summary>
        /// Throws <see cref="ArgumentException"/> if <paramref name="value"/> does not match one
        /// of <typeparamref name="TEnum"/>'s declared names. <br/>
        /// The correct validity check for a plain (non-flags) enum value arriving from an
        /// untrusted source — save data from an older app version, a network payload, a value
        /// deserialized from JSON — where an out-of-range integer could otherwise be silently
        /// cast into the enum and misinterpreted deep inside a switch statement's default case.
        /// Delegates to <see cref="EnumExtensions.IsDefinedFast{T}"/> rather than the
        /// reflection-heavy <see cref="Enum.IsDefined(Type, object)"/>, so validating many enum
        /// values during a save-load pass stays cheap.
        /// <para>
        /// For a <c>[Flags]</c> enum, composite values are intentionally not individually
        /// declared; validate those with <see cref="EnumExtensions.IsValidFlagCombination{T}"/>
        /// instead of this method.
        /// </para>
        /// </summary>
        public static TEnum IsDefinedEnum<TEnum>(TEnum value, string parameterName) where TEnum : unmanaged, Enum
        {
            if (!value.IsDefinedFast())
                throw new ArgumentException($"Value '{value}' is not a defined member of {typeof(TEnum).Name}.", parameterName);

            return value;
        }

        #endregion

        #region Conditions

        /// <summary>
        /// Throws <see cref="ArgumentException"/> if <paramref name="condition"/> is false.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void IsTrue(bool condition, string parameterName, string message = null)
        {
            if (!condition) throw new ArgumentException(message ?? "Condition was not met.", parameterName);
        }

        /// <summary>
        /// Throws <see cref="ArgumentException"/> if <paramref name="condition"/> is true. <br/>
        /// The logical complement to <see cref="IsTrue"/>, useful when the invalid state reads
        /// more naturally as a positive condition —
        /// <c>Guard.IsFalse(target.IsDead, nameof(target), "Target must be alive.")</c> is
        /// clearer at the call site than negating the condition to fit <see cref="IsTrue"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void IsFalse(bool condition, string parameterName, string message = null)
        {
            if (condition) throw new ArgumentException(message ?? "Condition was not met.", parameterName);
        }

        #endregion
    }
}
