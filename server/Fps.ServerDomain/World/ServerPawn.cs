namespace Fps.ServerDomain.World;

public sealed record ServerPawn(long EntityId, long OwnerPlayerId, string SpawnPointId);
