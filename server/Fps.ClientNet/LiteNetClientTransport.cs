using Fps.Protocol;
using LiteNetLib;

namespace Fps.ClientNet;

public sealed class LiteNetClientTransport : IClientTransport
{
    private readonly EventBasedLiteNetListener listener = new();
    private LiteNetManager? manager;
    private LiteNetPeer? peer;

    public LiteNetClientTransport()
    {
        listener.PeerConnectedEvent += HandleConnected;
        listener.PeerDisconnectedEvent += HandleDisconnected;
        listener.NetworkReceiveEvent += HandleReceive;
    }

    public event Action? Connected;

    public event Action<RpcResponse>? ResponseReceived;

    public event Action<string>? Disconnected;

    public bool IsConnected => peer is not null;

    public void Connect(string host, int port, string connectionKey)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            throw new ArgumentException("Host is required.", nameof(host));
        }

        if (port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port));
        }

        if (string.IsNullOrWhiteSpace(connectionKey))
        {
            throw new ArgumentException("Connection key is required.", nameof(connectionKey));
        }

        if (manager is not null)
        {
            throw new InvalidOperationException("The client transport is already started.");
        }

        var candidate = new LiteNetManager(listener);
        if (!candidate.Start())
        {
            candidate.Stop();
            throw new IOException("Unable to start the client UDP transport.");
        }

        manager = candidate;
        candidate.Connect(host, port, connectionKey);
    }

    public void Send(byte[] packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        if (peer is null)
        {
            throw new InvalidOperationException("The client transport is not connected.");
        }

        peer.Send(packet, DeliveryMethod.ReliableOrdered);
    }

    public void PollEvents()
    {
        manager?.PollEvents();
    }

    public void Dispose()
    {
        manager?.Stop();
        manager = null;
        peer = null;
    }

    private void HandleConnected(LiteNetPeer connectedPeer)
    {
        peer = connectedPeer;
        Connected?.Invoke();
    }

    private void HandleDisconnected(LiteNetPeer disconnectedPeer, DisconnectInfo disconnectInfo)
    {
        if (ReferenceEquals(peer, disconnectedPeer))
        {
            peer = null;
        }

        Disconnected?.Invoke(disconnectInfo.Reason.ToString());
    }

    private void HandleReceive(LiteNetPeer receivedPeer, NetPacketReader reader, DeliveryMethod deliveryMethod)
    {
        try
        {
            if (PacketCodec.TryDecode(reader.GetRemainingBytes(), out PacketHeader header, out ReadOnlyMemory<byte> payload))
            {
                ResponseReceived?.Invoke(new RpcResponse(header, payload.ToArray()));
            }
        }
        finally
        {
            reader.Recycle();
        }
    }
}
