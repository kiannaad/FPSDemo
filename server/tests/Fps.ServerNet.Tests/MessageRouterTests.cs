using MessagePack;
using NUnit.Framework;
using Fps.Protocol;
using Fps.ServerNet;

namespace Fps.ServerNet.Tests;

public sealed class MessageRouterTests
{
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

    private static PacketHeader Request(ulong requestId, MessageId messageId)
    {
        return new PacketHeader(ProtocolVersion.Current, messageId, PacketFlags.Request, requestId, 0);
    }
}
