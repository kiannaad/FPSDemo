using Fps.Protocol;
using Fps.ServerNet;
using LiteNetLib;
using MessagePack;
using NUnit.Framework;

namespace Fps.ServerNet.Tests;

public sealed class LiteNetServerTransportTests
{
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
