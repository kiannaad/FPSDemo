namespace Fps.ServerHost.DedicatedServer;

public interface IDedicatedServerHealthProbe
{
    Task<DedicatedServerHealth?> ProbeAsync(Uri endpoint, CancellationToken cancellationToken);
}
