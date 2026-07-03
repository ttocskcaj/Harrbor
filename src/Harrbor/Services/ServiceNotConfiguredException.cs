namespace Harrbor.Services;

/// <summary>
/// Thrown when an operation requires a connection that has not been configured yet.
/// Normally prevented by the scheduler's pre-check; the scheduler also catches it
/// per cycle for the config-deleted-mid-cycle race. Health checks pre-check the
/// store instead of relying on this exception.
/// </summary>
public class ServiceNotConfiguredException : InvalidOperationException
{
    public string ServiceName { get; }

    public ServiceNotConfiguredException(string serviceName)
        : base($"{serviceName} is not configured")
    {
        ServiceName = serviceName;
    }
}
