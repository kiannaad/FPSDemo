namespace Fps.ServerNet.Matches;

public interface IMatchPhysicsServerLifecycle
{
    Task<MatchPhysicsServerReady> StartAsync(
        MatchPhysicsServerRequest request,
        CancellationToken cancellationToken);

    Task StopAsync(long matchId, CancellationToken cancellationToken);
}
