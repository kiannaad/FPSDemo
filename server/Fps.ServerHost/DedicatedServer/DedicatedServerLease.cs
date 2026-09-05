namespace Fps.ServerHost.DedicatedServer;

public sealed class DedicatedServerLease : IAsyncDisposable
{
    private readonly Func<ValueTask> releaseAsync;

    internal DedicatedServerLease(
        long matchId,
        DedicatedServerHealth health,
        IDedicatedServerProcess process,
        Func<ValueTask> releaseAsync)
    {
        MatchId = matchId;
        Health = health;
        Process = process;
        this.releaseAsync = releaseAsync;
    }

    public long MatchId { get; }
    public DedicatedServerHealth Health { get; }
    internal IDedicatedServerProcess Process { get; }

    public ValueTask DisposeAsync() => releaseAsync();
}
