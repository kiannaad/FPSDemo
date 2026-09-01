using MessagePack;

namespace Fps.Protocol;

[MessagePackObject]
public sealed record CreateRoomRequest([property: Key(0)] string RequestedRoomName);

[MessagePackObject]
public sealed record CreateRoomResponse(
    [property: Key(0)] string RoomId,
    [property: Key(1)] long PlayerId,
    [property: Key(2)] string State);

[MessagePackObject]
public sealed record JoinRoomRequest([property: Key(0)] string RoomId);

[MessagePackObject]
public sealed record JoinRoomResponse(
    [property: Key(0)] string RoomId,
    [property: Key(1)] long PlayerId,
    [property: Key(2)] string State);

[MessagePackObject]
public sealed record SetReadyRequest(
    [property: Key(0)] string RoomId,
    [property: Key(1)] bool IsReady);

[MessagePackObject]
public sealed record SetReadyResponse(
    [property: Key(0)] string RoomId,
    [property: Key(1)] string State,
    [property: Key(2)] bool MatchStarted);
