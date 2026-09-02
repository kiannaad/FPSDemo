using MessagePack;
using NUnit.Framework;
using Fps.Protocol;
using Fps.ServerNet;

namespace Fps.ServerNet.Tests;

public sealed class MessageRouterTests
{
    [Test]
    public async Task SecondReady_PhysicsFailure_ResetsRoomAndPublishesNoMatchEvents()
    {
        var lifecycle = new RejectingMatchPhysicsServerLifecycle();
        var router = new MessageRouter("server-037", new[] { "spawn-a", "spawn-b" }, lifecycle);
        string roomId = MessagePackSerializer.Deserialize<CreateRoomResponse>(
            (await router.RouteAsync("connection-a", Request(1, MessageId.CreateRoomRequest),
                MessagePackSerializer.Serialize(new CreateRoomRequest("room"))))[0].Message.Payload).RoomId;
        await router.RouteAsync("connection-b", Request(2, MessageId.JoinRoomRequest),
            MessagePackSerializer.Serialize(new JoinRoomRequest(roomId)));
        await router.RouteAsync("connection-a", Request(3, MessageId.SetReadyRequest),
            MessagePackSerializer.Serialize(new SetReadyRequest(roomId, true)));

        IReadOnlyList<RoutedOutboundMessage> messages = await router.RouteAsync(
            "connection-b",
            Request(4, MessageId.SetReadyRequest),
            MessagePackSerializer.Serialize(new SetReadyRequest(roomId, true)));

        Assert.That(router.GetRoomState(roomId), Is.EqualTo(Fps.ServerDomain.Rooms.ServerRoomState.Waiting));
        Assert.That(lifecycle.StopCount, Is.EqualTo(1));
        Assert.That(messages.Any(message => message.Message.Header.MessageId == MessageId.MatchStarting), Is.False);
    }

    [Test]
    public async Task SecondReady_BeforePhysicsReady_DoesNotCompleteOrPublishMatchStarting()
    {
        var lifecycle = new DelayedMatchPhysicsServerLifecycle();
        var router = new MessageRouter("server-037", new[] { "spawn-a", "spawn-b" }, lifecycle);
        string roomId = MessagePackSerializer.Deserialize<CreateRoomResponse>(
            (await router.RouteAsync("connection-a", Request(1, MessageId.CreateRoomRequest),
                MessagePackSerializer.Serialize(new CreateRoomRequest("room"))))[0].Message.Payload).RoomId;
        await router.RouteAsync("connection-b", Request(2, MessageId.JoinRoomRequest),
            MessagePackSerializer.Serialize(new JoinRoomRequest(roomId)));
        await router.RouteAsync("connection-a", Request(3, MessageId.SetReadyRequest),
            MessagePackSerializer.Serialize(new SetReadyRequest(roomId, true)));

        Task<IReadOnlyList<RoutedOutboundMessage>> pending = router.RouteAsync(
            "connection-b",
            Request(4, MessageId.SetReadyRequest),
            MessagePackSerializer.Serialize(new SetReadyRequest(roomId, true)));
        await Task.Yield();

        Assert.That(pending.IsCompleted, Is.False);
        Assert.That(router.GetRoomState(roomId), Is.EqualTo(Fps.ServerDomain.Rooms.ServerRoomState.Starting));

        lifecycle.PublishReady();
        IReadOnlyList<RoutedOutboundMessage> messages = await pending;
        Assert.That(messages.Count(message => message.Message.Header.MessageId == MessageId.MatchStarting), Is.EqualTo(2));
        Assert.That(router.GetRoomState(roomId), Is.EqualTo(Fps.ServerDomain.Rooms.ServerRoomState.Started));
    }

    [Test]
    public void TwoReadyConnections_ReceiveAuthoritativeSpawnAndPossessionEvents()
    {
        var router = new MessageRouter("server-034", new[] { "spawn-a", "spawn-b" });
        string roomId = MessagePackSerializer.Deserialize<CreateRoomResponse>(
            router.Route("connection-a", Request(1, MessageId.CreateRoomRequest),
                MessagePackSerializer.Serialize(new CreateRoomRequest("room")))[0].Message.Payload).RoomId;

        router.Route("connection-b", Request(2, MessageId.JoinRoomRequest),
            MessagePackSerializer.Serialize(new JoinRoomRequest(roomId)));
        router.Route("connection-a", Request(3, MessageId.SetReadyRequest),
            MessagePackSerializer.Serialize(new SetReadyRequest(roomId, true)));
        IReadOnlyList<RoutedOutboundMessage> messages = router.Route("connection-b", Request(4, MessageId.SetReadyRequest),
            MessagePackSerializer.Serialize(new SetReadyRequest(roomId, true)));

        Assert.That(messages.Count(message => message.Message.Header.MessageId == MessageId.PawnSpawned), Is.EqualTo(4));
        Assert.That(messages.Count(message => message.Message.Header.MessageId == MessageId.PossessionChanged), Is.EqualTo(4));
        Assert.That(messages.Count(message => message.Message.Header.MessageId == MessageId.MatchStarting), Is.EqualTo(2));

        RoutedOutboundMessage startingA = messages.Single(message =>
            message.ConnectionId == "connection-a" &&
            message.Message.Header.MessageId == MessageId.MatchStarting);
        RoutedOutboundMessage startingB = messages.Single(message =>
            message.ConnectionId == "connection-b" &&
            message.Message.Header.MessageId == MessageId.MatchStarting);
        string credentialA = MessagePackSerializer.Deserialize<MatchStartingEvent>(startingA.Message.Payload).CredentialId;
        string credentialB = MessagePackSerializer.Deserialize<MatchStartingEvent>(startingB.Message.Payload).CredentialId;
        Assert.That(credentialA, Does.StartWith("development:"));
        Assert.That(credentialB, Does.StartWith("development:"));
        Assert.That(credentialA, Is.Not.EqualTo(credentialB));
    }

    [Test]
    public void RouteHelloRequest_ReturnsMatchingResponseHeaderAndPayload()
    {
        var router = new MessageRouter("server-033");
        var requestHeader = new PacketHeader(
            ProtocolVersion.Current,
            MessageId.HelloRequest,
            PacketFlags.Request,
            RequestId: 44,
            MatchId: 0);
        byte[] requestPayload = MessagePackSerializer.Serialize(new HelloRequest("client-033", ProtocolVersion.Current));

        RoutedMessage response = router.Route(requestHeader, requestPayload);

        Assert.That(response.Header.MessageId, Is.EqualTo(MessageId.HelloResponse));
        Assert.That(response.Header.Flags, Is.EqualTo(PacketFlags.Response));
        Assert.That(response.Header.RequestId, Is.EqualTo(44));
        Assert.That(MessagePackSerializer.Deserialize<HelloResponse>(response.Payload).ServerVersion, Is.EqualTo("server-033"));
    }

    [Test]
    public void AnimationRequests_ForFourKinds_AreAuthoritySequencedAndReloadCommitsAtTick()
    {
        var router = new MessageRouter("server-042", new[] { "spawn-a", "spawn-b" });
        string roomId = MessagePackSerializer.Deserialize<CreateRoomResponse>(
            router.Route("connection-a", Request(1, MessageId.CreateRoomRequest),
                MessagePackSerializer.Serialize(new CreateRoomRequest("room")))[0].Message.Payload).RoomId;
        router.Route("connection-b", Request(2, MessageId.JoinRoomRequest),
            MessagePackSerializer.Serialize(new JoinRoomRequest(roomId)));
        router.Route("connection-a", Request(3, MessageId.SetReadyRequest),
            MessagePackSerializer.Serialize(new SetReadyRequest(roomId, true)));
        IReadOnlyList<RoutedOutboundMessage> starting = router.Route(
            "connection-b", Request(4, MessageId.SetReadyRequest),
            MessagePackSerializer.Serialize(new SetReadyRequest(roomId, true)));
        PossessionChangedEvent possession = starting
            .Where(message => message.ConnectionId == "connection-a" && message.Message.Header.MessageId == MessageId.PossessionChanged)
            .Select(message => MessagePackSerializer.Deserialize<PossessionChangedEvent>(message.Message.Payload))
            .Single(value => value.PlayerId == 1);

        long expectedSequence = 0;
        var terminal = new List<RoutedOutboundMessage>();
        foreach (NetworkAnimationActionKind kind in new[]
                 {
                     NetworkAnimationActionKind.Equip,
                     NetworkAnimationActionKind.Melee,
                     NetworkAnimationActionKind.Reload,
                     NetworkAnimationActionKind.Unequip
                 })
        {
            if (kind == NetworkAnimationActionKind.Unequip)
                terminal.AddRange(router.AdvanceAnimationActions(1, 270));
            var request = new NetworkAnimationActionRequestMessage(
                possession.PawnId, possession.PossessionRevision, (long)kind,
                kind, kind.ToString(), 9,
                kind == NetworkAnimationActionKind.Reload ? 270 : 0,
                kind == NetworkAnimationActionKind.Reload ? 140 : null);
            var header = new PacketHeader(
                ProtocolVersion.Current, MessageId.AnimationActionRequest,
                PacketFlags.Request, (ulong)(10 + (int)kind), 1);
            IReadOnlyList<RoutedOutboundMessage> routed = router.Route(
                "connection-a", header, MessagePackSerializer.Serialize(request));
            NetworkAnimationActionStartedMessage response = MessagePackSerializer.Deserialize<NetworkAnimationActionStartedMessage>(
                routed.Single(message => message.ConnectionId == "connection-a").Message.Payload);
            Assert.That(response.ActionSequence, Is.EqualTo(++expectedSequence));
            if (kind == NetworkAnimationActionKind.Reload)
            {
                Assert.That(response.DurationTicks, Is.EqualTo(270));
                Assert.That(response.CommitTick, Is.EqualTo(140));
            }
            Assert.That(routed.Any(message => message.ConnectionId == "connection-b" &&
                message.Message.Header.MessageId == MessageId.AnimationActionStarted), Is.True);
        }

        terminal.AddRange(router.AdvanceAnimationActions(1, 320));
        Assert.That(terminal.Any(message => message.Message.Header.MessageId == MessageId.AnimationActionCommit), Is.True);
    }

    private static PacketHeader Request(ulong requestId, MessageId messageId)
    {
        return new PacketHeader(ProtocolVersion.Current, messageId, PacketFlags.Request, requestId, 0);
    }

    private sealed class RejectingMatchPhysicsServerLifecycle : Fps.ServerNet.Matches.IMatchPhysicsServerLifecycle
    {
        public int StopCount { get; private set; }

        public Task<Fps.ServerNet.Matches.MatchPhysicsServerReady> StartAsync(
            Fps.ServerNet.Matches.MatchPhysicsServerRequest request,
            CancellationToken cancellationToken) => throw new InvalidOperationException("physics failed");

        public Task StopAsync(long matchId, CancellationToken cancellationToken)
        {
            StopCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class DelayedMatchPhysicsServerLifecycle : Fps.ServerNet.Matches.IMatchPhysicsServerLifecycle
    {
        private readonly TaskCompletionSource<Fps.ServerNet.Matches.MatchPhysicsServerReady> ready =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<Fps.ServerNet.Matches.MatchPhysicsServerReady> StartAsync(
            Fps.ServerNet.Matches.MatchPhysicsServerRequest request,
            CancellationToken cancellationToken) => ready.Task;

        public Task StopAsync(long matchId, CancellationToken cancellationToken) => Task.CompletedTask;

        public void PublishReady() => ready.TrySetResult(
            new Fps.ServerNet.Matches.MatchPhysicsServerReady(1, "127.0.0.1:31000", "match-1"));
    }
}
