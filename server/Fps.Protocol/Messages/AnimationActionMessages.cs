using MessagePack;

namespace Fps.Protocol;

public enum NetworkAnimationActionKind : byte
{
    Reload = 1,
    Melee = 2,
    Equip = 3,
    Unequip = 4
}

public enum NetworkAnimationActionTerminalKind : byte
{
    Committed = 1,
    Ended = 2,
    Cancelled = 3
}

[MessagePackObject]
public sealed record NetworkAnimationActionRequestMessage(
    [property: Key(0)] long PawnId,
    [property: Key(1)] long PossessionRevision,
    [property: Key(2)] long PredictionNonce,
    [property: Key(3)] NetworkAnimationActionKind ActionKind,
    [property: Key(4)] string VariantId,
    [property: Key(5)] long EquipmentInstanceId);

[MessagePackObject]
public sealed record NetworkAnimationActionStartedMessage(
    [property: Key(0)] long PawnId,
    [property: Key(1)] long PossessionRevision,
    [property: Key(2)] long PredictionNonce,
    [property: Key(3)] long ActionSequence,
    [property: Key(4)] long ServerStartTick,
    [property: Key(5)] int DurationTicks,
    [property: Key(6)] long? CommitTick,
    [property: Key(7)] NetworkAnimationActionKind ActionKind,
    [property: Key(8)] string VariantId,
    [property: Key(9)] long EquipmentInstanceId);

[MessagePackObject]
public sealed record NetworkAnimationActionTerminalMessage(
    [property: Key(0)] long PawnId,
    [property: Key(1)] long PossessionRevision,
    [property: Key(2)] long ActionSequence,
    [property: Key(3)] long ServerTick,
    [property: Key(4)] NetworkAnimationActionTerminalKind TerminalKind);
