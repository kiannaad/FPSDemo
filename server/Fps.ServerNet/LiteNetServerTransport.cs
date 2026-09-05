using Fps.Protocol;
using LiteNetLib;

namespace Fps.ServerNet;

public sealed class LiteNetServerTransport : IDisposable
{
    public const string ConnectionKey = "fps-v1";

    private readonly EventBasedLiteNetListener listener = new();
    private readonly MessageRouter router;
    private readonly Dictionary<string, LiteNetPeer> peersByConnectionId = new(StringComparer.Ordinal);
    private readonly Queue<RoutedOutboundMessage> pendingOutboundMessages = new();
    private readonly Queue<InboundMessage> pendingInboundMessages = new();
    private LiteNetManager? manager;

    public LiteNetServerTransport(MessageRouter router)
    {
        this.router = router ?? throw new ArgumentNullException(nameof(router));
        listener.ConnectionRequestEvent += request => request.AcceptIfKey(ConnectionKey);
        listener.PeerConnectedEvent += peer => peersByConnectionId[GetConnectionId(peer)] = peer;
        listener.PeerDisconnectedEvent += (peer, _) => peersByConnectionId.Remove(GetConnectionId(peer));
        listener.NetworkReceiveEvent += HandleReceive;
    }

    public int Port => manager?.LocalPort ?? 0;

    public void Start(int port)
    {
        if (manager is not null)
        {
            throw new InvalidOperationException("The server transport is already started.");
        }

        var candidate = new LiteNetManager(listener);
        if (!candidate.Start(port))
        {
            candidate.Stop();
            throw new IOException($"Unable to bind UDP port {port}.");
        }

        manager = candidate;
    }

    public async Task PollEventsAsync(CancellationToken cancellationToken = default)
    {
        manager?.PollEvents();
        while (pendingInboundMessages.Count > 0)
        {
            InboundMessage inbound = pendingInboundMessages.Dequeue();
            try
            {
                IReadOnlyList<RoutedOutboundMessage> routed = await router.RouteAsync(
                    inbound.ConnectionId,
                    inbound.Header,
                    inbound.Payload,
                    cancellationToken);
                foreach (RoutedOutboundMessage routedMessage in routed)
                {
                    pendingOutboundMessages.Enqueue(routedMessage);
                }
            }
            catch (Exception exception)
            {
                if ((inbound.Header.Flags & PacketFlags.Request) != 0)
                {
                    Console.Error.WriteLine($"[Server][Protocol] Rejecting {inbound.ConnectionId}: {exception.Message}");
                    var failureHeader = new PacketHeader(
                        ProtocolVersion.Current,
                        inbound.Header.MessageId,
                        PacketFlags.Response | PacketFlags.Failure,
                        inbound.Header.RequestId,
                        inbound.Header.MatchId);
                    byte[] failurePayload = MessagePack.MessagePackSerializer.Serialize(new RpcFailureResponse(exception.Message));
                    pendingOutboundMessages.Enqueue(new RoutedOutboundMessage(
                        inbound.ConnectionId,
                        new RoutedMessage(failureHeader, failurePayload)));
                    continue;
                }

                Console.Error.WriteLine($"[Server][Protocol] Disconnecting {inbound.ConnectionId}: {exception.Message}");
                if (peersByConnectionId.TryGetValue(inbound.ConnectionId, out LiteNetPeer? failedPeer))
                {
                    failedPeer.Disconnect();
                }
            }
        }

        foreach (RoutedOutboundMessage timelineMessage in await router.AdvanceAnimationActionsAsync(cancellationToken))
        {
            pendingOutboundMessages.Enqueue(timelineMessage);
        }

        while (pendingOutboundMessages.Count > 0)
        {
            RoutedOutboundMessage outbound = pendingOutboundMessages.Dequeue();
            if (!peersByConnectionId.TryGetValue(outbound.ConnectionId, out LiteNetPeer? targetPeer))
            {
                continue;
            }

            targetPeer.Send(PacketCodec.Encode(outbound.Message.Header, outbound.Message.Payload), DeliveryMethod.ReliableOrdered);
        }
    }

    public void PollEvents() => PollEventsAsync().GetAwaiter().GetResult();

    public void Dispose()
    {
        manager?.Stop();
        manager = null;
    }

    private void HandleReceive(LiteNetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
    {
        try
        {
            if (!PacketCodec.TryDecode(reader.GetRemainingBytes(), out PacketHeader header, out ReadOnlyMemory<byte> payload))
            {
                Console.Error.WriteLine($"[Server][Protocol] Disconnecting {GetConnectionId(peer)}: invalid packet.");
                peer.Disconnect();
                return;
            }

            pendingInboundMessages.Enqueue(new InboundMessage(GetConnectionId(peer), header, payload.ToArray()));
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"[Server][Protocol] Disconnecting {GetConnectionId(peer)}: {exception.Message}");
            peer.Disconnect();
        }
        finally
        {
            reader.Recycle();
        }
    }

    private static string GetConnectionId(LiteNetPeer peer) => peer.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private readonly record struct InboundMessage(string ConnectionId, PacketHeader Header, byte[] Payload);
}
