namespace Fps.ServerNet.Matches;

public sealed record MatchPhysicsPawn(
    long PawnId,
    long OwnerPlayerId,
    long PossessionRevision,
    string SpawnPointId,
    string ConnectionId);

public sealed record MatchPhysicsServerRequest(
    long MatchId,
    IReadOnlyList<MatchPhysicsPawn> Pawns);

public sealed record MatchPhysicsServerReady(
    long MatchId,
    string DataEndpoint,
    string CredentialId);
