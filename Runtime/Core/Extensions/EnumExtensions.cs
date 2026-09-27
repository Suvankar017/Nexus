using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Text;

namespace Nexus.Core.Extensions
{
    /// <summary>
    /// Defensive enum helpers for Unity.
    ///
    /// The implementation is designed to remain correct for every supported enum backing
    /// type (sbyte, byte, short, ushort, int, uint, long, ulong), including negative values,
    /// large 64-bit flag masks, duplicate enum aliases, empty enums, and values that are not
    /// explicitly declared.
    ///
    /// The API keeps the original convenience methods while using UInt64 bit patterns for
    /// flag operations and cached metadata. Metadata is initialized once per closed enum type.
    ///
    /// Conversion methods that advertise Int32 are intentionally checked: an enum value that
    /// cannot be represented by Int32 throws OverflowException rather than silently truncating.
    /// Use ToUInt64Bits/FromUInt64Bits for lossless bit-pattern work across all backing types.
    /// </summary>
    public static class EnumExtensions
    {
        #region Internal layout and conversion support

        private enum EnumKind : byte
        {
            SByte,
            Byte,
            Int16,
            UInt16,
            Int32,
            UInt32,
            Int64,
            UInt64
        }

        private static class EnumLayout<T> where T : unmanaged, Enum
        {
            public static readonly Type UnderlyingType;
            public static readonly EnumKind Kind;
            public static readonly bool IsUnsigned;

            static EnumLayout()
            {
                UnderlyingType = Enum.GetUnderlyingType(typeof(T));

                if (UnderlyingType == typeof(sbyte))
                    Kind = EnumKind.SByte;
                else if (UnderlyingType == typeof(byte))
                    Kind = EnumKind.Byte;
                else if (UnderlyingType == typeof(short))
                    Kind = EnumKind.Int16;
                else if (UnderlyingType == typeof(ushort))
                    Kind = EnumKind.UInt16;
                else if (UnderlyingType == typeof(int))
                    Kind = EnumKind.Int32;
                else if (UnderlyingType == typeof(uint))
                    Kind = EnumKind.UInt32;
                else if (UnderlyingType == typeof(long))
                    Kind = EnumKind.Int64;
                else if (UnderlyingType == typeof(ulong))
                    Kind = EnumKind.UInt64;
                else
                    throw new NotSupportedException(
                        $"Unsupported enum underlying type '{UnderlyingType}'.");

                IsUnsigned = Kind == EnumKind.Byte ||
                             Kind == EnumKind.UInt16 ||
                             Kind == EnumKind.UInt32 ||
                             Kind == EnumKind.UInt64;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong GetUInt64Bits<T>(T value) where T : unmanaged, Enum
        {
            // Enum values are boxed by Convert, but this path is correctness-first and works
            // on every Unity scripting backend without unsafe code or runtime code generation.
            switch (EnumLayout<T>.Kind)
            {
                case EnumKind.SByte:
                    return unchecked((ulong)Convert.ToSByte(value));
                case EnumKind.Byte:
                    return Convert.ToByte(value);
                case EnumKind.Int16:
                    return unchecked((ulong)Convert.ToInt16(value));
                case EnumKind.UInt16:
                    return Convert.ToUInt16(value);
                case EnumKind.Int32:
                    return unchecked((ulong)Convert.ToInt32(value));
                case EnumKind.UInt32:
                    return Convert.ToUInt32(value);
                case EnumKind.Int64:
                    return unchecked((ulong)Convert.ToInt64(value));
                case EnumKind.UInt64:
                    return Convert.ToUInt64(value);
                default:
                    throw new NotSupportedException();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static T CreateFromUInt64Bits<T>(ulong bits) where T : unmanaged, Enum
        {
            object value;

            switch (EnumLayout<T>.Kind)
            {
                case EnumKind.SByte:
                    value = unchecked((sbyte)bits);
                    break;
                case EnumKind.Byte:
                    value = unchecked((byte)bits);
                    break;
                case EnumKind.Int16:
                    value = unchecked((short)bits);
                    break;
                case EnumKind.UInt16:
                    value = unchecked((ushort)bits);
                    break;
                case EnumKind.Int32:
                    value = unchecked((int)bits);
                    break;
                case EnumKind.UInt32:
                    value = unchecked((uint)bits);
                    break;
                case EnumKind.Int64:
                    value = unchecked((long)bits);
                    break;
                case EnumKind.UInt64:
                    value = bits;
                    break;
                default:
                    throw new NotSupportedException();
            }

            return (T)Enum.ToObject(typeof(T), value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ToInt32Checked<T>(T value) where T : unmanaged, Enum
        {
            ulong bits = GetUInt64Bits(value);

            if (EnumLayout<T>.IsUnsigned)
                return checked((int)bits);

            return checked((int)unchecked((long)bits));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static T FromInt32Checked<T>(int value) where T : unmanaged, Enum
        {
            switch (EnumLayout<T>.Kind)
            {
                case EnumKind.SByte:
                    return CreateFromUInt64Bits<T>(unchecked((ulong)checked((sbyte)value)));
                case EnumKind.Byte:
                    return CreateFromUInt64Bits<T>(checked((byte)value));
                case EnumKind.Int16:
                    return CreateFromUInt64Bits<T>(unchecked((ulong)checked((short)value)));
                case EnumKind.UInt16:
                    return CreateFromUInt64Bits<T>(checked((ushort)value));
                case EnumKind.Int32:
                    return CreateFromUInt64Bits<T>(unchecked((ulong)value));
                case EnumKind.UInt32:
                    return CreateFromUInt64Bits<T>(checked((uint)value));
                case EnumKind.Int64:
                    return CreateFromUInt64Bits<T>(unchecked((ulong)(long)value));
                case EnumKind.UInt64:
                    return CreateFromUInt64Bits<T>(checked((ulong)value));
                default:
                    throw new NotSupportedException();
            }
        }

        private static string FormatNumeric<T>(ulong bits) where T : unmanaged, Enum
        {
            if (EnumLayout<T>.IsUnsigned)
                return bits.ToString();

            return unchecked((long)bits).ToString();
        }

        #endregion

        #region Int32 / lossless bit conversion

        /// <summary>
        /// Converts an enum value to Int32 without silent truncation.
        /// Throws OverflowException if the value cannot be represented by Int32.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ToInt32Fast<T>(this T value) where T : unmanaged, Enum
            => ToInt32Checked(value);

        /// <summary>
        /// Creates an enum from an Int32, validating that the numeric value is representable
        /// by the enum's underlying type. For signed/unsigned 32-bit enums, every Int32 bit
        /// pattern is preserved where representable.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T FromInt32Fast<T>(int value) where T : unmanaged, Enum
            => FromInt32Checked<T>(value);

        /// <summary>
        /// Returns the exact underlying bit pattern as UInt64. This is lossless for all
        /// built-in enum backing types, including signed negative values and ulong masks.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong ToUInt64Bits<T>(this T value) where T : unmanaged, Enum
            => GetUInt64Bits(value);

        /// <summary>
        /// Reconstructs an enum from a raw UInt64 bit pattern using the enum's underlying type.
        /// Excess high bits are discarded for smaller backing types exactly as they would be
        /// when interpreting the corresponding underlying storage width.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T FromUInt64Bits<T>(ulong bits) where T : unmanaged, Enum
            => CreateFromUInt64Bits<T>(bits);

        #endregion

        #region Flags

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasFlagFast<T>(this T value, T flag) where T : unmanaged, Enum
        {
            ulong valueBits = GetUInt64Bits(value);
            ulong flagBits = GetUInt64Bits(flag);
            return (valueBits & flagBits) == flagBits;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasAnyFlag<T>(this T value, T flags) where T : unmanaged, Enum
        {
            ulong valueBits = GetUInt64Bits(value);
            ulong flagBits = GetUInt64Bits(flags);
            return (valueBits & flagBits) != 0UL;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T WithFlag<T>(this T value, T flag) where T : unmanaged, Enum
            => CreateFromUInt64Bits<T>(GetUInt64Bits(value) | GetUInt64Bits(flag));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T WithoutFlag<T>(this T value, T flag) where T : unmanaged, Enum
            => CreateFromUInt64Bits<T>(GetUInt64Bits(value) & ~GetUInt64Bits(flag));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T WithToggledFlag<T>(this T value, T flag) where T : unmanaged, Enum
            => CreateFromUInt64Bits<T>(GetUInt64Bits(value) ^ GetUInt64Bits(flag));

        /// <summary>
        /// True when no bit outside the union of all declared enum values is present.
        /// This works for all backing widths and negative flag values.
        /// </summary>
        public static bool IsValidFlagCombination<T>(this T value) where T : unmanaged, Enum
        {
            // On a non-[Flags] enum, only an explicitly declared value is considered valid.
            // This prevents a plain enum such as A=1, B=2 from accepting an undeclared 3.
            if (!EnumNameCache<T>.IsFlagsEnum)
                return value.IsDefinedFast();

            ulong allKnownBits = EnumNameCache<T>.AllKnownBits;
            return (GetUInt64Bits(value) & ~allKnownBits) == 0UL;
        }

        /// <summary>
        /// True when T is explicitly decorated with <see cref="FlagsAttribute"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFlagsEnum<T>() where T : unmanaged, Enum
            => EnumNameCache<T>.IsFlagsEnum;

        #endregion

        #region Cached metadata

        private static class EnumNameCache<T> where T : unmanaged, Enum
        {
            public static readonly T[] Values;
            public static readonly IReadOnlyList<T> ReadOnlyValues;
            public static readonly ulong[] Bits;
            public static readonly Dictionary<ulong, string> Names;
            public static readonly Dictionary<ulong, string> NiceNames;
            public static readonly ulong AllKnownBits;
            public static readonly bool IsFlagsEnum;

            static EnumNameCache()
            {
                T[] rawValues = (T[])Enum.GetValues(typeof(T));
                string[] rawNames = Enum.GetNames(typeof(T));

                Values = rawValues;
                ReadOnlyValues = new ReadOnlyCollection<T>(rawValues);
                Bits = new ulong[rawValues.Length];
                Names = new Dictionary<ulong, string>(rawValues.Length);
                NiceNames = new Dictionary<ulong, string>(rawValues.Length);
                IsFlagsEnum = Attribute.IsDefined(typeof(T), typeof(FlagsAttribute), false);

                ulong allKnownBits = 0UL;

                for (int i = 0; i < rawValues.Length; i++)
                {
                    ulong bits = GetUInt64Bits(rawValues[i]);
                    Bits[i] = bits;
                    allKnownBits |= bits;

                    // Enum aliases can share a numeric value. The most recently enumerated
                    // declaration wins, matching the previous dictionary-cache behavior.
                    Names[bits] = rawNames[i];
                    NiceNames[bits] = InsertSpacesBeforeCapitals(rawNames[i]);
                }

                AllKnownBits = allKnownBits;
            }
        }

        /// <summary>
        /// Returns an immutable view of the values returned by Enum.GetValues for this enum type.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IReadOnlyList<T> GetCachedValues<T>() where T : unmanaged, Enum
            => EnumNameCache<T>.ReadOnlyValues;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetValueCount<T>() where T : unmanaged, Enum
            => EnumNameCache<T>.Values.Length;

        #endregion

        #region Display & parsing

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string ToStringFast<T>(this T value) where T : unmanaged, Enum
        {
            ulong bits = GetUInt64Bits(value);
            return EnumNameCache<T>.Names.TryGetValue(bits, out string name)
                ? name
                : FormatNumeric<T>(bits);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string ToDisplayName<T>(this T value) where T : unmanaged, Enum
        {
            ulong bits = GetUInt64Bits(value);
            return EnumNameCache<T>.NiceNames.TryGetValue(bits, out string name)
                ? name
                : FormatNumeric<T>(bits);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsDefinedFast<T>(this T value) where T : unmanaged, Enum
            => EnumNameCache<T>.Names.ContainsKey(GetUInt64Bits(value));

        /// <summary>
        /// Parses an enum name or numeric value. Returns fallback for null, empty, or invalid
        /// input. This intentionally retains Enum.TryParse semantics for compatibility.
        /// For untrusted data that must correspond to a declared value, use ParseDefinedOr.
        /// </summary>
        public static T ParseOr<T>(string name, T fallback, bool ignoreCase = true)
            where T : unmanaged, Enum
        {
            if (string.IsNullOrWhiteSpace(name))
                return fallback;

            return Enum.TryParse(name.Trim(), ignoreCase, out T result)
                ? result
                : fallback;
        }

        /// <summary>
        /// Parses an enum value and rejects successfully-parsed values that are not actually
        /// declared. For [Flags] enums, validates the resulting bit combination instead.
        /// </summary>
        public static T ParseDefinedOr<T>(string name, T fallback, bool ignoreCase = true)
            where T : unmanaged, Enum
        {
            if (string.IsNullOrWhiteSpace(name) ||
                !Enum.TryParse(name.Trim(), ignoreCase, out T result))
                return fallback;

            return EnumNameCache<T>.IsFlagsEnum
                ? (result.IsValidFlagCombination() ? result : fallback)
                : (result.IsDefinedFast() ? result : fallback);
        }

        /// <summary>
        /// Non-throwing parse helper. Returns false and fallback when parsing or validation fails.
        /// </summary>
        public static bool TryParseDefined<T>(
            string name,
            out T value,
            bool ignoreCase = true)
            where T : unmanaged, Enum
        {
            value = default;

            if (string.IsNullOrWhiteSpace(name) ||
                !Enum.TryParse(name.Trim(), ignoreCase, out T result))
                return false;

            if (EnumNameCache<T>.IsFlagsEnum)
            {
                if (!result.IsValidFlagCombination())
                    return false;
            }
            else if (!result.IsDefinedFast())
            {
                return false;
            }

            value = result;
            return true;
        }

        #endregion

        #region Cycling & random

        public static T Next<T>(this T value) where T : unmanaged, Enum
        {
            T[] values = EnumNameCache<T>.Values;
            ulong[] bits = EnumNameCache<T>.Bits;

            if (values.Length == 0)
                return value;

            ulong current = GetUInt64Bits(value);
            for (int i = 0; i < bits.Length; i++)
            {
                if (bits[i] == current)
                    return values[(i + 1) % values.Length];
            }

            return values[0];
        }

        public static T Previous<T>(this T value) where T : unmanaged, Enum
        {
            T[] values = EnumNameCache<T>.Values;
            ulong[] bits = EnumNameCache<T>.Bits;

            if (values.Length == 0)
                return value;

            ulong current = GetUInt64Bits(value);
            for (int i = 0; i < bits.Length; i++)
            {
                if (bits[i] == current)
                    return values[(i - 1 + values.Length) % values.Length];
            }

            return values[0];
        }

        /// <summary>
        /// Returns a random declared enum value. Empty enums return default(T) instead of
        /// indexing an empty array.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T GetRandomValue<T>() where T : unmanaged, Enum
        {
            T[] values = EnumNameCache<T>.Values;
            if (values.Length == 0)
                return default;

            return values[UnityEngine.Random.Range(0, values.Length)];
        }

        #endregion

        #region Formatting helpers

        private static string InsertSpacesBeforeCapitals(string name)
        {
            if (string.IsNullOrEmpty(name))
                return string.Empty;

            StringBuilder builder = new StringBuilder(name.Length + 8);

            for (int i = 0; i < name.Length; i++)
            {
                char current = name[i];
                bool previousIsLower = i > 0 && char.IsLower(name[i - 1]);
                bool nextIsLower = i + 1 < name.Length && char.IsLower(name[i + 1]);

                if (i > 0 && char.IsUpper(current) && (previousIsLower || nextIsLower))
                    builder.Append(' ');

                builder.Append(current);
            }

            return builder.ToString();
        }

        #endregion
    }
}
