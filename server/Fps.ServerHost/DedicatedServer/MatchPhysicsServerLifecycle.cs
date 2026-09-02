using Fps.ServerNet.Matches;
using System.Net;
using System.Net.Sockets;

namespace Fps.ServerHost.DedicatedServer;

public sealed class MatchPhysicsServerLifecycle : IMatchPhysicsServerLifecycle
{
    private readonly DedicatedServerProcessManager processManager;
    private readonly string executablePath;
    private readonly string levelId;
    private readonly string contentVersion;
    private readonly TimeSpan startupTimeout;
    private readonly string logRoot;
    private readonly Func<int> allocatePort;

    public MatchPhysicsServerLifecycle(
        DedicatedServerProcessManager processManager,
        string executablePath,
        string levelId,
        string contentVersion,
        TimeSpan startupTimeout,
        string logRoot,
        Func<int>? allocatePort = null)
    {
        this.processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
        this.executablePath = executablePath;
        this.levelId = levelId;
        this.contentVersion = contentVersion;
        this.startupTimeout = startupTimeout;
        this.logRoot = logRoot;
        this.allocatePort = allocatePort ?? AllocateLoopbackPort;
    }

    public async Task<MatchPhysicsServerReady> StartAsync(
        MatchPhysicsServerRequest request,
        CancellationToken cancellationToken)
    {
        int dataPort = allocatePort();
        int healthPort = allocatePort();
        string credential = Convert.ToHexString(Guid.NewGuid().ToByteArray());
        string logPath = Path.Combine(logRoot, $"match-{request.MatchId}.log");
        var launchRequest = new DedicatedServerLaunchRequest(
            executablePath,
            request.MatchId,
            dataPort,
            healthPort,
            credential,
            levelId,
            contentVersion,
            request.Pawns.Select(pawn => new DedicatedAuthorityPawn(
                pawn.PawnId,
                pawn.OwnerPlayerId,
                pawn.PossessionRevision,
                pawn.SpawnPointId,
                pawn.ConnectionId)).ToArray(),
            startupTimeout,
            logPath);

        await processManager.StartAsync(launchRequest, cancellationToken);
        return new MatchPhysicsServerReady(
            request.MatchId,
            $"127.0.0.1:{dataPort}",
            credential);
    }

    public Task StopAsync(long matchId, CancellationToken cancellationToken) =>
        processManager.StopAsync(matchId, cancellationToken);

    public Task<long?> GetAuthorityTickAsync(long matchId, CancellationToken cancellationToken) =>
        processManager.GetAuthorityTickAsync(matchId, cancellationToken);

    public Task<AuthorityFireQueryResult?> QueryFireAsync(
        long matchId,
        AuthorityFireQuery query,
        CancellationToken cancellationToken) =>
        processManager.QueryFireAsync(matchId, query, cancellationToken);

    private static int AllocateLoopbackPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
