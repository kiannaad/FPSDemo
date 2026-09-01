using Fps.ClientNet;
using Fps.Protocol;
using Fps.ServerHost;
using Fps.ServerNet;
using MessagePack;
using NUnit.Framework;

namespace Fps.ClientNet.Tests;

public sealed class RpcClientTests
{
    [Test]
    public async Task Tick_QueuesTransportResponseUntilTheMainThreadDrainsIt()
    {
        using var transport = new FakeClientTransport();
        using var client = new RpcClient(transport);
        transport.Connect("127.0.0.1", 29000, "test");

        Task<RpcResponse> responseTask = client.RequestAsync(MessageId.HelloRequest, Array.Empty<byte>(), TimeSpan.FromSeconds(1));
        transport.EmitResponse(new RpcResponse(
            new PacketHeader(ProtocolVersion.Current, MessageId.HelloResponse, PacketFlags.Response, 1, 0),
            Array.Empty<byte>()));

        Assert.That(responseTask.IsCompleted, Is.False);
        client.Tick();

        Assert.That((await responseTask).Header.RequestId, Is.EqualTo(1));
    }

    [Test]
    public async Task Tick_ConnectedServerHost_CompletesHelloRpc()
    {
        await using var host = new Fps.ServerHost.ServerHost();
        await host.StartAsync(new ServerHostOptions(await WriteContentAsync(), 0, 60));
        using var transport = new LiteNetClientTransport();
        using var client = new RpcClient(transport);

        client.Connect("127.0.0.1", host.Health.Port, LiteNetServerTransport.ConnectionKey);
        await TickUntilCompleteAsync(client, () => client.IsConnected);

        byte[] requestPayload = MessagePackSerializer.Serialize(new HelloRequest("client-rpc", ProtocolVersion.Current));
        Task<RpcResponse> responseTask = client.RequestAsync(MessageId.HelloRequest, requestPayload, TimeSpan.FromSeconds(2));
        await TickUntilCompleteAsync(client, () => responseTask.IsCompleted);
        RpcResponse response = await responseTask;

        Assert.That(response.Header.MessageId, Is.EqualTo(MessageId.HelloResponse));
        Assert.That(MessagePackSerializer.Deserialize<HelloResponse>(response.Payload).ServerVersion, Is.EqualTo("fps-server-v1"));
    }

    [Test]
    public async Task TwoClients_CreateJoinReady_ReceiveAuthoritativeMatchEvents()
    {
        await using var host = new Fps.ServerHost.ServerHost();
        await host.StartAsync(new ServerHostOptions(await WriteTwoSpawnContentAsync(), 0, 60));
        using var first = new RpcClient(new LiteNetClientTransport());
        using var second = new RpcClient(new LiteNetClientTransport());
        var firstEvents = new List<MessageId>();
        var secondEvents = new List<MessageId>();
        first.EventReceived += response => firstEvents.Add(response.Header.MessageId);
        second.EventReceived += response => secondEvents.Add(response.Header.MessageId);
        first.Connect("127.0.0.1", host.Health.Port, LiteNetServerTransport.ConnectionKey);
        second.Connect("127.0.0.1", host.Health.Port, LiteNetServerTransport.ConnectionKey);
        await TickBothUntilCompleteAsync(first, second, () => first.IsConnected && second.IsConnected);

        CreateRoomResponse created = MessagePackSerializer.Deserialize<CreateRoomResponse>((await RequestAsync(
            first, MessageId.CreateRoomRequest, new CreateRoomRequest("room"), first, second)).Payload);
        await RequestAsync(second, MessageId.JoinRoomRequest, new JoinRoomRequest(created.RoomId), first, second);
        await RequestAsync(first, MessageId.SetReadyRequest, new SetReadyRequest(created.RoomId, true), first, second);
        await RequestAsync(second, MessageId.SetReadyRequest, new SetReadyRequest(created.RoomId, true), first, second);
        await TickBothUntilCompleteAsync(first, second, () =>
            firstEvents.Count(message => message == MessageId.PawnSpawned) == 2
            && secondEvents.Count(message => message == MessageId.PawnSpawned) == 2);

        Assert.That(firstEvents.Count(message => message == MessageId.MatchStarting), Is.EqualTo(1));
        Assert.That(secondEvents.Count(message => message == MessageId.MatchStarting), Is.EqualTo(1));
        Assert.That(firstEvents.Count(message => message == MessageId.PossessionChanged), Is.EqualTo(2));
        Assert.That(secondEvents.Count(message => message == MessageId.PossessionChanged), Is.EqualTo(2));
    }

    private static async Task TickUntilCompleteAsync(RpcClient client, Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(3);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            client.Tick();
            await Task.Delay(10);
        }

        Assert.That(condition(), Is.True, "The client did not complete before the deadline.");
    }

    private static async Task<string> WriteContentAsync()
    {
        string contentPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"rpc-level-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(contentPath, """
            {
              "contentVersion": "rpc-test-v1",
              "spawnPoints": [
                { "id": "player-a", "position": [0, 0, 0], "rotation": [0, 0, 0] }
              ]
            }
            """);
        return contentPath;
    }

    private static async Task<string> WriteTwoSpawnContentAsync()
    {
        string contentPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"rpc-two-spawns-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(contentPath, """
            {
              "contentVersion": "rpc-test-v1",
              "spawnPoints": [
                { "id": "player-a", "position": [0, 0, 0], "rotation": [0, 0, 0] },
                { "id": "player-b", "position": [1, 0, 0], "rotation": [0, 0, 0] }
              ]
            }
            """);
        return contentPath;
    }

    private static async Task<RpcResponse> RequestAsync<T>(
        RpcClient client,
        MessageId messageId,
        T payload,
        RpcClient first,
        RpcClient second)
    {
        Task<RpcResponse> responseTask = client.RequestAsync(
            messageId,
            MessagePackSerializer.Serialize(payload),
            TimeSpan.FromSeconds(3));
        await TickBothUntilCompleteAsync(first, second, () => responseTask.IsCompleted);
        return await responseTask;
    }

    private static async Task TickBothUntilCompleteAsync(RpcClient first, RpcClient second, Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(3);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            first.Tick();
            second.Tick();
            await Task.Delay(5);
        }

        Assert.That(condition(), Is.True, "The clients did not complete before the deadline.");
    }

    private sealed class FakeClientTransport : IClientTransport
    {
        public event Action? Connected;

        public event Action<RpcResponse>? ResponseReceived;

        public event Action<string>? Disconnected;

        public bool IsConnected { get; private set; }

        public void Connect(string host, int port, string connectionKey)
        {
            IsConnected = true;
            Connected?.Invoke();
        }

        public void Send(byte[] packet)
        {
        }

        public void PollEvents()
        {
        }

        public void EmitResponse(RpcResponse response)
        {
            ResponseReceived?.Invoke(response);
        }

        public void Dispose()
        {
            IsConnected = false;
            Disconnected?.Invoke("disposed");
        }
    }
}
