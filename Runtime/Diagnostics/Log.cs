using System;
using Object = UnityEngine.Object;

namespace Nexus.Diagnostics
{
    /// <summary>
    /// Static logging facade bound to whichever <see cref="ILoggingService"/> is currently
    /// initialized (bound in <see cref="LoggingService.Initialize"/>, unbound in
    /// <see cref="LoggingService.Shutdown"/>). Exists so gameplay code can log without carrying an
    /// <see cref="ILoggingService"/> reference everywhere; code that already holds a registry
    /// reference (other services, in particular) should prefer resolving
    /// <see cref="ILoggingService"/> directly.
    ///
    /// Every method here is a no-op when nothing is bound (e.g. before Bootstrap reaches Ready, or
    /// after Shutdown) rather than throwing — a missing logging destination must never itself be a
    /// crash source.
    /// </summary>
    public static class Log
    {
        private static ILoggingService _service;

        public static void Bind(ILoggingService service) => _service = service;

        public static void Unbind(ILoggingService service)
        {
            if (_service == service)
            {
                _service = null;
            }
        }

        public static bool IsEnabled(LogLevel level, string category) =>
            _service != null && _service.IsEnabled(level, category);

        public static void Trace(string category, string message, Object context = null) =>
            _service?.Log(LogLevel.Trace, category, message, context);

        public static void Debug(string category, string message, Object context = null) =>
            _service?.Log(LogLevel.Debug, category, message, context);

        public static void Info(string category, string message, Object context = null) =>
            _service?.Log(LogLevel.Info, category, message, context);

        public static void Warning(string category, string message, Object context = null) =>
            _service?.Log(LogLevel.Warning, category, message, context);

        public static void Error(string category, string message, Object context = null) =>
            _service?.Log(LogLevel.Error, category, message, context);

        public static void Fatal(string category, string message, Object context = null) =>
            _service?.Log(LogLevel.Fatal, category, message, context);

        public static void Exception(Exception exception, string category = null, Object context = null) =>
            _service?.LogException(exception, category, context);
    }
}
