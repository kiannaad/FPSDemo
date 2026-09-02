namespace Fps.ServerNet.Matches;

public interface IMatchPhysicsServerLifecycle
{
    Task<MatchPhysicsServerReady> StartAsync(
        MatchPhysicsServerRequest request,
        CancellationToken cancellationToken);

    Task StopAsync(long matchId, CancellationToken cancellationToken);

    Task<long?> GetAuthorityTickAsync(long matchId, CancellationToken cancellationToken) =>
        Task.FromResult<long?>(null);

    Task<AuthorityFireQueryResult?> QueryFireAsync(
        long matchId,
        AuthorityFireQuery query,
        CancellationToken cancellationToken) =>
        Task.FromResult<AuthorityFireQueryResult?>(null);
}

public sealed record AuthorityFireQuery(
    long PawnId,
    float OriginX,
    float OriginY,
    float OriginZ,
    float DirectionX,
    float DirectionY,
    float DirectionZ,
    float Range = 1000f);

public sealed record AuthorityFireQueryResult(
    bool Accepted,
    bool Hit,
    float PositionX,
    float PositionY,
    float PositionZ,
    float NormalX,
    float NormalY,
    float NormalZ,
    string SurfaceId,
    string? Failure);
