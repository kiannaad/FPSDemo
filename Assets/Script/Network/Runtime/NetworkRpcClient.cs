using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using LiteNetLib;

namespace CGame.Network
{
    public readonly struct NetworkRpcResponse
    {
        public NetworkRpcResponse(NetworkPacketHeader header, byte[] payload)
        {
            Header = header;
            Payload = payload;
        }

        public NetworkPacketHeader Header { get; }
        public byte[] Payload { get; }
    }

    public interface IClientNetworkTransport : IDisposable
    {
        event Action Connected;
        event Action<NetworkRpcResponse> ResponseReceived;
        event Action<string> Disconnected;
        bool IsConnected { get; }
        void Connect(string host, int port, string connectionKey);
        void Send(byte[] packet, NetworkDelivery delivery);
        void PollEvents();
    }

    public sealed class LiteNetClientNetworkTransport : IClientNetworkTransport
    {
        private readonly EventBasedLiteNetListener listener = new EventBasedLiteNetListener();
        private LiteNetManager manager;
        private LiteNetPeer peer;

        public LiteNetClientNetworkTransport()
        {
            listener.PeerConnectedEvent += OnConnected;
            listener.PeerDisconnectedEvent += OnDisconnected;
            listener.NetworkReceiveEvent += OnReceive;
        }

        public event Action Connected;
        public event Action<NetworkRpcResponse> ResponseReceived;
        public event Action<string> Disconnected;

        public bool IsConnected => peer != null;

        public void Connect(string host, int port, string connectionKey)
        {
            if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("Host is required.", nameof(host));
            if (port < 1 || port > 65535) throw new ArgumentOutOfRangeException(nameof(port));
            if (string.IsNullOrWhiteSpace(connectionKey)) throw new ArgumentException("Connection key is required.", nameof(connectionKey));
            if (manager != null) throw new InvalidOperationException("The client transport is already started.");

            LiteNetManager candidate = new LiteNetManager(listener);
            if (!candidate.Start())
            {
                candidate.Stop();
                throw new IOException("Unable to start the client UDP transport.");
            }

            manager = candidate;
            candidate.Connect(host, port, connectionKey);
        }

        public void Send(byte[] packet, NetworkDelivery delivery)
        {
            if (packet == null) throw new ArgumentNullException(nameof(packet));
            if (peer == null) throw new InvalidOperationException("The client transport is not connected.");
            peer.Send(
                packet,
                delivery == NetworkDelivery.UnreliableSequenced
                    ? DeliveryMethod.Sequenced
                    : DeliveryMethod.ReliableOrdered);
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

        private void OnConnected(LiteNetPeer connectedPeer)
        {
            peer = connectedPeer;
            Connected?.Invoke();
        }

        private void OnDisconnected(LiteNetPeer disconnectedPeer, DisconnectInfo disconnectInfo)
        {
            if (ReferenceEquals(peer, disconnectedPeer)) peer = null;
            Disconnected?.Invoke(disconnectInfo.Reason.ToString());
        }

        private void OnReceive(LiteNetPeer receivedPeer, NetPacketReader reader, DeliveryMethod deliveryMethod)
        {
            try
            {
                if (NetworkPacketCodec.TryDecode(reader.GetRemainingBytes(), out NetworkPacketHeader header, out byte[] payload))
                {
                    ResponseReceived?.Invoke(new NetworkRpcResponse(header, payload));
                }
            }
            finally
            {
                reader.Recycle();
            }
        }
    }

    public sealed class NetworkRpcClient : IDisposable
    {
        private readonly IClientNetworkTransport transport;
        private readonly ConcurrentQueue<Action> mainThreadEvents = new ConcurrentQueue<Action>();
        private readonly Dictionary<ulong, PendingRequest> pendingRequests = new Dictionary<ulong, PendingRequest>();
        private ulong nextRequestId;
        private bool disposed;

        public NetworkRpcClient(IClientNetworkTransport transport)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            transport.ResponseReceived += OnResponseReceived;
            transport.Disconnected += OnDisconnected;
        }

        public bool IsConnected => transport.IsConnected;
        public event Action<NetworkRpcResponse> EventReceived;

        public void Connect(string host, int port, string connectionKey)
        {
            ThrowIfDisposed();
            transport.Connect(host, port, connectionKey);
        }

        public Task<NetworkRpcResponse> RequestAsync(
            NetworkMessageId messageId,
            byte[] payload,
            TimeSpan timeout,
            long matchId = 0)
        {
            ThrowIfDisposed();
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            if (!transport.IsConnected) throw new InvalidOperationException("Cannot send an RPC before the client transport is connected.");
            if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));

            ulong requestId = checked(++nextRequestId);
            var completionSource = new TaskCompletionSource<NetworkRpcResponse>();
            pendingRequests.Add(requestId, new PendingRequest(DateTime.UtcNow.Add(timeout), completionSource));
            var header = new NetworkPacketHeader(NetworkPacketCodec.ProtocolVersion, messageId, NetworkPacketFlags.Request, requestId, matchId);
            transport.Send(
                NetworkPacketCodec.Encode(header, payload),
                NetworkDeliveryPolicy.For(messageId));
            return completionSource.Task;
        }

        public void Tick()
        {
            ThrowIfDisposed();
            transport.PollEvents();
            while (mainThreadEvents.TryDequeue(out Action action)) action();

            DateTime now = DateTime.UtcNow;
            List<ulong> expiredRequestIds = null;
            foreach (KeyValuePair<ulong, PendingRequest> item in pendingRequests)
            {
                if (item.Value.Deadline > now) continue;
                if (expiredRequestIds == null) expiredRequestIds = new List<ulong>();
                expiredRequestIds.Add(item.Key);
            }

            if (expiredRequestIds == null) return;
            foreach (ulong requestId in expiredRequestIds)
            {
                PendingRequest request = pendingRequests[requestId];
                pendingRequests.Remove(requestId);
                request.CompletionSource.TrySetException(new TimeoutException("The RPC response timed out."));
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            transport.ResponseReceived -= OnResponseReceived;
            transport.Disconnected -= OnDisconnected;
            foreach (PendingRequest request in pendingRequests.Values)
            {
                request.CompletionSource.TrySetException(new IOException("The RPC client was disposed."));
            }

            pendingRequests.Clear();
            transport.Dispose();
        }

        private void OnResponseReceived(NetworkRpcResponse response)
        {
            mainThreadEvents.Enqueue(() =>
            {
                if ((response.Header.Flags & NetworkPacketFlags.Response) == 0)
                {
                    EventReceived?.Invoke(response);
                    return;
                }
                if (!pendingRequests.TryGetValue(response.Header.RequestId, out PendingRequest pendingRequest)) return;
                pendingRequests.Remove(response.Header.RequestId);
                pendingRequest.CompletionSource.TrySetResult(response);
            });
        }

        private void OnDisconnected(string reason)
        {
            mainThreadEvents.Enqueue(() =>
            {
                foreach (PendingRequest request in pendingRequests.Values)
                {
                    request.CompletionSource.TrySetException(new IOException(reason));
                }

                pendingRequests.Clear();
            });
        }

        private void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(NetworkRpcClient));
        }

        private readonly struct PendingRequest
        {
            public PendingRequest(DateTime deadline, TaskCompletionSource<NetworkRpcResponse> completionSource)
            {
                Deadline = deadline;
                CompletionSource = completionSource;
            }

            public DateTime Deadline { get; }
            public TaskCompletionSource<NetworkRpcResponse> CompletionSource { get; }
        }
    }
}
