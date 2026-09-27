using System.Globalization;
using System.Text;

namespace Nexus.Core.Extensions
{
    /// <summary>
    /// Production-ready extension methods for <see cref="string"/> covering validation, safe
    /// parsing, case conversion, truncation, deterministic hashing and rich-text handling for
    /// UI/TMP display.
    /// <para>Design rules:</para>
    /// <list type="bullet">
    ///     <item>Unlike the rest of this framework, most members here <b>cannot</b> be zero-allocation — <see cref="string"/> is immutable, so any transformation necessarily produces a new instance. These methods are explicitly scoped to UI text, logging, save-data keys and localization, never per-frame hot loops.</item>
    ///     <item>Validation and comparison helpers (<c>IsNullOrEmpty</c>, <c>EqualsIgnoreCase</c>) remain allocation-free and are safe anywhere, including hot paths.</item>
    ///     <item>Parsing helpers never throw; malformed input returns a documented fallback instead of propagating <see cref="System.FormatException"/> from untrusted save data, network payloads or user input.</item>
    ///     <item>Hashing uses a fixed FNV-1a implementation rather than <see cref="object.GetHashCode"/>, because .NET's string hash is randomized per process and is unsafe as a stable key for save files, network protocol IDs or deterministic random seeds.</item>
    ///     <item>Rich-text helpers use manual character scanning rather than <see cref="System.Text.RegularExpressions.Regex"/>, avoiding regex engine allocation overhead for a pattern simple enough to hand-roll.</item>
    /// </list>
    /// </summary>
    public static class StringExtensions
    {
        #region Constants

        /// <summary>
        /// Default suffix appended by <see cref="Truncate(string, int, string)"/> when a string is
        /// shortened, matching the conventional ellipsis used in UI text truncation (item names,
        /// chat messages, tooltips).
        /// </summary>
        private const string DefaultTruncationSuffix = "...";

        /// <summary>
        /// FNV-1a 32-bit offset basis. Used as the starting accumulator value for
        /// <see cref="GetStableHashCode(string)"/>.
        /// </summary>
        private const int FnvOffsetBasis = unchecked((int)2166136261);

        /// <summary>
        /// FNV-1a 32-bit prime. Used as the multiplier in <see cref="GetStableHashCode(string)"/>.
        /// </summary>
        private const int FnvPrime = 16777619;

        #endregion

        #region Validation

        /// <summary>
        /// Returns <c>true</c> if <paramref name="s"/> is <c>null</c> or has zero length.
        /// <br/>
        /// Allocation-free shorthand for <see cref="string.IsNullOrEmpty(string)"/>, kept as an
        /// extension so guard clauses read fluently: <c>if (input.IsNullOrEmpty()) return;</c>
        /// </summary>
        /// <param name="s">String to test.</param>
        /// <returns><c>true</c> if <paramref name="s"/> is null or empty.</returns>
        public static bool IsNullOrEmpty(this string s)
            => string.IsNullOrEmpty(s);

        /// <summary>
        /// Returns <c>true</c> if <paramref name="s"/> is <c>null</c>, empty, or consists only of
        /// whitespace characters.
        /// <br/>
        /// Critical for validating user-entered text (player name, chat input, search fields)
        /// where a string of only spaces is functionally empty but would pass
        /// <see cref="IsNullOrEmpty(string)"/>.
        /// </summary>
        /// <param name="s">String to test.</param>
        /// <returns><c>true</c> if <paramref name="s"/> is null, empty, or whitespace-only.</returns>
        public static bool IsNullOrWhiteSpace(this string s)
            => string.IsNullOrWhiteSpace(s);

        /// <summary>
        /// Returns <c>true</c> if <paramref name="s"/> contains at least one non-whitespace
        /// character.
        /// <br/>
        /// Positive-phrased counterpart to <see cref="IsNullOrWhiteSpace(string)"/> for readable
        /// guard clauses, e.g. <c>if (playerName.HasContent()) SaveProfile();</c>
        /// </summary>
        /// <param name="s">String to test.</param>
        /// <returns><c>true</c> if <paramref name="s"/> has meaningful content.</returns>
        public static bool HasContent(this string s)
            => !string.IsNullOrWhiteSpace(s);

        /// <summary>
        /// Returns <paramref name="s"/> unchanged, or <see cref="string.Empty"/> if it is
        /// <c>null</c>.
        /// <br/>
        /// Guards against <see cref="System.NullReferenceException"/> when passing a possibly-null
        /// field (an unassigned inspector string, an optional save value) directly into UI text
        /// assignment or string concatenation.
        /// </summary>
        /// <param name="s">Candidate string.</param>
        /// <returns><paramref name="s"/> or <see cref="string.Empty"/>.</returns>
        public static string OrEmpty(this string s)
            => s ?? string.Empty;

        /// <summary>
        /// Returns <paramref name="s"/> if it <see cref="HasContent(string)"/>, otherwise returns
        /// <paramref name="fallback"/>.
        /// <br/>
        /// Ideal for resolving optional display text (item description, subtitle) to a safe
        /// default without scattering conditional null/empty checks across UI code.
        /// </summary>
        /// <param name="s">Candidate string.</param>
        /// <param name="fallback">Value returned when <paramref name="s"/> has no content.</param>
        /// <returns><paramref name="s"/> or <paramref name="fallback"/>.</returns>
        public static string OrDefault(this string s, string fallback)
            => s.HasContent() ? s : fallback;

        #endregion

        #region Comparison

        /// <summary>
        /// Returns <c>true</c> if <paramref name="a"/> and <paramref name="b"/> are equal,
        /// ignoring case, using ordinal comparison rules.
        /// <br/>
        /// Uses <see cref="System.StringComparison.OrdinalIgnoreCase"/> rather than a
        /// culture-aware comparison, avoiding surprising behavior on culture-sensitive builds
        /// (e.g. Turkish "I" casing) for gameplay-relevant string keys like item IDs or tags.
        /// </summary>
        /// <param name="a">First string.</param>
        /// <param name="b">Second string.</param>
        /// <returns><c>true</c> if the strings are equal ignoring case.</returns>
        public static bool EqualsIgnoreCase(this string a, string b)
            => string.Equals(a, b, System.StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Returns <c>true</c> if <paramref name="source"/> contains <paramref name="value"/>,
        /// ignoring case.
        /// <br/>
        /// Standard building block for search bars, chat filters and console command matching
        /// where case sensitivity would make the feature feel broken to players.
        /// </summary>
        /// <param name="source">String to search within.</param>
        /// <param name="value">Substring to search for.</param>
        /// <returns><c>true</c> if <paramref name="value"/> is found within <paramref name="source"/>.</returns>
        public static bool ContainsIgnoreCase(this string source, string value)
            => source != null && value != null && source.IndexOf(value, System.StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>
        /// Returns <c>true</c> if <paramref name="s"/> starts with <paramref name="prefix"/>,
        /// ignoring case.
        /// <br/>
        /// Useful for command-parsing systems (console commands, chat commands) where a
        /// case-sensitive prefix check would reject valid input due to player capitalization habits.
        /// </summary>
        /// <param name="s">String to test.</param>
        /// <param name="prefix">Prefix to compare against.</param>
        /// <returns><c>true</c> if <paramref name="s"/> starts with <paramref name="prefix"/>.</returns>
        public static bool StartsWithIgnoreCase(this string s, string prefix)
            => s != null && prefix != null && s.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Returns <c>true</c> if <paramref name="s"/> ends with <paramref name="suffix"/>,
        /// ignoring case.
        /// <br/>
        /// Common for file-extension-style checks on addressable keys or asset names bundled at
        /// runtime (e.g. matching "_icon" regardless of authored casing).
        /// </summary>
        /// <param name="s">String to test.</param>
        /// <param name="suffix">Suffix to compare against.</param>
        /// <returns><c>true</c> if <paramref name="s"/> ends with <paramref name="suffix"/>.</returns>
        public static bool EndsWithIgnoreCase(this string s, string suffix)
            => s != null && suffix != null && s.EndsWith(suffix, System.StringComparison.OrdinalIgnoreCase);

        #endregion

        #region Case Conversion

        /// <summary>
        /// Converts <paramref name="s"/> to title case ("the quick fox" → "The Quick Fox") using
        /// invariant culture rules.
        /// <br/>
        /// Ideal for displaying data-driven names (item IDs, enum-derived labels) as
        /// human-readable UI text without hand-authoring a display string for every value.
        /// Allocates and is intended for UI/display formatting, not hot-path logic.
        /// </summary>
        /// <param name="s">Source string to convert.</param>
        /// <returns>The title-cased string, or <paramref name="s"/> unchanged if null or empty.</returns>
        public static string ToTitleCase(this string s)
        {
            if (s.IsNullOrEmpty()) return s;
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s.ToLowerInvariant());
        }

        /// <summary>
        /// Converts <paramref name="s"/> to PascalCase, stripping spaces, underscores and hyphens
        /// ("player_health" → "PlayerHealth").
        /// <br/>
        /// Useful when generating C#-safe identifiers at runtime (dynamic enum-like keys,
        /// generated class or field names in modding/scripting tools) from designer-authored
        /// snake_case or kebab-case config values. Allocates by necessity.
        /// </summary>
        /// <param name="s">Source string to convert.</param>
        /// <returns>The PascalCase string, or <see cref="string.Empty"/> if <paramref name="s"/> has no content.</returns>
        public static string ToPascalCase(this string s)
        {
            if (!s.HasContent()) return string.Empty;

            StringBuilder sb = new StringBuilder(s.Length);
            bool capitalizeNext = true;

            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '_' || c == '-' || c == ' ')
                {
                    capitalizeNext = true;
                    continue;
                }

                sb.Append(capitalizeNext ? char.ToUpperInvariant(c) : c);
                capitalizeNext = false;
            }

            return sb.ToString();
        }

        /// <summary>
        /// Converts <paramref name="s"/> to camelCase, stripping spaces, underscores and hyphens
        /// ("player_health" → "playerHealth").
        /// <br/>
        /// Common for converting designer-authored config keys into JSON property names or
        /// scripting-API-friendly identifiers that follow camelCase convention. Allocates by necessity.
        /// </summary>
        /// <param name="s">Source string to convert.</param>
        /// <returns>The camelCase string, or <see cref="string.Empty"/> if <paramref name="s"/> has no content.</returns>
        public static string ToCamelCase(this string s)
        {
            string pascal = s.ToPascalCase();
            if (pascal.Length == 0) return pascal;

            return char.ToLowerInvariant(pascal[0]) + pascal.Substring(1);
        }

        /// <summary>
        /// Converts <paramref name="s"/> to snake_case, inserting underscores before capital
        /// letters and lower-casing the result ("PlayerHealth" → "player_health").
        /// <br/>
        /// Standard format for save-file keys, analytics event names and config file identifiers
        /// generated from C# field or property names. Allocates by necessity.
        /// </summary>
        /// <param name="s">Source string to convert.</param>
        /// <returns>The snake_case string, or <see cref="string.Empty"/> if <paramref name="s"/> has no content.</returns>
        public static string ToSnakeCase(this string s)
        {
            if (!s.HasContent()) return string.Empty;

            StringBuilder sb = new StringBuilder(s.Length + 8);

            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];

                if (char.IsUpper(c))
                {
                    if (i > 0 && s[i - 1] != '_')
                        sb.Append('_');

                    sb.Append(char.ToLowerInvariant(c));
                }
                else if (c == ' ' || c == '-')
                {
                    sb.Append('_');
                }
                else
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }

        #endregion

        #region Truncation

        /// <summary>
        /// Truncates <paramref name="s"/> to at most <paramref name="maxLength"/> visible
        /// characters, appending <paramref name="suffix"/> if truncation occurred.
        /// <br/>
        /// Guards against <paramref name="maxLength"/> being larger than the string itself or
        /// smaller than the suffix length, returning a sensibly clamped result instead of
        /// throwing <see cref="System.ArgumentOutOfRangeException"/> from a raw
        /// <see cref="string.Substring(int, int)"/> call. Ideal for chat messages, tooltips and
        /// name plates with a fixed character budget.
        /// </summary>
        /// <param name="s">Source string to truncate.</param>
        /// <param name="maxLength">Maximum total length of the result, including <paramref name="suffix"/>.</param>
        /// <param name="suffix">Suffix appended when truncation occurs. Defaults to <c>"..."</c>.</param>
        /// <returns>The original or truncated string.</returns>
        public static string Truncate(this string s, int maxLength, string suffix = DefaultTruncationSuffix)
        {
            if (s.IsNullOrEmpty() || maxLength <= 0) return string.Empty;
            if (s.Length <= maxLength) return s;

            int keep = maxLength - suffix.Length;
            if (keep <= 0) return s.Substring(0, maxLength);

            return s.Substring(0, keep) + suffix;
        }

        #endregion

        #region Safe Parsing

        /// <summary>
        /// Parses <paramref name="s"/> as an <see cref="int"/>, returning <paramref name="fallback"/>
        /// if parsing fails.
        /// <br/>
        /// Prevents <see cref="System.FormatException"/> from crashing save-file loading, config
        /// parsing or console command handling when the source data is malformed or missing.
        /// </summary>
        /// <param name="s">String to parse.</param>
        /// <param name="fallback">Value returned if parsing fails. Defaults to <c>0</c>.</param>
        /// <returns>The parsed integer, or <paramref name="fallback"/>.</returns>
        public static int ToIntOr(this string s, int fallback = 0)
            => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result) ? result : fallback;

        /// <summary>
        /// Parses <paramref name="s"/> as a <see cref="float"/>, returning <paramref name="fallback"/>
        /// if parsing fails.
        /// <br/>
        /// Uses <see cref="CultureInfo.InvariantCulture"/> explicitly, avoiding a subtle bug class
        /// where a build running under a comma-decimal locale (many European systems) fails to
        /// parse dot-decimal config or save values authored on a US-locale machine.
        /// </summary>
        /// <param name="s">String to parse.</param>
        /// <param name="fallback">Value returned if parsing fails. Defaults to <c>0f</c>.</param>
        /// <returns>The parsed float, or <paramref name="fallback"/>.</returns>
        public static float ToFloatOr(this string s, float fallback = 0f)
            => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float result) ? result : fallback;

        /// <summary>
        /// Parses <paramref name="s"/> as a <see cref="bool"/>, returning <paramref name="fallback"/>
        /// if parsing fails.
        /// <br/>
        /// Ideal for reading boolean flags out of loosely-typed config sources (INI files, remote
        /// config strings, command-line arguments) where the value might be missing or malformed.
        /// </summary>
        /// <param name="s">String to parse.</param>
        /// <param name="fallback">Value returned if parsing fails. Defaults to <c>false</c>.</param>
        /// <returns>The parsed boolean, or <paramref name="fallback"/>.</returns>
        public static bool ToBoolOr(this string s, bool fallback = false)
            => bool.TryParse(s, out bool result) ? result : fallback;

        /// <summary>
        /// Returns <c>true</c> if <paramref name="s"/> can be parsed as a numeric value.
        /// <br/>
        /// Useful for validating user input fields (stat allocation, quantity entry) before
        /// attempting a conversion, allowing UI to reject invalid characters immediately rather
        /// than silently falling back to a default value.
        /// </summary>
        /// <param name="s">String to test.</param>
        /// <returns><c>true</c> if <paramref name="s"/> represents a valid number.</returns>
        public static bool IsNumeric(this string s)
            => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out _);

        #endregion

        #region Hashing

        /// <summary>
        /// Computes a deterministic FNV-1a 32-bit hash of <paramref name="s"/>.
        /// <br/>
        /// <para>
        /// The built-in <see cref="object.GetHashCode"/> for strings is explicitly documented by
        /// Microsoft as unstable across process runs and .NET versions. Using it as a save-file
        /// key, network protocol identifier or procedural generation seed will silently break
        /// after an engine/runtime update or between platforms.
        /// </para>
        /// <para>
        /// This implementation is fixed and portable, making it safe for persistent IDs (item
        /// name → stable inventory key), deterministic seeding (level name → procedural seed) and
        /// cross-platform network protocol hashes.
        /// </para>
        /// </summary>
        /// <param name="s">String to hash.</param>
        /// <returns>A stable 32-bit hash code, or <c>0</c> if <paramref name="s"/> is null.</returns>
        public static int GetStableHashCode(this string s)
        {
            if (s == null) return 0;

            int hash = FnvOffsetBasis;
            for (int i = 0; i < s.Length; i++)
            {
                hash ^= s[i];
                hash *= FnvPrime;
            }

            return hash;
        }

        #endregion

        #region Rich Text

        /// <summary>
        /// Returns a copy of <paramref name="s"/> with all <c>&lt;...&gt;</c>-style rich-text tags
        /// removed.
        /// <br/>
        /// Uses manual character scanning rather than <see cref="System.Text.RegularExpressions.Regex"/>
        /// to avoid regex engine allocation for a tag-stripping pattern simple enough to hand-roll.
        /// Ideal for extracting plain text from TextMeshPro/UGUI rich-text strings for
        /// accessibility readers, search indexing or character-count validation that must ignore
        /// markup like <c>&lt;color&gt;</c> or <c>&lt;b&gt;</c>.
        /// </summary>
        /// <param name="s">Source string, potentially containing rich-text tags.</param>
        /// <returns>The plain-text string with tags removed.</returns>
        public static string StripRichTags(this string s)
        {
            if (s.IsNullOrEmpty()) return s;

            StringBuilder sb = new StringBuilder(s.Length);
            bool insideTag = false;

            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];

                if (c == '<') { insideTag = true; continue; }
                if (c == '>') { insideTag = false; continue; }
                if (!insideTag) sb.Append(c);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Returns the number of visible (non-tag) characters in <paramref name="s"/>, ignoring
        /// any <c>&lt;...&gt;</c>-style rich-text markup.
        /// <br/>
        /// Computes the count directly without allocating a stripped copy of the string, making it
        /// safe for per-character typewriter reveal effects that must compare progress against the
        /// true visible length rather than the raw string length inflated by markup tags.
        /// </summary>
        /// <param name="s">Source string, potentially containing rich-text tags.</param>
        /// <returns>The count of visible characters.</returns>
        public static int GetVisibleLength(this string s)
        {
            if (s.IsNullOrEmpty()) return 0;

            int count = 0;
            bool insideTag = false;

            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];

                if (c == '<') { insideTag = true; continue; }
                if (c == '>') { insideTag = false; continue; }
                if (!insideTag) count++;
            }

            return count;
        }

        #endregion

        #region Debug

        /// <summary>
        /// Wraps <paramref name="s"/> in double quotes for log output, substituting a
        /// <c>&lt;null&gt;</c> placeholder if <paramref name="s"/> is null.
        /// <br/>
        /// Removes ambiguity in log output between an empty string, a whitespace-only string and a
        /// null reference, which are otherwise visually indistinguishable when printed raw.
        /// </summary>
        /// <param name="s">String to format for logging.</param>
        /// <returns>A quoted debug representation of <paramref name="s"/>.</returns>
        public static string ToDebugString(this string s)
            => s == null ? "<null>" : $"\"{s}\"";

        #endregion
    }
}