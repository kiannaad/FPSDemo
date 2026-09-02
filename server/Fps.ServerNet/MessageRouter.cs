using MessagePack;
using Fps.Protocol;
using Fps.ServerDomain.Matches;
using Fps.ServerDomain.Players;
using Fps.ServerDomain.Rooms;
using Fps.ServerDomain.World;
using Fps.ServerNet.Matches;

namespace Fps.ServerNet;

public readonly record struct RoutedMessage(PacketHeader Header, byte[] Payload);
public readonly record struct RoutedOutboundMessage(string ConnectionId, RoutedMessage Message);

public sealed class MessageRouter
{
    private readonly string serverVersion;
    private readonly RoomManager rooms = new();
    private readonly Dictionary<string, ServerPlayer> playersByConnection = new(StringComparer.Ordinal);
    private readonly string[] spawnPointIds;
    private readonly IMatchPhysicsServerLifecycle physicsServerLifecycle;
    private readonly Dictionary<long, ActiveAnimationMatch> animationMatchesById = new();
    private long nextPlayerId;
    private long nextMatchId;

    public MessageRouter(
        string serverVersion,
        IEnumerable<string>? spawnPointIds = null,
        IMatchPhysicsServerLifecycle? physicsServerLifecycle = null)
    {
        if (string.IsNullOrWhiteSpace(serverVersion))
        {
            throw new ArgumentException("Server version is required.", nameof(serverVersion));
        }

        this.serverVersion = serverVersion;
        this.spawnPointIds = (spawnPointIds ?? new[] { "spawn-a", "spawn-b" }).ToArray();
        this.physicsServerLifecycle = physicsServerLifecycle ?? new ImmediateMatchPhysicsServerLifecycle();
        if (this.spawnPointIds.Length == 0) throw new ArgumentException("At least one spawn point is required.", nameof(spawnPointIds));
    }

    public RoutedMessage Route(PacketHeader header, ReadOnlySpan<byte> payload)
    {
        if (header.Version != ProtocolVersion.Current || header.MessageId != MessageId.HelloRequest)
        {
            throw new InvalidDataException("Only a current Hello request can be routed.");
        }

        if ((header.Flags & PacketFlags.Request) == 0)
        {
            throw new InvalidDataException("Hello packets must be requests.");
        }

        HelloRequest request = MessagePackSerializer.Deserialize<HelloRequest>(payload.ToArray());
        if (request.RequestedProtocolVersion != ProtocolVersion.Current)
        {
            throw new InvalidDataException("The client requested an unsupported protocol version.");
        }

        var responseHeader = new PacketHeader(
            ProtocolVersion.Current,
            MessageId.HelloResponse,
            PacketFlags.Response,
            header.RequestId,
            header.MatchId);
        byte[] responsePayload = MessagePackSerializer.Serialize(
            new HelloResponse(serverVersion, ProtocolVersion.Current));
        return new RoutedMessage(responseHeader, responsePayload);
    }

    public IReadOnlyList<RoutedOutboundMessage> Route(string connectionId, PacketHeader header, ReadOnlySpan<byte> payload)
    {
        return RouteAsync(connectionId, header, payload.ToArray()).GetAwaiter().GetResult();
    }

    public Task<IReadOnlyList<RoutedOutboundMessage>> RouteAsync(
        string connectionId,
        PacketHeader header,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connectionId)) throw new ArgumentException("Connection id is required.", nameof(connectionId));
        if (header.Version != ProtocolVersion.Current || (header.Flags & PacketFlags.Request) == 0)
            throw new InvalidDataException("Only current request packets can be routed.");

        return header.MessageId switch
        {
            MessageId.HelloRequest => Task.FromResult<IReadOnlyList<RoutedOutboundMessage>>(
                new[] { new RoutedOutboundMessage(connectionId, Route(header, payload.Span)) }),
            MessageId.CreateRoomRequest => Task.FromResult(RouteCreateRoom(connectionId, header, payload.Span)),
            MessageId.JoinRoomRequest => Task.FromResult(RouteJoinRoom(connectionId, header, payload.Span)),
            MessageId.SetReadyRequest => RouteSetReadyAsync(connectionId, header, payload, cancellationToken),
            MessageId.AnimationActionRequest => Task.FromResult(RouteAnimationAction(connectionId, header, payload.Span)),
            MessageId.FireRequest => RouteFireAsync(connectionId, header, payload, cancellationToken),
            _ => throw new InvalidDataException("The request message is not supported.")
        };
    }

    private IReadOnlyList<RoutedOutboundMessage> RouteCreateRoom(string connectionId, PacketHeader header, ReadOnlySpan<byte> payload)
    {
        _ = MessagePackSerializer.Deserialize<CreateRoomRequest>(payload.ToArray());
        ServerPlayer player = GetOrCreatePlayer(connectionId);
        ServerRoom room = rooms.CreateRoom(connectionId);
        Console.WriteLine($"[Server][034] RoomCreated RoomId={room.RoomId} ConnectionId={connectionId} PlayerId={player.PlayerId}");
        return Respond(connectionId, header, MessageId.CreateRoomResponse,
            new CreateRoomResponse(room.RoomId, player.PlayerId, room.State.ToString()));
    }

    private IReadOnlyList<RoutedOutboundMessage> RouteJoinRoom(string connectionId, PacketHeader header, ReadOnlySpan<byte> payload)
    {
        JoinRoomRequest request = MessagePackSerializer.Deserialize<JoinRoomRequest>(payload.ToArray());
        ServerPlayer player = GetOrCreatePlayer(connectionId);
        ServerRoom room = rooms.JoinRoom(request.RoomId, connectionId);
        Console.WriteLine($"[Server][034] RoomJoined RoomId={room.RoomId} ConnectionId={connectionId} PlayerId={player.PlayerId}");
        return Respond(connectionId, header, MessageId.JoinRoomResponse,
            new JoinRoomResponse(room.RoomId, player.PlayerId, room.State.ToString()));
    }

    public ServerRoomState GetRoomState(string roomId) => rooms.GetRoomState(roomId);

    private async Task<IReadOnlyList<RoutedOutboundMessage>> RouteSetReadyAsync(
        string connectionId,
        PacketHeader header,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken)
    {
        SetReadyRequest request = MessagePackSerializer.Deserialize<SetReadyRequest>(payload.ToArray());
        ServerRoom room = rooms.SetReady(request.RoomId, connectionId, request.IsReady);
        var messages = new List<RoutedOutboundMessage>(8);
        messages.AddRange(Respond(connectionId, header, MessageId.SetReadyResponse,
            new SetReadyResponse(room.RoomId, room.State.ToString(), room.State == ServerRoomState.Starting)));
        if (room.State != ServerRoomState.Starting) return messages;

        var players = room.ConnectionIds.Select(GetOrCreatePlayer).ToArray();
        ServerMatch? match = null;
        MatchPhysicsServerReady? physicsReady = null;
        try
        {
            match = new ServerMatch(players, spawnPointIds);
            match.Start();
            long pendingMatchId = checked(nextMatchId + 1);
            var physicsRequest = new MatchPhysicsServerRequest(
                pendingMatchId,
                match.Pawns.Select(pawn =>
                {
                    ServerPlayer owner = players.Single(player => player.PlayerId == pawn.OwnerPlayerId);
                    return new MatchPhysicsPawn(
                        pawn.EntityId,
                        pawn.OwnerPlayerId,
                        owner.PossessionRevision,
                        pawn.SpawnPointId,
                        owner.ConnectionId);
                }).ToArray());
            physicsReady = await physicsServerLifecycle.StartAsync(physicsRequest, cancellationToken);
            rooms.MarkStarted(room.RoomId);
        }
        catch
        {
            match?.Stop();
            rooms.ResetToWaiting(room.RoomId);
            long failedMatchId = checked(nextMatchId + 1);
            await physicsServerLifecycle.StopAsync(failedMatchId, CancellationToken.None);
            return messages;
        }
        long matchId = checked(++nextMatchId);
        animationMatchesById.Add(matchId, new ActiveAnimationMatch(players, room.ConnectionIds));
        foreach (ServerPlayer player in players)
        {
            Console.WriteLine($"[Server][034] MatchStarted MatchId={matchId} ConnectionId={player.ConnectionId} PlayerId={player.PlayerId} ControlledPawnId={player.ControlledPawnId}");
        }

        foreach (string targetConnectionId in room.ConnectionIds)
        {
            foreach (ServerPawn pawn in match!.Pawns)
                messages.Add(Event(targetConnectionId, matchId, MessageId.PawnSpawned, new PawnSpawnedEvent(pawn.EntityId, pawn.OwnerPlayerId, pawn.SpawnPointId)));
            foreach (ServerPlayer player in players)
                messages.Add(Event(targetConnectionId, matchId, MessageId.PossessionChanged, new PossessionChangedEvent(player.PlayerId, player.ControlledPawnId, player.PossessionRevision)));
            ServerPlayer targetPlayer = players.Single(player => player.ConnectionId == targetConnectionId);
            ServerPawn targetPawn = match.Pawns.Single(pawn => pawn.OwnerPlayerId == targetPlayer.PlayerId);
            string dataCredential = $"{physicsReady!.CredentialId}:{targetPawn.EntityId}";
            messages.Add(Event(targetConnectionId, matchId, MessageId.MatchStarting, new MatchStartingEvent(
                matchId,
                0,
                physicsReady.DataEndpoint,
                dataCredential)));
        }

        return messages;
    }

    public IReadOnlyList<RoutedOutboundMessage> AdvanceAnimationActions(long matchId, long authorityTick)
    {
        var messages = new List<RoutedOutboundMessage>();
        if (authorityTick <= 0 || !animationMatchesById.TryGetValue(matchId, out ActiveAnimationMatch? match))
            return messages;
        if (authorityTick <= match.AuthorityTick)
            return messages;
        match.AuthorityTick = authorityTick;
        foreach (NetworkAnimationActionTerminalMessage terminal in match.Timeline.AdvanceTo(authorityTick))
        {
            MessageId messageId = terminal.TerminalKind switch
            {
                NetworkAnimationActionTerminalKind.Committed => MessageId.AnimationActionCommit,
                NetworkAnimationActionTerminalKind.Ended => MessageId.AnimationActionEnded,
                _ => MessageId.AnimationActionCancelled
            };
            foreach (string target in match.ConnectionIds)
                messages.Add(Event(target, matchId, messageId, terminal));
            Console.WriteLine($"[Server][042] ActionTerminal MatchId={matchId} PawnId={terminal.PawnId} ActionSequence={terminal.ActionSequence} Kind={terminal.TerminalKind} ServerTick={authorityTick}");
        }
        return messages;
    }

    public async Task<IReadOnlyList<RoutedOutboundMessage>> AdvanceAnimationActionsAsync(CancellationToken cancellationToken)
    {
        var messages = new List<RoutedOutboundMessage>();
        foreach (long matchId in animationMatchesById.Keys.ToArray())
        {
            long? authorityTick = await physicsServerLifecycle.GetAuthorityTickAsync(matchId, cancellationToken);
            if (authorityTick.HasValue) messages.AddRange(AdvanceAnimationActions(matchId, authorityTick.Value));
        }
        return messages;
    }

    private IReadOnlyList<RoutedOutboundMessage> RouteAnimationAction(
        string connectionId,
        PacketHeader header,
        ReadOnlySpan<byte> payload)
    {
        if (!animationMatchesById.TryGetValue(header.MatchId, out ActiveAnimationMatch? match))
            throw new InvalidDataException($"Animation action match is not active: MatchId={header.MatchId}.");
        ServerPlayer player = match.Players.Single(candidate => candidate.ConnectionId == connectionId);
        NetworkAnimationActionRequestMessage request = MessagePackSerializer.Deserialize<NetworkAnimationActionRequestMessage>(payload.ToArray());
        if (!match.EquipmentByPawnId.TryGetValue(player.ControlledPawnId, out AuthorityActionEquipmentState? equipment))
            throw new InvalidDataException("AuthorityPawnEquipmentMissing");
        AuthorityActionEquipmentSnapshot equipmentBeforeStart = equipment.Capture();
        if (!equipment.TryBegin(request, out Action commit, out string equipmentReason))
            throw new InvalidDataException(equipmentReason);
        int durationTicks = request.DurationTicks > 0 ? request.DurationTicks : request.ActionKind switch
        {
            NetworkAnimationActionKind.Reload => 120,
            NetworkAnimationActionKind.Melee => 60,
            _ => 45
        };
        if (durationTicks > 600) throw new InvalidDataException("AnimationActionDurationOutOfRange");
        int? commitOffset = request.ActionKind == NetworkAnimationActionKind.Reload
            ? request.CommitOffsetTicks ?? 30
            : null;
        if (!match.Timeline.TryStart(
                request,
                player.ControlledPawnId,
                player.PossessionRevision,
                match.AuthorityTick,
                durationTicks,
                commitOffset,
                commit,
                () => equipment.Complete(request.ActionKind),
                request.ActionKind == NetworkAnimationActionKind.Reload
                    ? () => (equipment.MagazineAmmo, equipment.ReserveAmmo)
                    : null,
                out NetworkAnimationActionStartedMessage? started,
                out string reason))
        {
            equipment.Restore(equipmentBeforeStart);
            throw new InvalidDataException(reason);
        }

        var messages = new List<RoutedOutboundMessage>
        {
            new(connectionId, new RoutedMessage(
                new PacketHeader(ProtocolVersion.Current, MessageId.AnimationActionStarted, PacketFlags.Response, header.RequestId, header.MatchId),
                MessagePackSerializer.Serialize(started)))
        };
        foreach (string target in match.ConnectionIds)
            if (target != connectionId) messages.Add(Event(target, header.MatchId, MessageId.AnimationActionStarted, started));
        Console.WriteLine($"[Server][042] ActionStarted MatchId={header.MatchId} PawnId={started!.PawnId} PredictionNonce={started.PredictionNonce} ActionSequence={started.ActionSequence} Kind={started.ActionKind} ServerTick={match.AuthorityTick}");
        return messages;
    }

    private async Task<IReadOnlyList<RoutedOutboundMessage>> RouteFireAsync(
        string connectionId,
        PacketHeader header,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken)
    {
        if (!animationMatchesById.TryGetValue(header.MatchId, out ActiveAnimationMatch? match))
            throw new InvalidDataException($"Fire match is not active: MatchId={header.MatchId}.");
        ServerPlayer player = match.Players.Single(candidate => candidate.ConnectionId == connectionId);
        if (!match.EquipmentByPawnId.TryGetValue(player.ControlledPawnId, out AuthorityActionEquipmentState? equipment))
            throw new InvalidDataException("AuthorityPawnEquipmentMissing");

        FireRequestMessage request = MessagePackSerializer.Deserialize<FireRequestMessage>(payload.ToArray());
        AuthorityFireQueryResult? query = await physicsServerLifecycle.QueryFireAsync(
            header.MatchId,
            new AuthorityFireQuery(
                player.ControlledPawnId,
                request.OriginX,
                request.OriginY,
                request.OriginZ,
                request.DirectionX,
                request.DirectionY,
                request.DirectionZ),
            cancellationToken);
        AuthorityFireProcessor fireProcessor = match.FireByPawnId[player.ControlledPawnId];
        FireResolution resolution = query is { Accepted: true }
            ? fireProcessor.Process(
                request,
                player.ControlledPawnId,
                player.PossessionRevision,
                match.AuthorityTick,
                equipment,
                new AuthorityFireImpact(
                    query.Hit,
                    query.PositionX,
                    query.PositionY,
                    query.PositionZ,
                    query.NormalX,
                    query.NormalY,
                    query.NormalZ,
                    query.SurfaceId))
            : fireProcessor.RejectAuthorityUnavailable(request, equipment.MagazineAmmo);
        if (resolution.Committed is FireCommittedMessage committed)
        {
            var messages = new List<RoutedOutboundMessage>
            {
                new(connectionId, new RoutedMessage(
                    new PacketHeader(ProtocolVersion.Current, MessageId.FireCommitted, PacketFlags.Response, header.RequestId, header.MatchId),
                    MessagePackSerializer.Serialize(committed)))
            };
            foreach (string target in match.ConnectionIds)
                if (target != connectionId) messages.Add(Event(target, header.MatchId, MessageId.FireCommitted, committed));
            Console.WriteLine($"[Server][045] FireCommitted MatchId={header.MatchId} PawnId={committed.PawnId} ShotSequence={committed.ShotSequence} Magazine={committed.AuthoritativeMagazineAmmo} Hit={committed.HasImpact} ImpactId={committed.ImpactId} Surface={committed.SurfaceId}");
            return messages;
        }

        FireRejectedMessage rejected = resolution.Rejected!;
        Console.WriteLine($"[Server][045] FireRejected MatchId={header.MatchId} PawnId={rejected.PawnId} ClientShotSequence={rejected.ClientShotSequence} Reason={rejected.Reason}");
        return Respond(connectionId, header, MessageId.FireRejected, rejected);
    }

    private sealed class ImmediateMatchPhysicsServerLifecycle : IMatchPhysicsServerLifecycle
    {
        public Task<MatchPhysicsServerReady> StartAsync(
            MatchPhysicsServerRequest request,
            CancellationToken cancellationToken) => Task.FromResult(
                new MatchPhysicsServerReady(request.MatchId, "loopback", "development"));

        public Task StopAsync(long matchId, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<AuthorityFireQueryResult?> QueryFireAsync(
            long matchId,
            AuthorityFireQuery query,
            CancellationToken cancellationToken) => Task.FromResult<AuthorityFireQueryResult?>(
            new AuthorityFireQueryResult(true, false, 0f, 0f, 0f, 0f, 0f, 0f, string.Empty, null));
    }

    private sealed class ActiveAnimationMatch
    {
        public ActiveAnimationMatch(IEnumerable<ServerPlayer> players, IEnumerable<string> connectionIds)
        {
            Players = players.ToArray();
            ConnectionIds = connectionIds.ToArray();
            foreach (ServerPlayer player in Players)
            {
                EquipmentByPawnId.Add(player.ControlledPawnId, new AuthorityActionEquipmentState());
                FireByPawnId.Add(player.ControlledPawnId, new AuthorityFireProcessor());
            }
        }

        public ServerPlayer[] Players { get; }
        public string[] ConnectionIds { get; }
        public AuthorityAnimationActionTimeline Timeline { get; } = new();
        public Dictionary<long, AuthorityActionEquipmentState> EquipmentByPawnId { get; } = new();
        public Dictionary<long, AuthorityFireProcessor> FireByPawnId { get; } = new();
        public long AuthorityTick { get; set; }
    }

    private ServerPlayer GetOrCreatePlayer(string connectionId)
    {
        if (playersByConnection.TryGetValue(connectionId, out ServerPlayer? player)) return player;
        player = new ServerPlayer(checked(++nextPlayerId), connectionId);
        playersByConnection.Add(connectionId, player);
        return player;
    }

    private static IReadOnlyList<RoutedOutboundMessage> Respond<T>(string connectionId, PacketHeader requestHeader, MessageId responseMessageId, T response)
    {
        var responseHeader = new PacketHeader(ProtocolVersion.Current, responseMessageId, PacketFlags.Response, requestHeader.RequestId, requestHeader.MatchId);
        return new[] { new RoutedOutboundMessage(connectionId, new RoutedMessage(responseHeader, MessagePackSerializer.Serialize(response))) };
    }

    private static RoutedOutboundMessage Event<T>(string connectionId, long matchId, MessageId messageId, T payload)
    {
        var header = new PacketHeader(ProtocolVersion.Current, messageId, PacketFlags.None, 0, matchId);
        return new RoutedOutboundMessage(connectionId, new RoutedMessage(header, MessagePackSerializer.Serialize(payload)));
    }
}
