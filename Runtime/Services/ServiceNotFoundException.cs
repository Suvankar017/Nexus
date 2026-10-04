using System;

namespace Nexus.Services
{
    /// <summary>
    /// Thrown by <see cref="IServiceRegistry.Get{TService}"/> when no service is registered
    /// for the requested type.
    /// </summary>
    public sealed class ServiceNotFoundException : Exception
    {
        public ServiceNotFoundException(Type serviceType)
            : base($"No service of type '{serviceType.Name}' is registered.")
        {
        }
    }
}
