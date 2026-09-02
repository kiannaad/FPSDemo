using System;

namespace CGame.Network
{
    public sealed class ClientMovementNetworkChannel : IDisposable
    {
        private readonly IClientNetworkTransport transport;
        private long matchId;

        public ClientMovementNetworkChannel(IClientNetworkTransport transport)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            transport.ResponseReceived += OnResponseReceived;
        }

        public event Action<NetworkRpcResponse> OwnerReconcileReceived;
        public event Action<NetworkRpcResponse> AuthoritySnapshotReceived;
        public bool IsConnected => transport.IsConnected;

        public void Connect(long targetMatchId, string endpoint, string credential)
        {
            if (!TryParseEndpoint(endpoint, out string host, out int port))
                throw new ArgumentException("Data endpoint must use host:port.", nameof(endpoint));
            matchId = targetMatchId;
            transport.Connect(host, port, credential);
        }

        public void Send(PawnMove move)
        {
            byte[] payload = NetworkMessageSerializer.Serialize(PawnMoveWireMessage.FromMove(move));
            byte[] packet = NetworkPacketCodec.Encode(new NetworkPacketHeader(
                NetworkPacketCodec.ProtocolVersion,
                NetworkMessageId.PawnMove,
                NetworkPacketFlags.Request,
                0,
                move.MatchId), payload);
            transport.Send(packet, NetworkDelivery.UnreliableSequenced);
        }

        public void PollEvents() => transport.PollEvents();

        public void Dispose()
        {
            transport.ResponseReceived -= OnResponseReceived;
            transport.Dispose();
        }

        private void OnResponseReceived(NetworkRpcResponse response)
        {
            if (response.Header.MatchId == matchId && response.Header.MessageId == NetworkMessageId.OwnerReconcile)
                OwnerReconcileReceived?.Invoke(response);
            else if (response.Header.MatchId == matchId && response.Header.MessageId == NetworkMessageId.AuthoritySnapshot)
                AuthoritySnapshotReceived?.Invoke(response);
        }

        private static bool TryParseEndpoint(string endpoint, out string host, out int port)
        {
            host = null;
            port = 0;
            if (string.IsNullOrWhiteSpace(endpoint)) return false;
            int separator = endpoint.LastIndexOf(':');
            return separator > 0 &&
                   int.TryParse(endpoint.Substring(separator + 1), out port) &&
                   port > 0 && port <= 65535 &&
                   !string.IsNullOrWhiteSpace(host = endpoint.Substring(0, separator));
        }
    }
}
