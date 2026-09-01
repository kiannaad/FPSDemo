using MessagePack;
using Fps.Protocol;
using Fps.ServerDomain.Matches;
using Fps.ServerDomain.Players;
using Fps.ServerDomain.Rooms;
using Fps.ServerDomain.World;

namespace Fps.ServerNet;

public readonly record struct RoutedMessage(PacketHeader Header, byte[] Payload);
public readonly record struct RoutedOutboundMessage(string ConnectionId, RoutedMessage Message);

public sealed class MessageRouter
{
    private readonly string serverVersion;
    private readonly RoomManager rooms = new();
    private readonly Dictionary<string, ServerPlayer> playersByConnection = new(StringComparer.Ordinal);
    private readonly string[] spawnPointIds;
    private long nextPlayerId;
    private long nextMatchId;

    public MessageRouter(string serverVersion, IEnumerable<string>? spawnPointIds = null)
    {
        if (string.IsNullOrWhiteSpace(serverVersion))
        {
            throw new ArgumentException("Server version is required.", nameof(serverVersion));
        }

        this.serverVersion = serverVersion;
        this.spawnPointIds = (spawnPointIds ?? new[] { "spawn-a", "spawn-b" }).ToArray();
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
        if (string.IsNullOrWhiteSpace(connectionId)) throw new ArgumentException("Connection id is required.", nameof(connectionId));
        if (header.Version != ProtocolVersion.Current || (header.Flags & PacketFlags.Request) == 0)
            throw new InvalidDataException("Only current request packets can be routed.");

        return header.MessageId switch
        {
            MessageId.HelloRequest => new[] { new RoutedOutboundMessage(connectionId, Route(header, payload)) },
            MessageId.CreateRoomRequest => RouteCreateRoom(connectionId, header, payload),
            MessageId.JoinRoomRequest => RouteJoinRoom(connectionId, header, payload),
            MessageId.SetReadyRequest => RouteSetReady(connectionId, header, payload),
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

    private IReadOnlyList<RoutedOutboundMessage> RouteSetReady(string connectionId, PacketHeader header, ReadOnlySpan<byte> payload)
    {
        SetReadyRequest request = MessagePackSerializer.Deserialize<SetReadyRequest>(payload.ToArray());
        ServerRoom room = rooms.SetReady(request.RoomId, connectionId, request.IsReady);
        var messages = new List<RoutedOutboundMessage>(8);
        messages.AddRange(Respond(connectionId, header, MessageId.SetReadyResponse,
            new SetReadyResponse(room.RoomId, room.State.ToString(), room.State == ServerRoomState.Starting)));
        if (room.State != ServerRoomState.Starting) return messages;

        var players = room.ConnectionIds.Select(GetOrCreatePlayer).ToArray();
        ServerMatch match;
        try
        {
            match = new ServerMatch(players, spawnPointIds);
            match.Start();
            rooms.MarkStarted(room.RoomId);
        }
        catch
        {
            rooms.ResetToWaiting(room.RoomId);
            throw;
        }
        long matchId = checked(++nextMatchId);
        foreach (ServerPlayer player in players)
        {
            Console.WriteLine($"[Server][034] MatchStarted MatchId={matchId} ConnectionId={player.ConnectionId} PlayerId={player.PlayerId} ControlledPawnId={player.ControlledPawnId}");
        }

        foreach (string targetConnectionId in room.ConnectionIds)
        {
            messages.Add(Event(targetConnectionId, matchId, MessageId.MatchStarting, new MatchStartingEvent(matchId, 0)));
            foreach (ServerPawn pawn in match.Pawns)
                messages.Add(Event(targetConnectionId, matchId, MessageId.PawnSpawned, new PawnSpawnedEvent(pawn.EntityId, pawn.OwnerPlayerId, pawn.SpawnPointId)));
            foreach (ServerPlayer player in players)
                messages.Add(Event(targetConnectionId, matchId, MessageId.PossessionChanged, new PossessionChangedEvent(player.PlayerId, player.ControlledPawnId, player.PossessionRevision)));
        }

        return messages;
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
