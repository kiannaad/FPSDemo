namespace Fps.ServerHost.DedicatedServer;

public interface IDedicatedServerProcess : IAsyncDisposable
{
    bool HasExited { get; }
    int? ExitCode { get; }
    Task StopAsync(CancellationToken cancellationToken = default);
}
