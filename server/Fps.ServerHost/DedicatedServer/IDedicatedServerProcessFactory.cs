namespace Fps.ServerHost.DedicatedServer;

public interface IDedicatedServerProcessFactory
{
    Task<IDedicatedServerProcess> StartAsync(
        DedicatedServerLaunchRequest request,
        CancellationToken cancellationToken);
}
