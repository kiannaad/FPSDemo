using Fps.Protocol;
using Fps.ServerNet;
using LiteNetLib;
using MessagePack;
using NUnit.Framework;

namespace Fps.ServerNet.Tests;

public sealed class LiteNetServerTransportTests
{
    [Test]
    public async Task RejectedActionRequest_ReturnsFailureResponseWithoutDisconnectingPeer()
    {
        using var server = new LiteNetServerTransport(new MessageRouter("server-042"));
        server.Start(0);

        var clientListener = new EventBasedLiteNetListener();
        var client = new LiteNetManager(clientListener);
        Assert.That(client.Start(), Is.True);
        try
        {
            var failureSource = new TaskCompletionSource<RoutedMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            var helloSource = new TaskCompletionSource<RoutedMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            clientListener.NetworkReceiveEvent += (peer, reader, _) =>
            {
                try
                {
                    if (!PacketCodec.TryDecode(reader.GetRemainingBytes(), out PacketHeader header, out ReadOnlyMemory<byte> payload)) return;
                    var response = new RoutedMessage(header, payload.ToArray());
                    if ((header.Flags & PacketFlags.Failure) != 0)
                    {
                        failureSource.TrySetResult(response);
                        var helloHeader = new PacketHeader(ProtocolVersion.Current, MessageId.HelloRequest, PacketFlags.Request, 99, 0);
                        peer.Send(PacketCodec.Encode(helloHeader, MessagePackSerializer.Serialize(new HelloRequest("client-042", ProtocolVersion.Current))), DeliveryMethod.ReliableOrdered);
                        return;
                    }

                    helloSource.TrySetResult(response);
                }
                finally
                {
                    reader.Recycle();
                }
            };
            clientListener.PeerConnectedEvent += peer =>
            {
                var header = new PacketHeader(ProtocolVersion.Current, MessageId.AnimationActionRequest, PacketFlags.Request, 98, 123);
                var request = new NetworkAnimationActionRequestMessage(1, 1, 1, NetworkAnimationActionKind.Melee, "melee", 1);
                peer.Send(PacketCodec.Encode(header, MessagePackSerializer.Serialize(request)), DeliveryMethod.ReliableOrdered);
            };

            client.Connect("127.0.0.1", server.Port, LiteNetServerTransport.ConnectionKey);
            RoutedMessage failure = await PollUntilCompleteAsync(server, client, failureSource.Task);
            RoutedMessage hello = await PollUntilCompleteAsync(server, client, helloSource.Task);

            Assert.That(failure.Header.MessageId, Is.EqualTo(MessageId.AnimationActionRequest));
            Assert.That(failure.Header.Flags, Is.EqualTo(PacketFlags.Response | PacketFlags.Failure));
            Assert.That(failure.Header.RequestId, Is.EqualTo(98));
            Assert.That(MessagePackSerializer.Deserialize<RpcFailureResponse>(failure.Payload).Reason, Does.Contain("match is not active"));
            Assert.That(hello.Header.MessageId, Is.EqualTo(MessageId.HelloResponse));
            Assert.That(hello.Header.RequestId, Is.EqualTo(99));
        }
        finally
        {
            client.Stop();
        }
    }

    [Test]
    public async Task ConnectedClient_HelloRequest_ReceivesRoutedHelloResponse()
    {
        using var server = new LiteNetServerTransport(new MessageRouter("server-033"));
        server.Start(0);

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
                var requestHeader = new PacketHeader(
                    ProtocolVersion.Current,
                    MessageId.HelloRequest,
                    PacketFlags.Request,
                    RequestId: 99,
                    MatchId: 0);
                byte[] requestPayload = MessagePackSerializer.Serialize(new HelloRequest("client-033", ProtocolVersion.Current));
                peer.Send(PacketCodec.Encode(requestHeader, requestPayload), DeliveryMethod.ReliableOrdered);
            };

            client.Connect("127.0.0.1", server.Port, LiteNetServerTransport.ConnectionKey);
            RoutedMessage response = await PollUntilCompleteAsync(server, client, responseSource.Task);

            Assert.That(response.Header.RequestId, Is.EqualTo(99));
            Assert.That(response.Header.Flags, Is.EqualTo(PacketFlags.Response));
            Assert.That(MessagePackSerializer.Deserialize<HelloResponse>(response.Payload).ServerVersion, Is.EqualTo("server-033"));
        }
        finally
        {
            client.Stop();
        }
    }

    private static async Task<T> PollUntilCompleteAsync<T>(LiteNetServerTransport server, LiteNetManager client, Task<T> task)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(3);
        while (!task.IsCompleted && DateTime.UtcNow < deadline)
        {
            server.PollEvents();
            client.PollEvents();
            await Task.Delay(10);
        }

        return await task.WaitAsync(TimeSpan.FromSeconds(1));
    }
}
