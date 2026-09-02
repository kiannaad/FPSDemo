namespace Fps.ServerHost.DedicatedServer;

public sealed class DedicatedServerProcessManager
{
    private readonly IDedicatedServerProcessFactory processFactory;
    private readonly IDedicatedServerHealthProbe healthProbe;
    private readonly TimeSpan healthPollInterval;
    private readonly HashSet<long> startingMatchIds = new();
    private readonly Dictionary<long, DedicatedServerLease> activeLeasesByMatchId = new();
    private readonly object sync = new();

    public DedicatedServerProcessManager(
        IDedicatedServerProcessFactory processFactory,
        IDedicatedServerHealthProbe healthProbe,
        TimeSpan? healthPollInterval = null)
    {
        this.processFactory = processFactory ?? throw new ArgumentNullException(nameof(processFactory));
        this.healthProbe = healthProbe ?? throw new ArgumentNullException(nameof(healthProbe));
        this.healthPollInterval = healthPollInterval ?? TimeSpan.FromMilliseconds(100);
    }

    public int ActiveMatchCount
    {
        get
        {
            lock (sync)
            {
                return startingMatchIds.Count + activeLeasesByMatchId.Count;
            }
        }
    }

    public async Task<DedicatedServerLease> StartAsync(
        DedicatedServerLaunchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        lock (sync)
        {
            if (!startingMatchIds.Add(request.MatchId) || activeLeasesByMatchId.ContainsKey(request.MatchId))
            {
                throw new InvalidOperationException($"Dedicated server match {request.MatchId} is already starting or active.");
            }
        }

        IDedicatedServerProcess? process = null;
        using var readyTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        readyTimeout.CancelAfter(request.StartupTimeout);
        CancellationToken startupToken = readyTimeout.Token;
        try
        {
            process = await processFactory.StartAsync(request, startupToken);
            var endpoint = new Uri($"http://127.0.0.1:{request.HealthPort}/health");
            DedicatedServerHealth? health;
            do
            {
                if (process.HasExited)
                {
                    throw new InvalidOperationException(
                        $"Dedicated server match {request.MatchId} exited before PhysicsReady with exit code {process.ExitCode?.ToString() ?? "unknown"}.");
                }

                health = await healthProbe.ProbeAsync(endpoint, startupToken);
                if (health?.Status != DedicatedServerHealthStatus.PhysicsReady)
                {
                    await Task.Delay(healthPollInterval, startupToken);
                }
            }
            while (health?.Status != DedicatedServerHealthStatus.PhysicsReady);

            ValidateReadyHealth(request, health);
            var lease = new DedicatedServerLease(
                request.MatchId,
                health!,
                process,
                () => new ValueTask(StopAsync(request.MatchId)));
            lock (sync)
            {
                startingMatchIds.Remove(request.MatchId);
                activeLeasesByMatchId.Add(request.MatchId, lease);
            }
            return lease;
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            await StopAndDisposeAsync(process);
            RemoveStartingMatch(request.MatchId);
            throw new TimeoutException(
                $"Dedicated server match {request.MatchId} did not report PhysicsReady within {request.StartupTimeout}.",
                exception);
        }
        catch
        {
            await StopAndDisposeAsync(process);
            RemoveStartingMatch(request.MatchId);
            throw;
        }
    }

    public async Task StopAsync(long matchId, CancellationToken cancellationToken = default)
    {
        DedicatedServerLease? lease;
        lock (sync)
        {
            if (!activeLeasesByMatchId.Remove(matchId, out lease))
            {
                return;
            }
        }

        try
        {
            await lease.Process.StopAsync(cancellationToken);
        }
        finally
        {
            await lease.Process.DisposeAsync();
        }
    }

    public async Task<long?> GetAuthorityTickAsync(long matchId, CancellationToken cancellationToken = default)
    {
        DedicatedServerLease? lease;
        lock (sync)
        {
            activeLeasesByMatchId.TryGetValue(matchId, out lease);
        }

        if (lease == null) return null;
        var endpoint = new Uri($"http://127.0.0.1:{lease.Health.HealthPort}/health");
        DedicatedServerHealth? health = await healthProbe.ProbeAsync(endpoint, cancellationToken);
        if (health?.Status != DedicatedServerHealthStatus.PhysicsReady || health.MatchId != matchId)
            return null;
        return health.FixedStepCount;
    }

    private async Task StopAndDisposeAsync(IDedicatedServerProcess? process)
    {
        if (process == null)
        {
            return;
        }

        try
        {
            await process.StopAsync(CancellationToken.None);
        }
        finally
        {
            await process.DisposeAsync();
        }
    }

    private void RemoveStartingMatch(long matchId)
    {
        lock (sync)
        {
            startingMatchIds.Remove(matchId);
        }
    }

    private static void ValidateReadyHealth(
        DedicatedServerLaunchRequest request,
        DedicatedServerHealth? health)
    {
        if (health == null || health.Status != DedicatedServerHealthStatus.PhysicsReady)
        {
            throw new InvalidOperationException("Dedicated server did not report PhysicsReady.");
        }

        if (health.MatchId != request.MatchId ||
            health.DataPort != request.DataPort ||
            health.HealthPort != request.HealthPort ||
            !string.Equals(health.LevelId, request.LevelId, StringComparison.Ordinal) ||
            !string.Equals(health.ContentVersion, request.ContentVersion, StringComparison.Ordinal) ||
            health.AuthorityPawnCount != request.AuthorityPawns.Count ||
            health.FixedStepCount <= 0)
        {
            throw new InvalidDataException("Dedicated server PhysicsReady health does not match the launch request.");
        }
    }
}
