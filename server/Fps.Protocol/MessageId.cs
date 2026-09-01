namespace Fps.Protocol;

public enum MessageId : ushort
{
    HelloRequest = 1,
    HelloResponse = 2,
    CreateRoomRequest = 10,
    CreateRoomResponse = 11,
    JoinRoomRequest = 12,
    JoinRoomResponse = 13,
    SetReadyRequest = 14,
    SetReadyResponse = 15,
    MatchStarting = 20,
    PawnSpawned = 21,
    PossessionChanged = 22
}
