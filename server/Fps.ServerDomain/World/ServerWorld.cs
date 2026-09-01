namespace Fps.ServerDomain.World;

public sealed class ServerWorld
{
    private readonly Queue<string> availableSpawnPointIds;
    private readonly HashSet<string> reservedSpawnPointIds = new(StringComparer.Ordinal);
    private readonly Dictionary<long, ServerPawn> pawnsById = new();
    private long nextEntityId;

    public ServerWorld(IEnumerable<string> spawnPointIds)
    {
        availableSpawnPointIds = new Queue<string>((spawnPointIds ?? throw new ArgumentNullException(nameof(spawnPointIds))).Distinct(StringComparer.Ordinal));
        if (availableSpawnPointIds.Count == 0) throw new ArgumentException("At least one spawn point is required.", nameof(spawnPointIds));
    }

    public IReadOnlyCollection<ServerPawn> Pawns => pawnsById.Values;
    public int AvailableSpawnPointCount => availableSpawnPointIds.Count;

    public string ReserveSpawnPoint()
    {
        if (availableSpawnPointIds.Count == 0) throw new InvalidOperationException("No spawn point is available.");
        string spawnPointId = availableSpawnPointIds.Dequeue();
        reservedSpawnPointIds.Add(spawnPointId);
        return spawnPointId;
    }

    public ServerPawn CreatePawn(long ownerPlayerId, string spawnPointId, bool failCreation)
    {
        if (!reservedSpawnPointIds.Contains(spawnPointId)) throw new InvalidOperationException("Spawn point must be reserved before pawn creation.");
        if (failCreation) throw new InvalidOperationException("Configured pawn creation failure.");
        var pawn = new ServerPawn(checked(++nextEntityId), ownerPlayerId, spawnPointId);
        pawnsById.Add(pawn.EntityId, pawn);
        reservedSpawnPointIds.Remove(spawnPointId);
        return pawn;
    }

    public void DestroyPawn(ServerPawn pawn)
    {
        if (pawn == null || !pawnsById.Remove(pawn.EntityId)) return;
        availableSpawnPointIds.Enqueue(pawn.SpawnPointId);
    }

    public void ReleaseReservation(string spawnPointId)
    {
        if (reservedSpawnPointIds.Remove(spawnPointId)) availableSpawnPointIds.Enqueue(spawnPointId);
    }
}
