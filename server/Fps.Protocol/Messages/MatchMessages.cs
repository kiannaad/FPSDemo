using MessagePack;

namespace Fps.Protocol;

[MessagePackObject]
public sealed record MatchStartingEvent(
    [property: Key(0)] long MatchId,
    [property: Key(1)] long StartTick,
    [property: Key(2)] string DataEndpoint,
    [property: Key(3)] string CredentialId);

[MessagePackObject]
public sealed record PawnSpawnedEvent(
    [property: Key(0)] long PawnId,
    [property: Key(1)] long OwnerPlayerId,
    [property: Key(2)] string SpawnPointId);

[MessagePackObject]
public sealed record PossessionChangedEvent(
    [property: Key(0)] long PlayerId,
    [property: Key(1)] long PawnId,
    [property: Key(2)] long PossessionRevision);
