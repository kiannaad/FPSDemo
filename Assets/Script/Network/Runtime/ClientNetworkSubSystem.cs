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
        private ClientMovementNetworkChannel movementChannel;
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
        public bool IsMovementConnected => movementChannel?.IsConnected == true;
        public event Action<OwnerReconcile> OwnerReconcileReceived;
        public event Action<AuthoritySnapshot> AuthoritySnapshotReceived;
        public event Action<NetworkAnimationActionStarted> AnimationActionStartedReceived;
        public event Action<NetworkAnimationActionTerminal> AnimationActionTerminalReceived;
        public event Action<FireCommitted> FireCommittedReceived;
        public event Action<FireRejected> FireRejectedReceived;
        public event Action<TargetStateMessage> TargetStateChangedReceived;
        public event Action<TargetStateSnapshotMessage> TargetStateSnapshotReceived;

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
            if (movementChannel != null) movementChannel.OwnerReconcileReceived -= OnOwnerReconcileReceived;
            if (movementChannel != null) movementChannel.AuthoritySnapshotReceived -= OnAuthoritySnapshotReceived;
            movementChannel?.Dispose();
            movementChannel = null;
            rpcClient?.Dispose();
            rpcClient = null;
            return Task.CompletedTask;
        }

        private void Tick(float deltaTime)
        {
            rpcClient.Tick();
            movementChannel?.PollEvents();
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

        public async Task<NetworkAnimationActionStarted> SendAnimationActionRequestAsync(
            NetworkAnimationActionRequest request)
        {
            if (ClientWorld.MatchId <= 0)
                throw new InvalidOperationException("Cannot send an animation action before MatchStarting.");
            Debug.Log($"[Network][042] ActionRequestSend MatchId={ClientWorld.MatchId} PawnId={request.PawnId} Kind={request.ActionKind} PredictionNonce={request.PredictionNonce}");
            NetworkRpcResponse response = await rpcClient.RequestAsync(
                NetworkMessageId.AnimationActionRequest,
                MessagePackSerializer.Serialize(request),
                TimeSpan.FromSeconds(definition.RequestTimeoutSeconds),
                ClientWorld.MatchId);
            NetworkAnimationActionStarted started = NetworkMessageSerializer.Deserialize<NetworkAnimationActionStarted>(response.Payload);
            AnimationActionStartedReceived?.Invoke(started);
            return started;
        }

        public async Task SendFireRequestAsync(FireRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (ClientWorld.MatchId <= 0)
                throw new InvalidOperationException("Cannot send fire before MatchStarting.");

            Debug.Log($"[Network][044] FireRequestSend MatchId={ClientWorld.MatchId} PawnId={request.PawnId} PredictionNonce={request.PredictionNonce} ClientShotSequence={request.ClientShotSequence}");
            NetworkRpcResponse response = await rpcClient.RequestAsync(
                NetworkMessageId.FireRequest,
                NetworkMessageSerializer.Serialize(request),
                TimeSpan.FromSeconds(definition.RequestTimeoutSeconds),
                ClientWorld.MatchId);
            switch (response.Header.MessageId)
            {
                case NetworkMessageId.FireCommitted:
                    FireCommittedReceived?.Invoke(NetworkMessageSerializer.Deserialize<FireCommitted>(response.Payload));
                    break;
                case NetworkMessageId.FireRejected:
                    FireRejectedReceived?.Invoke(NetworkMessageSerializer.Deserialize<FireRejected>(response.Payload));
                    break;
                case NetworkMessageId.TargetStateChanged:
                    TargetStateChangedReceived?.Invoke(MessagePackSerializer.Deserialize<TargetStateMessage>(response.Payload));
                    break;
                case NetworkMessageId.TargetStateSnapshot:
                    TargetStateSnapshotReceived?.Invoke(MessagePackSerializer.Deserialize<TargetStateSnapshotMessage>(response.Payload));
                    break;
                default:
                    throw new InvalidOperationException($"Unexpected Fire response: {response.Header.MessageId}.");
            }
        }

        public void SendPawnMove(PawnMove move)
        {
            if (!IsMovementConnected) throw new InvalidOperationException("Movement data channel is not connected.");
            movementChannel.Send(move);
            if (move.Sequence % 60 == 0)
                Debug.Log($"[Network][038] MoveCreated PawnId={move.PawnId} Sequence={move.Sequence} ClientTick={move.ClientTick}");
            if (move.Flags != PawnMoveFlags.None)
                Debug.Log($"[Network][043] InputMoveSendTrace MatchId={move.MatchId} PawnId={move.PawnId} Sequence={move.Sequence} ClientTick={move.ClientTick} Flags={move.Flags}");
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
                    MatchStartingEvent starting = MessagePackSerializer.Deserialize<MatchStartingEvent>(response.Payload);
                    ClientWorld.OnMatchStarting(starting);
                    movementChannel = new ClientMovementNetworkChannel(new LiteNetClientNetworkTransport());
                    movementChannel.OwnerReconcileReceived += OnOwnerReconcileReceived;
                    movementChannel.AuthoritySnapshotReceived += OnAuthoritySnapshotReceived;
                    movementChannel.Connect(starting.MatchId, starting.DataEndpoint, starting.CredentialId);
                    break;
                case NetworkMessageId.PawnSpawned:
                    ClientWorld.OnPawnSpawned(MessagePackSerializer.Deserialize<PawnSpawnedEvent>(response.Payload));
                    break;
                case NetworkMessageId.PossessionChanged:
                    ClientWorld.OnPossessionChanged(MessagePackSerializer.Deserialize<PossessionChangedEvent>(response.Payload));
                    break;
                case NetworkMessageId.AnimationActionStarted:
                    AnimationActionStartedReceived?.Invoke(
                        NetworkMessageSerializer.Deserialize<NetworkAnimationActionStarted>(response.Payload));
                    break;
                case NetworkMessageId.AnimationActionCommit:
                case NetworkMessageId.AnimationActionEnded:
                case NetworkMessageId.AnimationActionCancelled:
                    AnimationActionTerminalReceived?.Invoke(
                        NetworkMessageSerializer.Deserialize<NetworkAnimationActionTerminal>(response.Payload));
                    break;
                case NetworkMessageId.FireCommitted:
                    FireCommittedReceived?.Invoke(NetworkMessageSerializer.Deserialize<FireCommitted>(response.Payload));
                    break;
                case NetworkMessageId.FireRejected:
                    FireRejectedReceived?.Invoke(NetworkMessageSerializer.Deserialize<FireRejected>(response.Payload));
                    break;
                case NetworkMessageId.TargetStateChanged:
                    TargetStateChangedReceived?.Invoke(MessagePackSerializer.Deserialize<TargetStateMessage>(response.Payload));
                    break;
                case NetworkMessageId.TargetStateSnapshot:
                    TargetStateSnapshotReceived?.Invoke(MessagePackSerializer.Deserialize<TargetStateSnapshotMessage>(response.Payload));
                    break;
            }
        }

        private void OnOwnerReconcileReceived(NetworkRpcResponse response)
        {
            OwnerReconcile reconcile = NetworkMessageSerializer.Deserialize<OwnerReconcileWireMessage>(response.Payload).ToValue();
            OwnerReconcileReceived?.Invoke(reconcile);
            if (reconcile.Kind != OwnerReconcileKind.Ack || reconcile.AckSequence % 60 == 0)
                Debug.Log($"[Network][038] ReconcileReceived Sequence={reconcile.AckSequence} Kind={reconcile.Kind} Reason={reconcile.Reason ?? "None"}");
        }

        private void OnAuthoritySnapshotReceived(NetworkRpcResponse response)
        {
            AuthoritySnapshot snapshot = NetworkMessageSerializer
                .Deserialize<AuthoritySnapshotWireMessage>(response.Payload)
                .ToValue();
            AuthoritySnapshotReceived?.Invoke(snapshot);
        }
    }
}
