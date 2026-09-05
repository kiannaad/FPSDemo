using Fps.Protocol;
using Fps.ServerNet;
using LiteNetLib;
using MessagePack;
using NUnit.Framework;
using System.Net;
using System.Net.Sockets;

namespace Fps.ServerHost.Tests;

public sealed class ServerHostLifecycleTests
{
    [Test]
    public void ParseArguments_MapsExplicitServerOptions()
    {
        ServerHostOptions options = ServerHostCommandLine.Parse(new[]
        {
            "--content", "content.json",
            "--port", "29000",
            "--tick-rate", "60",
            "--health-port", "29001"
        });

        Assert.That(options.ContentPath, Is.EqualTo("content.json"));
        Assert.That(options.Port, Is.EqualTo(29000));
        Assert.That(options.TickRate, Is.EqualTo(60));
        Assert.That(options.HealthPort, Is.EqualTo(29001));
    }

    [Test]
    public void ParseArguments_MapsDedicatedServerOptions()
    {
        ServerHostOptions options = ServerHostCommandLine.Parse(new[]
        {
            "--content", "content.json",
            "--dedicated-executable", "FPSResearchServer.exe",
            "--level-id", "SampleScene",
            "--content-version", "v1",
            "--physics-ready-timeout-ms", "5000",
            "--dedicated-log-root", "logs"
        });

        Assert.That(options.DedicatedExecutablePath, Is.EqualTo("FPSResearchServer.exe"));
        Assert.That(options.LevelId, Is.EqualTo("SampleScene"));
        Assert.That(options.ContentVersion, Is.EqualTo("v1"));
        Assert.That(options.PhysicsReadyTimeout, Is.EqualTo(TimeSpan.FromSeconds(5)));
        Assert.That(options.DedicatedLogRoot, Is.EqualTo("logs"));
    }

    [Test]
    public async Task StartAsync_WithValidContent_ReportsRunningHealth()
    {
        string contentPath = await WriteContentAsync();

        await using var host = new ServerHost();
        await host.StartAsync(new ServerHostOptions(contentPath, 0, 30));

        Assert.That(host.Health.Status, Is.EqualTo(ServerHostStatus.Running));
        Assert.That(host.Health.ContentVersion, Is.EqualTo("test-v1"));
        Assert.That(host.Health.Port, Is.GreaterThan(0));
    }

    [Test]
    public async Task StopAsync_ReleasesTheBoundUdpPort()
    {
        int port;
        using (var reservation = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)))
        {
            port = ((IPEndPoint)reservation.Client.LocalEndPoint!).Port;
        }

        await using var host = new ServerHost();
        await host.StartAsync(new ServerHostOptions(await WriteContentAsync(), port, 30));
        await host.StopAsync();

        using var rebound = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
        Assert.That(host.Health.Status, Is.EqualTo(ServerHostStatus.Stopped));
    }

    [Test]
    public async Task StartAsync_WithDuplicateSpawnIds_RejectsContentBeforeBindingPort()
    {
        int port;
        using (var reservation = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)))
        {
            port = ((IPEndPoint)reservation.Client.LocalEndPoint!).Port;
        }

        string contentPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"invalid-level-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(contentPath, """
            {
              "contentVersion": "test-v1",
              "spawnPoints": [
                { "id": "duplicate", "position": [0, 0, 0], "rotation": [0, 0, 0] },
                { "id": "duplicate", "position": [1, 0, 0], "rotation": [0, 0, 0] }
              ]
            }
            """);

        await using var host = new ServerHost();
        InvalidDataException? exception = null;
        try
        {
            await host.StartAsync(new ServerHostOptions(contentPath, port, 30));
        }
        catch (InvalidDataException caught)
        {
            exception = caught;
        }

        Assert.That(exception, Is.Not.Null);
        Assert.That(exception!.Message, Does.Contain("duplicate"));

        using var rebound = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
        Assert.That(host.Health.Status, Is.EqualTo(ServerHostStatus.Stopped));
    }

    [Test]
    public async Task StartAsync_WithHealthPort_ReturnsRunningHealthResponse()
    {
        await using var host = new ServerHost();
        await host.StartAsync(new ServerHostOptions(await WriteContentAsync(), 0, 30, 0));

        using var client = new HttpClient();
        string response = await client.GetStringAsync($"http://127.0.0.1:{host.Health.HealthPort}/health");

        Assert.That(response, Does.Contain("Running"));
        Assert.That(response, Does.Contain("test-v1"));
    }

    [Test]
    public async Task StartAsync_ConnectedClientReceivesHelloResponse()
    {
        await using var host = new ServerHost();
        await host.StartAsync(new ServerHostOptions(await WriteContentAsync(), 0, 60));

        var clientListener = new EventBasedLiteNetListener();
        var client = new LiteNetManager(clientListener);
        Assert.That(client.Start(), Is.True);
        try
        {
            var responseSource = new TaskCompletionSource<RoutedMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            clientListener.NetworkReceiveEvent += (_, reader, _) =>
            {
                if (PacketCodec.TryDecode(reader.GetRemainingBytes(), out PacketHeader header, out ReadOnlyMemory<byte> payload))
                {
                    responseSource.TrySetResult(new RoutedMessage(header, payload.ToArray()));
                }
            };
            clientListener.PeerConnectedEvent += peer =>
            {
                var header = new PacketHeader(ProtocolVersion.Current, MessageId.HelloRequest, PacketFlags.Request, 7, 0);
                byte[] payload = MessagePackSerializer.Serialize(new HelloRequest("client-host-test", ProtocolVersion.Current));
                peer.Send(PacketCodec.Encode(header, payload), DeliveryMethod.ReliableOrdered);
            };

            client.Connect("127.0.0.1", host.Health.Port, LiteNetServerTransport.ConnectionKey);
            RoutedMessage response = await PollUntilCompleteAsync(client, responseSource.Task);

            Assert.That(response.Header.RequestId, Is.EqualTo(7));
            Assert.That(MessagePackSerializer.Deserialize<HelloResponse>(response.Payload).ServerVersion, Is.EqualTo("fps-server-v1"));
        }
        finally
        {
            client.Stop();
        }
    }

    private static async Task<string> WriteContentAsync()
    {
        string contentPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"level-server-definition-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(contentPath, """
            {
              "contentVersion": "test-v1",
              "spawnPoints": [
                { "id": "player-a", "position": [0, 0, 0], "rotation": [0, 0, 0] }
              ]
            }
            """);
        return contentPath;
    }

    private static async Task<T> PollUntilCompleteAsync<T>(LiteNetManager client, Task<T> task)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(3);
        while (!task.IsCompleted && DateTime.UtcNow < deadline)
        {
            client.PollEvents();
            await Task.Delay(10);
        }

        return await task.WaitAsync(TimeSpan.FromSeconds(1));
    }
}
