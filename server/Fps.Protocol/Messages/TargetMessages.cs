using MessagePack;

namespace Fps.Protocol;

[MessagePackObject]
public sealed record TargetStateMessage(
    [property: Key(0)] string TargetId,
    [property: Key(1)] long Revision,
    [property: Key(2)] float Health,
    [property: Key(3)] float MaxHealth,
    [property: Key(4)] bool IsDead,
    [property: Key(5)] long CausingPawnId,
    [property: Key(6)] long CausingShotSequence);

[MessagePackObject]
public sealed record TargetStateSnapshotMessage(
    [property: Key(0)] IReadOnlyList<TargetStateMessage> Targets);
