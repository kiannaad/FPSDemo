using System;
using System.Threading;
using System.Threading.Tasks;
using MessagePack;
using UnityEngine;

namespace CGame.Network
{
    public sealed class ClientNetworkSubSystem : WorldSubSystem
    {
        private readonly ClientNetworkDefinition definition;
        private NetworkRpcClient rpcClient;
        private Task<NetworkRpcResponse> helloTask;
        private bool helloRequested;

        public ClientNetworkSubSystem(ClientNetworkDefinition definition)
        {
            this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
        }

        public bool IsHelloComplete { get; private set; }
        public string Failure { get; private set; } = string.Empty;
        public HelloResponse HelloResponse { get; private set; }
        public ClientWorld ClientWorld { get; } = new ClientWorld();

        protected override Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            rpcClient = new NetworkRpcClient(new LiteNetClientNetworkTransport());
            rpcClient.EventReceived += OnNetworkEvent;
            AddTickTask("Network.Client", TickGroup.TG_Gameplay, Tick);
            rpcClient.Connect(definition.Host, definition.Port, definition.ConnectionKey);
            return Task.CompletedTask;
        }

        protected override void OnBeginPlay()
        {
        }

        protected override Task OnShutdownAsync()
        {
            if (rpcClient != null) rpcClient.EventReceived -= OnNetworkEvent;
            rpcClient?.Dispose();
            rpcClient = null;
            return Task.CompletedTask;
        }

        private void Tick(float deltaTime)
        {
            rpcClient.Tick();
            if (!helloRequested && rpcClient.IsConnected)
            {
                helloRequested = true;
                byte[] payload = MessagePackSerializer.Serialize(new HelloRequest(Application.version, NetworkPacketCodec.ProtocolVersion));
                helloTask = rpcClient.RequestAsync(
                    NetworkMessageId.HelloRequest,
                    payload,
                    TimeSpan.FromSeconds(definition.RequestTimeoutSeconds));
            }

            if (helloTask == null || !helloTask.IsCompleted || IsHelloComplete || !string.IsNullOrEmpty(Failure)) return;
            if (helloTask.IsFaulted)
            {
                Failure = helloTask.Exception?.GetBaseException().Message ?? "Unknown Hello RPC failure.";
                Debug.LogError($"[Network][033] Hello RPC failed: {Failure}");
                return;
            }

            try
            {
                HelloResponse = MessagePackSerializer.Deserialize<HelloResponse>(helloTask.Result.Payload);
                IsHelloComplete = HelloResponse.ProtocolVersion == NetworkPacketCodec.ProtocolVersion;
                if (!IsHelloComplete)
                {
                    Failure = $"Protocol mismatch: server={HelloResponse.ProtocolVersion}, client={NetworkPacketCodec.ProtocolVersion}.";
                    Debug.LogError($"[Network][033] {Failure}");
                    return;
                }

                Debug.Log($"[Network][033] Hello RPC complete: server={HelloResponse.ServerVersion}, protocol={HelloResponse.ProtocolVersion}.");
            }
            catch (Exception exception)
            {
                Failure = exception.Message;
                Debug.LogError($"[Network][033] Hello RPC decode failed: {Failure}");
            }
        }

        public void PumpPrePlay()
        {
            if (rpcClient == null) return;
            Tick(0f);
        }

        public async Task<CreateRoomResponse> CreateRoomAsync(string requestedRoomName)
        {
            NetworkRpcResponse response = await SendRequestAsync(
                NetworkMessageId.CreateRoomRequest,
                new CreateRoomRequest { RequestedRoomName = requestedRoomName });
            var room = MessagePackSerializer.Deserialize<CreateRoomResponse>(response.Payload);
            ClientWorld.SetLocalPlayer(room.PlayerId);
            return room;
        }

        public async Task<JoinRoomResponse> JoinRoomAsync(string roomId)
        {
            NetworkRpcResponse response = await SendRequestAsync(
                NetworkMessageId.JoinRoomRequest,
                new JoinRoomRequest { RoomId = roomId });
            var room = MessagePackSerializer.Deserialize<JoinRoomResponse>(response.Payload);
            ClientWorld.SetLocalPlayer(room.PlayerId);
            return room;
        }

        public async Task<SetReadyResponse> SetReadyAsync(string roomId, bool isReady)
        {
            NetworkRpcResponse response = await SendRequestAsync(
                NetworkMessageId.SetReadyRequest,
                new SetReadyRequest { RoomId = roomId, IsReady = isReady });
            return MessagePackSerializer.Deserialize<SetReadyResponse>(response.Payload);
        }

        private Task<NetworkRpcResponse> SendRequestAsync<TRequest>(NetworkMessageId messageId, TRequest request)
        {
            if (rpcClient == null) throw new InvalidOperationException("Client network is not initialized.");
            return rpcClient.RequestAsync(messageId, MessagePackSerializer.Serialize(request), TimeSpan.FromSeconds(definition.RequestTimeoutSeconds));
        }

        private void OnNetworkEvent(NetworkRpcResponse response)
        {
            switch (response.Header.MessageId)
            {
                case NetworkMessageId.MatchStarting:
                    ClientWorld.OnMatchStarting(MessagePackSerializer.Deserialize<MatchStartingEvent>(response.Payload));
                    break;
                case NetworkMessageId.PawnSpawned:
                    ClientWorld.OnPawnSpawned(MessagePackSerializer.Deserialize<PawnSpawnedEvent>(response.Payload));
                    break;
                case NetworkMessageId.PossessionChanged:
                    ClientWorld.OnPossessionChanged(MessagePackSerializer.Deserialize<PossessionChangedEvent>(response.Payload));
                    break;
            }
        }
    }
}
