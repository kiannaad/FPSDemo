using Fps.ServerDomain.Players;
using Fps.ServerDomain.World;

namespace Fps.ServerDomain.Matches;

public sealed class ServerMatch
{
    private readonly ServerWorld world;
    private readonly int failCreationAt;

    public ServerMatch(IEnumerable<ServerPlayer> players, IEnumerable<string> spawnPointIds, int failCreationAt = 0)
    {
        Players = (players ?? throw new ArgumentNullException(nameof(players))).ToArray();
        if (Players.Count != 2) throw new ArgumentException("V1 matches require exactly two players.", nameof(players));
        world = new ServerWorld(spawnPointIds);
        this.failCreationAt = failCreationAt;
    }

    public IReadOnlyList<ServerPlayer> Players { get; }
    public IReadOnlyCollection<ServerPawn> Pawns => world.Pawns;
    public int AvailableSpawnPointCount => world.AvailableSpawnPointCount;

    public void Start()
    {
        var createdPawns = new List<ServerPawn>();
        var reservations = new List<string>();
        try
        {
            for (int index = 0; index < Players.Count; index++)
            {
                string spawnPointId = world.ReserveSpawnPoint();
                reservations.Add(spawnPointId);
                ServerPawn pawn = world.CreatePawn(Players[index].PlayerId, spawnPointId, index + 1 == failCreationAt);
                reservations.Remove(spawnPointId);
                createdPawns.Add(pawn);
                Players[index].Possess(pawn.EntityId);
            }
        }
        catch
        {
            for (int index = createdPawns.Count - 1; index >= 0; index--)
            {
                world.DestroyPawn(createdPawns[index]);
            }

            foreach (string reservation in reservations)
            {
                world.ReleaseReservation(reservation);
            }

            foreach (ServerPlayer player in Players) player.Unpossess();
            throw;
        }
    }
}
