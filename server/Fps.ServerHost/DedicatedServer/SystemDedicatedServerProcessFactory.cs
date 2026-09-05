using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Fps.ServerHost.DedicatedServer;

public sealed class SystemDedicatedServerProcessFactory : IDedicatedServerProcessFactory
{
    public ProcessStartInfo CreateStartInfo(DedicatedServerLaunchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        var startInfo = new ProcessStartInfo
        {
            FileName = request.ExecutablePath,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        Add(startInfo, "-batchmode");
        Add(startInfo, "-nographics");
        Add(startInfo, "-logFile", request.LogPath);
        Add(startInfo, "--match-id", request.MatchId.ToString(CultureInfo.InvariantCulture));
        Add(startInfo, "--data-port", request.DataPort.ToString(CultureInfo.InvariantCulture));
        Add(startInfo, "--health-port", request.HealthPort.ToString(CultureInfo.InvariantCulture));
        Add(startInfo, "--credential", request.Credential);
        Add(startInfo, "--level-id", request.LevelId);
        Add(startInfo, "--content-version", request.ContentVersion);
        foreach (DedicatedAuthorityPawn pawn in request.AuthorityPawns)
        {
            string spawnPoint = Convert.ToBase64String(Encoding.UTF8.GetBytes(pawn.SpawnPointId));
            string connectionId = Convert.ToBase64String(Encoding.UTF8.GetBytes(pawn.ConnectionId));
            Add(startInfo, "--authority-pawn", string.Join('|',
                pawn.PawnId.ToString(CultureInfo.InvariantCulture),
                pawn.OwnerPlayerId.ToString(CultureInfo.InvariantCulture),
                pawn.PossessionRevision.ToString(CultureInfo.InvariantCulture),
                spawnPoint,
                connectionId));
        }

        return startInfo;
    }

    public Task<IDedicatedServerProcess> StartAsync(
        DedicatedServerLaunchRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string? logDirectory = Path.GetDirectoryName(Path.GetFullPath(request.LogPath));
        if (!string.IsNullOrEmpty(logDirectory))
        {
            Directory.CreateDirectory(logDirectory);
        }

        Process process = Process.Start(CreateStartInfo(request)) ??
            throw new InvalidOperationException("Dedicated server process could not be started.");
        return Task.FromResult<IDedicatedServerProcess>(new SystemDedicatedServerProcess(process));
    }

    private static void Add(ProcessStartInfo startInfo, params string[] arguments)
    {
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }
    }

    private sealed class SystemDedicatedServerProcess : IDedicatedServerProcess
    {
        private readonly Process process;
        private int disposed;

        public SystemDedicatedServerProcess(Process process)
        {
            this.process = process;
        }

        public bool HasExited => process.HasExited;
        public int? ExitCode => process.HasExited ? process.ExitCode : null;

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            if (process.HasExited)
            {
                return;
            }

            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(cancellationToken);
        }

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0)
            {
                process.Dispose();
            }

            return ValueTask.CompletedTask;
        }
    }
}
