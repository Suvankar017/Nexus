using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using Nexus.Services;

namespace Nexus.Diagnostics
{
    /// <summary>
    /// Default <see cref="ILoggingService"/> implementation. Routes to <see cref="Debug"/> with a
    /// "[Category] " prefix; in the Editor the prefix is colorized per category (a stable hash of
    /// the category name picks a color from a fixed palette) using Unity's rich-text console
    /// support. Player builds get the plain, uncolored prefix — rich-text tags are not stripped by
    /// most log destinations outside the Editor, so color is Editor-only rather than emitted and
    /// left unreadable.
    /// </summary>
    public sealed class LoggingService : ILoggingService
    {
        private readonly HashSet<string> _disabledCategories = new();

        public LogLevel MinimumLevel { get; set; } = LogLevel.Trace;

        public void Initialize(IServiceRegistry registry)
        {
            Diagnostics.Log.Bind(this);
        }

        public void Shutdown()
        {
            Diagnostics.Log.Unbind(this);
            _disabledCategories.Clear();
        }

        public bool IsCategoryEnabled(string category)
        {
            return string.IsNullOrEmpty(category) || !_disabledCategories.Contains(category);
        }

        public void SetCategoryEnabled(string category, bool enabled)
        {
            if (string.IsNullOrEmpty(category))
            {
                return;
            }

            if (enabled)
            {
                _disabledCategories.Remove(category);
            }
            else
            {
                _disabledCategories.Add(category);
            }
        }

        public bool IsEnabled(LogLevel level, string category)
        {
            return level >= MinimumLevel && IsCategoryEnabled(category);
        }

        public void Log(LogLevel level, string category, string message, Object context = null)
        {
            if (!IsEnabled(level, category))
            {
                return;
            }

            string formatted = Format(category, message);
            switch (level)
            {
                case LogLevel.Error:
                case LogLevel.Fatal:
                    Debug.LogError(formatted, context);
                    break;
                case LogLevel.Warning:
                    Debug.LogWarning(formatted, context);
                    break;
                default:
                    Debug.Log(formatted, context);
                    break;
            }
        }

        public void LogException(Exception exception, string category = null, Object context = null)
        {
            if (exception == null || !IsCategoryEnabled(category))
            {
                return;
            }

            Debug.LogException(exception, context);
        }

#if UNITY_EDITOR
        private static readonly string[] CategoryColors =
        {
            "#4FC3F7", "#AED581", "#FFD54F", "#BA68C8", "#FF8A65", "#90A4AE"
        };

        private static string Format(string category, string message)
        {
            if (string.IsNullOrEmpty(category))
            {
                return message;
            }

            int index = (category.GetHashCode() & int.MaxValue) % CategoryColors.Length;
            return $"<color={CategoryColors[index]}>[{category}]</color> {message}";
        }
#else
        private static string Format(string category, string message)
        {
            return string.IsNullOrEmpty(category) ? message : $"[{category}] {message}";
        }
#endif
    }
}
