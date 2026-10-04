using System;
using Nexus.Services;
using Object = UnityEngine.Object;

namespace Nexus.Diagnostics
{
    /// <summary>
    /// Framework logging service. Most call sites should use the <see cref="Log"/> static facade
    /// instead of resolving this interface directly — it exists mainly so logging is swappable
    /// and testable, and so other services can depend on it explicitly through the registry.
    /// </summary>
    public interface ILoggingService : IGameService
    {
        /// <summary>Messages below this level are dropped before any formatting occurs.</summary>
        LogLevel MinimumLevel { get; set; }

        bool IsCategoryEnabled(string category);
        void SetCategoryEnabled(string category, bool enabled);

        /// <summary>True if a call at this level/category would actually be emitted. Call this
        /// before building an expensive message when <see cref="Log"/>'s own filtering isn't
        /// enough to avoid the cost of constructing the string.</summary>
        bool IsEnabled(LogLevel level, string category);

        void Log(LogLevel level, string category, string message, Object context = null);

        /// <summary>Logs an exception. Bypasses <see cref="MinimumLevel"/> filtering (exceptions
        /// are always significant) but still respects a disabled category.</summary>
        void LogException(Exception exception, string category = null, Object context = null);
    }
}
