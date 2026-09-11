namespace Fps.ServerDomain.Rooms;

public enum ServerRoomState
{
    Waiting,
    Starting,
    Started
}

public sealed class ServerRoom
{
    private readonly List<string> connectionIds = new();
    private readonly HashSet<string> readyConnectionIds = new(StringComparer.Ordinal);

    internal ServerRoom(string roomId, string ownerConnectionId)
    {
        RoomId = roomId;
        connectionIds.Add(ownerConnectionId);
    }

    public string RoomId { get; }
    public ServerRoomState State { get; private set; } = ServerRoomState.Waiting;
    public IReadOnlyList<string> ConnectionIds => connectionIds;
    public IReadOnlySet<string> ReadyConnectionIds => readyConnectionIds;

    internal void Join(string connectionId)
    {
        if (State != ServerRoomState.Waiting) throw new InvalidOperationException("The room is no longer joinable.");
        if (connectionIds.Contains(connectionId, StringComparer.Ordinal)) return;
        if (connectionIds.Count >= 2) throw new InvalidOperationException("The room is full.");
        connectionIds.Add(connectionId);
    }

    internal void SetReady(string connectionId, bool ready)
    {
        if (!connectionIds.Contains(connectionId, StringComparer.Ordinal)) throw new InvalidOperationException("Connection does not belong to the room.");
        if (State != ServerRoomState.Waiting) return;
        if (ready) readyConnectionIds.Add(connectionId);
        else readyConnectionIds.Remove(connectionId);
        if (connectionIds.Count > 0 && readyConnectionIds.Count == connectionIds.Count) State = ServerRoomState.Starting;
    }

    internal void MarkStarted()
    {
        if (State != ServerRoomState.Starting) throw new InvalidOperationException("The room is not ready to start.");
        State = ServerRoomState.Started;
    }

    internal void ResetToWaiting()
    {
        if (State != ServerRoomState.Starting) return;
        readyConnectionIds.Clear();
        State = ServerRoomState.Waiting;
    }
}

public sealed class RoomManager
{
    private readonly Dictionary<string, ServerRoom> roomsById = new(StringComparer.Ordinal);
    private long nextRoomId;

    public ServerRoom CreateRoom(string connectionId)
    {
        if (string.IsNullOrWhiteSpace(connectionId)) throw new ArgumentException("Connection id is required.", nameof(connectionId));
        string roomId = $"room-{checked(++nextRoomId)}";
        var room = new ServerRoom(roomId, connectionId);
        roomsById.Add(roomId, room);
        return room;
    }

    public ServerRoom JoinRoom(string roomId, string connectionId)
    {
        ServerRoom room = GetRoom(roomId);
        room.Join(connectionId);
        return room;
    }

    public ServerRoom SetReady(string roomId, string connectionId, bool ready)
    {
        ServerRoom room = GetRoom(roomId);
        room.SetReady(connectionId, ready);
        return room;
    }

    public void MarkStarted(string roomId) => GetRoom(roomId).MarkStarted();

    public void ResetToWaiting(string roomId) => GetRoom(roomId).ResetToWaiting();

    public ServerRoomState GetRoomState(string roomId) => GetRoom(roomId).State;

    private ServerRoom GetRoom(string roomId)
    {
        if (string.IsNullOrWhiteSpace(roomId) || !roomsById.TryGetValue(roomId, out ServerRoom? room))
            throw new InvalidOperationException("The room does not exist.");
        return room;
    }
}
