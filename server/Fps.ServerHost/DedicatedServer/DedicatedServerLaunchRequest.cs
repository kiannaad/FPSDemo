namespace Fps.ServerHost.DedicatedServer;

public sealed record DedicatedAuthorityPawn(
    long PawnId,
    long OwnerPlayerId,
    long PossessionRevision,
    string SpawnPointId,
    string ConnectionId = "integration-client");

public sealed record DedicatedServerLaunchRequest(
    string ExecutablePath,
    long MatchId,
    int DataPort,
    int HealthPort,
    string Credential,
    string LevelId,
    string ContentVersion,
    IReadOnlyList<DedicatedAuthorityPawn> AuthorityPawns,
    TimeSpan StartupTimeout,
    string LogPath)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ExecutablePath)) throw new ArgumentException("Dedicated server executable path is required.", nameof(ExecutablePath));
        if (MatchId <= 0) throw new ArgumentOutOfRangeException(nameof(MatchId));
        if (DataPort is <= 0 or > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(DataPort));
        if (HealthPort is <= 0 or > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(HealthPort));
        if (string.IsNullOrWhiteSpace(Credential)) throw new ArgumentException("Dedicated server credential is required.", nameof(Credential));
        if (string.IsNullOrWhiteSpace(LevelId)) throw new ArgumentException("Dedicated server level id is required.", nameof(LevelId));
        if (string.IsNullOrWhiteSpace(ContentVersion)) throw new ArgumentException("Dedicated server content version is required.", nameof(ContentVersion));
        if (AuthorityPawns is null || AuthorityPawns.Count == 0) throw new ArgumentException("At least one Authority pawn is required.", nameof(AuthorityPawns));
        if (StartupTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(StartupTimeout));
        if (string.IsNullOrWhiteSpace(LogPath)) throw new ArgumentException("Dedicated server log path is required.", nameof(LogPath));
    }
}
