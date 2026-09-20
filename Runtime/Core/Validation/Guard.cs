using System;

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
        /// <summary>
        /// Throws <see cref="ArgumentNullException"/> if <paramref name="value"/> is null.
        /// For <see cref="UnityEngine.Object"/> references, this only detects a true C# null
        /// (the reference itself is null), not a destroyed Unity object, because generic code
        /// cannot invoke Unity's overloaded == operator. Use
        /// <see cref="Extensions.UnityObjectExtensions.IsNullOrDestroyed"/>
        /// when the value is a UnityEngine.Object and destroyed instances must also be treated as null.
        /// </summary>
        public static T NotNull<T>(T value, string parameterName) where T : class
        {
            if (value == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            return value;
        }

        /// <summary>
        /// Throws <see cref="ArgumentException"/> if <paramref name="value"/> is null or empty.
        /// </summary>
        public static string NotNullOrEmpty(string value, string parameterName)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException("Value cannot be null or empty.", parameterName);
            }

            return value;
        }

        /// <summary>
        /// Throws <see cref="ArgumentOutOfRangeException"/> if <paramref name="value"/> is
        /// outside the inclusive range [<paramref name="min"/>, <paramref name="max"/>].
        /// </summary>
        public static T InRange<T>(T value, T min, T max, string parameterName) where T : IComparable<T>
        {
            if (value.CompareTo(min) < 0 || value.CompareTo(max) > 0)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    $"Value must be between {min} and {max}.");
            }

            return value;
        }

        /// <summary>
        /// Throws <see cref="ArgumentException"/> if <paramref name="condition"/> is false.
        /// </summary>
        public static void IsTrue(bool condition, string parameterName, string message = null)
        {
            if (!condition)
            {
                throw new ArgumentException(message ?? "Condition was not met.", parameterName);
            }
        }
    }
}
