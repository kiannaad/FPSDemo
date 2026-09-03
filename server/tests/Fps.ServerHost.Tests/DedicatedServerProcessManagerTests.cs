using Fps.ServerHost.DedicatedServer;
using Fps.ServerNet.Matches;
using NUnit.Framework;
using System.Net;
using System.Text;

namespace Fps.ServerHost.Tests;

public sealed class DedicatedServerProcessManagerTests
{
    [Test]
    public void SystemProcessFactory_CreateStartInfo_UsesStructuredNoShellArguments()
    {
        var factory = new SystemDedicatedServerProcessFactory();

        System.Diagnostics.ProcessStartInfo startInfo = factory.CreateStartInfo(CreateRequest());

        Assert.That(startInfo.UseShellExecute, Is.False);
        Assert.That(startInfo.ArgumentList, Does.Contain("-batchmode"));
        Assert.That(startInfo.ArgumentList, Does.Contain("-nographics"));
        Assert.That(startInfo.ArgumentList, Does.Contain("--match-id"));
        Assert.That(startInfo.ArgumentList, Does.Contain("17"));
        Assert.That(startInfo.ArgumentList, Does.Contain("--authority-pawn"));
    }

    [Test]
    public async Task HttpHealthProbe_WhenResponseIsSuccessful_DeserializesSnapshot()
    {
        const string json = """
            {"status":"PhysicsReady","matchId":17,"dataPort":31000,"healthPort":31001,"levelId":"SampleScene","contentVersion":"v1","authorityPawnCount":2,"fixedStepCount":4,"failure":null}
            """;
        using var client = new HttpClient(new StaticHttpHandler(HttpStatusCode.OK, json));
        var probe = new HttpDedicatedServerHealthProbe(client);

        DedicatedServerHealth? health = await probe.ProbeAsync(
            new Uri("http://127.0.0.1:31001/health"),
            CancellationToken.None);

        Assert.That(health, Is.Not.Null);
        Assert.That(health!.Status, Is.EqualTo(DedicatedServerHealthStatus.PhysicsReady));
        Assert.That(health.FixedStepCount, Is.EqualTo(4));
    }

    [Test]
    public async Task HttpHealthProbe_WhenEndpointIsNotListening_ReturnsNotReady()
    {
        using var client = new HttpClient(new ThrowingHttpHandler());
        var probe = new HttpDedicatedServerHealthProbe(client);

        DedicatedServerHealth? health = await probe.ProbeAsync(
            new Uri("http://127.0.0.1:31001/health"),
            CancellationToken.None);

        Assert.That(health, Is.Null);
    }

    [Test]
    public async Task StartAsync_WhenHealthMatchesRequest_ReturnsActiveLease()
    {
        var process = new RecordingDedicatedServerProcess();
        var manager = new DedicatedServerProcessManager(
            new SingleProcessFactory(process),
            new ReadyHealthProbe(new DedicatedServerHealth(
                DedicatedServerHealthStatus.PhysicsReady,
                17,
                31000,
                31001,
                "SampleScene",
                "v1",
                2,
                1,
                null)));

        DedicatedServerLease lease = await manager.StartAsync(CreateRequest());

        Assert.That(lease.MatchId, Is.EqualTo(17));
        Assert.That(lease.Health.Status, Is.EqualTo(DedicatedServerHealthStatus.PhysicsReady));
        Assert.That(manager.ActiveMatchCount, Is.EqualTo(1));
    }

    [Test]
    public async Task StartAsync_WhenContentVersionDiffers_StopsProcessAndRejectsLease()
    {
        var process = new RecordingDedicatedServerProcess();
        var manager = new DedicatedServerProcessManager(
            new SingleProcessFactory(process),
            new ReadyHealthProbe(new DedicatedServerHealth(
                DedicatedServerHealthStatus.PhysicsReady,
                17,
                31000,
                31001,
                "SampleScene",
                "wrong-version",
                2,
                1,
                null)));

        InvalidDataException? exception = null;
        try
        {
            await manager.StartAsync(CreateRequest());
        }
        catch (InvalidDataException caught)
        {
            exception = caught;
        }

        Assert.That(exception, Is.Not.Null);
        Assert.That(process.StopCount, Is.EqualTo(1));
        Assert.That(process.DisposeCount, Is.EqualTo(1));
        Assert.That(manager.ActiveMatchCount, Is.Zero);
    }

    [Test]
    public async Task StartAsync_WhenProcessFactoryThrows_DoesNotRetainActiveMatch()
    {
        var manager = new DedicatedServerProcessManager(
            new ThrowingProcessFactory(),
            new UnusedHealthProbe());

        InvalidOperationException? exception = null;
        try
        {
            await manager.StartAsync(CreateRequest());
        }
        catch (InvalidOperationException caught)
        {
            exception = caught;
        }

        Assert.That(exception, Is.Not.Null);
        Assert.That(manager.ActiveMatchCount, Is.Zero);
    }

    [Test]
    public async Task StartAsync_WhenHealthStartsBeforeReady_PollsUntilPhysicsReady()
    {
        var process = new RecordingDedicatedServerProcess();
        var healthProbe = new SequenceHealthProbe(
            new DedicatedServerHealth(
                DedicatedServerHealthStatus.Starting,
                17,
                31000,
                31001,
                "SampleScene",
                "v1",
                0,
                0,
                null),
            new DedicatedServerHealth(
                DedicatedServerHealthStatus.PhysicsReady,
                17,
                31000,
                31001,
                "SampleScene",
                "v1",
                2,
                1,
                null));
        var manager = new DedicatedServerProcessManager(
            new SingleProcessFactory(process),
            healthProbe);

        DedicatedServerLease lease = await manager.StartAsync(CreateRequest());

        Assert.That(lease.Health.Status, Is.EqualTo(DedicatedServerHealthStatus.PhysicsReady));
        Assert.That(healthProbe.ProbeCount, Is.EqualTo(2));
    }

    [Test]
    public async Task StartAsync_WhenPhysicsReadyTimesOut_StopsProcessAndRejectsLease()
    {
        var process = new RecordingDedicatedServerProcess();
        var manager = new DedicatedServerProcessManager(
            new SingleProcessFactory(process),
            new ReadyHealthProbe(new DedicatedServerHealth(
                DedicatedServerHealthStatus.Starting,
                17,
                31000,
                31001,
                "SampleScene",
                "v1",
                0,
                0,
                null)),
            TimeSpan.FromMilliseconds(1));

        TimeoutException? exception = null;
        try
        {
            await manager.StartAsync(CreateRequest(TimeSpan.FromMilliseconds(10)));
        }
        catch (TimeoutException caught)
        {
            exception = caught;
        }

        Assert.That(exception, Is.Not.Null);
        Assert.That(process.StopCount, Is.EqualTo(1));
        Assert.That(process.DisposeCount, Is.EqualTo(1));
        Assert.That(manager.ActiveMatchCount, Is.Zero);
    }

    [Test]
    public async Task StartAsync_WhenProcessExitsBeforePhysicsReady_RejectsLease()
    {
        var process = new RecordingDedicatedServerProcess(hasExited: true, exitCode: 23);
        var manager = new DedicatedServerProcessManager(
            new SingleProcessFactory(process),
            new ReadyHealthProbe(null),
            TimeSpan.FromMilliseconds(1));

        InvalidOperationException? exception = null;
        try
        {
            await manager.StartAsync(CreateRequest());
        }
        catch (InvalidOperationException caught)
        {
            exception = caught;
        }

        Assert.That(exception, Is.Not.Null);
        Assert.That(exception!.Message, Does.Contain("23"));
        Assert.That(process.StopCount, Is.EqualTo(1));
        Assert.That(process.DisposeCount, Is.EqualTo(1));
        Assert.That(manager.ActiveMatchCount, Is.Zero);
    }

    [Test]
    public async Task StopAsync_WhenCalledTwice_ReclaimsActiveMatchOnce()
    {
        var process = new RecordingDedicatedServerProcess();
        var manager = new DedicatedServerProcessManager(
            new SingleProcessFactory(process),
            new ReadyHealthProbe(new DedicatedServerHealth(
                DedicatedServerHealthStatus.PhysicsReady,
                17,
                31000,
                31001,
                "SampleScene",
                "v1",
                2,
                1,
                null)));
        await manager.StartAsync(CreateRequest());

        await manager.StopAsync(17);
        await manager.StopAsync(17);

        Assert.That(process.StopCount, Is.EqualTo(1));
        Assert.That(process.DisposeCount, Is.EqualTo(1));
        Assert.That(manager.ActiveMatchCount, Is.Zero);
    }

    [Test]
    public async Task LeaseDisposeAsync_ReclaimsMatchThroughManager()
    {
        var process = new RecordingDedicatedServerProcess();
        var manager = new DedicatedServerProcessManager(
            new SingleProcessFactory(process),
            new ReadyHealthProbe(new DedicatedServerHealth(
                DedicatedServerHealthStatus.PhysicsReady,
                17,
                31000,
                31001,
                "SampleScene",
                "v1",
                2,
                1,
                null)));
        DedicatedServerLease lease = await manager.StartAsync(CreateRequest());

        await lease.DisposeAsync();

        Assert.That(process.StopCount, Is.EqualTo(1));
        Assert.That(process.DisposeCount, Is.EqualTo(1));
        Assert.That(manager.ActiveMatchCount, Is.Zero);
    }

    [Test]
    public void MatchPhysicsServerLifecycle_WhenDedicatedRosterIsMissing_StopsAndRejectsMatch()
    {
        var process = new RecordingDedicatedServerProcess();
        var manager = new DedicatedServerProcessManager(
            new SingleProcessFactory(process),
            new ReadyHealthProbe(new DedicatedServerHealth(
                DedicatedServerHealthStatus.PhysicsReady,
                17,
                31000,
                31001,
                "SampleScene",
                "v1",
                2,
                1,
                null)));
        var lifecycle = new MatchPhysicsServerLifecycle(
            manager,
            "dedicated-server.exe",
            "SampleScene",
            "v1",
            TimeSpan.FromSeconds(1),
            "dedicated-logs",
            new Queue<int>(new[] { 31000, 31001 }).Dequeue);

        Func<Task> startMatch = () => lifecycle.StartAsync(
            new MatchPhysicsServerRequest(17, new[]
            {
                new MatchPhysicsPawn(100, 1, 1, "PlayerPoint 1", "connection-a"),
                new MatchPhysicsPawn(200, 2, 1, "PlayerPoint 2", "connection-b")
            }),
            CancellationToken.None);
        Assert.ThrowsAsync<InvalidDataException>(startMatch);
        Assert.That(process.StopCount, Is.EqualTo(1));
        Assert.That(manager.ActiveMatchCount, Is.Zero);
    }

    [Test]
    public async Task MatchPhysicsServerLifecycle_WithThreeDedicatedTargets_ReturnsVerifiedRoster()
    {
        string[] targetIds = { "EnemyPoint 1", "EnemyPoint 2", "EnemyPoint 3" };
        var process = new RecordingDedicatedServerProcess();
        var manager = new DedicatedServerProcessManager(
            new SingleProcessFactory(process),
            new ReadyHealthProbe(new DedicatedServerHealth(
                DedicatedServerHealthStatus.PhysicsReady,
                17,
                31000,
                31001,
                "SampleScene",
                "v1",
                2,
                1,
                null,
                targetIds)));
        var lifecycle = new MatchPhysicsServerLifecycle(
            manager,
            "dedicated-server.exe",
            "SampleScene",
            "v1",
            TimeSpan.FromSeconds(1),
            "dedicated-logs",
            new Queue<int>(new[] { 31000, 31001 }).Dequeue);

        MatchPhysicsServerReady ready = await lifecycle.StartAsync(
            new MatchPhysicsServerRequest(17, new[]
            {
                new MatchPhysicsPawn(100, 1, 1, "PlayerPoint 1", "connection-a"),
                new MatchPhysicsPawn(200, 2, 1, "PlayerPoint 2", "connection-b")
            }),
            CancellationToken.None);

        Assert.That(ready.TargetIds, Is.EqualTo(targetIds));
        await lifecycle.StopAsync(17, CancellationToken.None);
        Assert.That(process.StopCount, Is.EqualTo(1));
    }

    private static DedicatedServerLaunchRequest CreateRequest(TimeSpan? readyTimeout = null) => new(
        "dedicated-server.exe",
        17,
        31000,
        31001,
        "credential-17",
        "SampleScene",
        "v1",
        new[]
        {
            new DedicatedAuthorityPawn(100, 1, 1, "PlayerPoint 1"),
            new DedicatedAuthorityPawn(200, 2, 1, "PlayerPoint 2")
        },
        readyTimeout ?? TimeSpan.FromSeconds(1),
        "dedicated-17.log");

    private sealed class ThrowingProcessFactory : IDedicatedServerProcessFactory
    {
        public Task<IDedicatedServerProcess> StartAsync(
            DedicatedServerLaunchRequest request,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("process start failed");
        }
    }

    private sealed class SingleProcessFactory : IDedicatedServerProcessFactory
    {
        private readonly IDedicatedServerProcess process;

        public SingleProcessFactory(IDedicatedServerProcess process)
        {
            this.process = process;
        }

        public Task<IDedicatedServerProcess> StartAsync(
            DedicatedServerLaunchRequest request,
            CancellationToken cancellationToken) => Task.FromResult(process);
    }

    private sealed class ReadyHealthProbe : IDedicatedServerHealthProbe
    {
        private readonly DedicatedServerHealth? health;

        public ReadyHealthProbe(DedicatedServerHealth? health)
        {
            this.health = health;
        }

        public Task<DedicatedServerHealth?> ProbeAsync(Uri endpoint, CancellationToken cancellationToken) =>
            Task.FromResult<DedicatedServerHealth?>(health);
    }

    private sealed class SequenceHealthProbe : IDedicatedServerHealthProbe
    {
        private readonly Queue<DedicatedServerHealth?> healthSequence;

        public SequenceHealthProbe(params DedicatedServerHealth?[] healthSequence)
        {
            this.healthSequence = new Queue<DedicatedServerHealth?>(healthSequence);
        }

        public int ProbeCount { get; private set; }

        public Task<DedicatedServerHealth?> ProbeAsync(Uri endpoint, CancellationToken cancellationToken)
        {
            ProbeCount++;
            return Task.FromResult(healthSequence.Dequeue());
        }
    }

    private sealed class RecordingDedicatedServerProcess : IDedicatedServerProcess
    {
        private readonly bool hasExited;
        private readonly int? exitCode;

        public RecordingDedicatedServerProcess(bool hasExited = false, int? exitCode = null)
        {
            this.hasExited = hasExited;
            this.exitCode = exitCode;
        }

        public int StopCount { get; private set; }
        public int DisposeCount { get; private set; }
        public bool HasExited => hasExited;
        public int? ExitCode => exitCode;

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            StopCount++;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class UnusedHealthProbe : IDedicatedServerHealthProbe
    {
        public Task<DedicatedServerHealth?> ProbeAsync(Uri endpoint, CancellationToken cancellationToken) =>
            throw new AssertionException("Health must not be probed after process start failed.");
    }

    private sealed class StaticHttpHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode statusCode;
        private readonly string content;

        public StaticHttpHandler(HttpStatusCode statusCode, string content)
        {
            this.statusCode = statusCode;
            this.content = content;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            });
    }

    private sealed class ThrowingHttpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => throw new HttpRequestException("connection refused");
    }
}
