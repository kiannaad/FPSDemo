namespace Fps.ServerHost;

public sealed record ServerHostOptions(
    string ContentPath,
    int Port,
    int TickRate,
    int HealthPort = 0,
    string? DedicatedExecutablePath = null,
    string? LevelId = null,
    string? ContentVersion = null,
    TimeSpan? PhysicsReadyTimeout = null,
    string? DedicatedLogRoot = null)
{
    public void Validate()
    {
        if (Port is < 0 or > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(Port));
        }

        if (TickRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(TickRate));
        }

        if (HealthPort is < 0 or > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(HealthPort));
        }

        if (!string.IsNullOrWhiteSpace(DedicatedExecutablePath))
        {
            if (string.IsNullOrWhiteSpace(LevelId)) throw new ArgumentException("Level id is required for Dedicated Server.", nameof(LevelId));
            if (string.IsNullOrWhiteSpace(ContentVersion)) throw new ArgumentException("Content version is required for Dedicated Server.", nameof(ContentVersion));
            if (PhysicsReadyTimeout is null || PhysicsReadyTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(PhysicsReadyTimeout));
            if (string.IsNullOrWhiteSpace(DedicatedLogRoot)) throw new ArgumentException("Dedicated log root is required.", nameof(DedicatedLogRoot));
        }
    }
}

public enum ServerHostStatus
{
    Stopped,
    Starting,
    Running,
    Stopping
}

public sealed record ServerHostHealth(ServerHostStatus Status, int Port, int HealthPort, string? ContentVersion);
