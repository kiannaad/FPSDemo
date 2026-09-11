namespace Fps.ServerHost.DedicatedServer;

public enum DedicatedServerHealthStatus
{
    Starting,
    PhysicsReady,
    Failed
}

public sealed record DedicatedServerHealth(
    DedicatedServerHealthStatus Status,
    long MatchId,
    int DataPort,
    int HealthPort,
    string LevelId,
    string ContentVersion,
    int AuthorityPawnCount,
    long FixedStepCount,
    string? Failure,
    IReadOnlyList<string>? TargetIds = null,
    IReadOnlyList<DedicatedEnemyHealth>? EnemySpawns = null);

public sealed record DedicatedEnemyHealth(long EnemyId, string ArchetypeId, int Health);
