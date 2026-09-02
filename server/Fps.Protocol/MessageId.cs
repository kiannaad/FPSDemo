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
    PossessionChanged = 22,
    PawnMove = 30,
    OwnerReconcile = 31,
    AuthoritySnapshot = 32,
    AnimationActionRequest = 40,
    AnimationActionStarted = 41,
    AnimationActionCommit = 42,
    AnimationActionEnded = 43,
    AnimationActionCancelled = 44,
    FireRequest = 50,
    FireCommitted = 51,
    FireRejected = 52
}
