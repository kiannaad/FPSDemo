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

    public void PollEvents()
    {
        manager?.PollEvents();
        if (pendingOutboundMessages.Count == 0) return;
        RoutedOutboundMessage outbound = pendingOutboundMessages.Dequeue();
        if (!peersByConnectionId.TryGetValue(outbound.ConnectionId, out LiteNetPeer? targetPeer)) return;
        targetPeer.Send(PacketCodec.Encode(outbound.Message.Header, outbound.Message.Payload), DeliveryMethod.ReliableOrdered);
    }

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

            IReadOnlyList<RoutedOutboundMessage> outboundMessages = router.Route(GetConnectionId(peer), header, payload.Span);
            foreach (RoutedOutboundMessage outbound in outboundMessages)
            {
                pendingOutboundMessages.Enqueue(outbound);
            }
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
}
