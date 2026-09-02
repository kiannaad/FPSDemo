using Fps.ServerHost.DedicatedServer;
using NUnit.Framework;
using System.Net;
using System.Net.Sockets;

namespace Fps.ServerHost.Tests;

public sealed class DedicatedServerExecutableIntegrationTests
{
    [Test]
    public async Task BuiltExecutable_ProcessManager_ReachesPhysicsReadyAndReleasesPorts()
    {
        string? executablePath = Environment.GetEnvironmentVariable("FPS_DEDICATED_SERVER_EXE");
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            Assert.Ignore("FPS_DEDICATED_SERVER_EXE is not configured.");
        }

        int dataPort = AllocatePort();
        int healthPort = AllocatePort();
        string logPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "dedicated-server-integration.log");
        using var client = new HttpClient();
        var manager = new DedicatedServerProcessManager(
            new SystemDedicatedServerProcessFactory(),
            new HttpDedicatedServerHealthProbe(client),
            TimeSpan.FromMilliseconds(50));
        var request = new DedicatedServerLaunchRequest(
            executablePath!,
            37,
            dataPort,
            healthPort,
            "integration-credential",
            "SampleScene",
            "v1",
            new[]
            {
                new DedicatedAuthorityPawn(100, 1, 1, "PlayerPoint 1"),
                new DedicatedAuthorityPawn(200, 2, 1, "PlayerPoint 2")
            },
            TimeSpan.FromSeconds(30),
            logPath);

        DedicatedServerLease lease = await manager.StartAsync(request);
        Assert.That(lease.Health.Status, Is.EqualTo(DedicatedServerHealthStatus.PhysicsReady));
        Assert.That(lease.Health.FixedStepCount, Is.GreaterThan(0));
        Assert.That(lease.Health.AuthorityPawnCount, Is.EqualTo(2));

        await lease.DisposeAsync();
        using var dataRebind = new UdpClient(new IPEndPoint(IPAddress.Loopback, dataPort));
        using var healthRebind = new TcpListener(IPAddress.Loopback, healthPort);
        healthRebind.Start();
        Assert.That(manager.ActiveMatchCount, Is.Zero);
    }

    private static int AllocatePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
