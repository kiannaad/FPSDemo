namespace Fps.ServerHost;

public sealed record ServerHostOptions(string ContentPath, int Port, int TickRate, int HealthPort = 0)
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
