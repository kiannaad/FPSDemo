using MessagePack;

namespace Fps.Protocol;

[MessagePackObject]
public readonly record struct QuantizedVector3Message(
    [property: Key(0)] int XMillimeters,
    [property: Key(1)] int YMillimeters,
    [property: Key(2)] int ZMillimeters);

[MessagePackObject]
public readonly record struct QuantizedQuaternionMessage(
    [property: Key(0)] short X,
    [property: Key(1)] short Y,
    [property: Key(2)] short Z,
    [property: Key(3)] short W);

[MessagePackObject]
public readonly record struct QuantizedInputMessage(
    [property: Key(0)] short X,
    [property: Key(1)] short Y);

[MessagePackObject]
public readonly record struct QuantizedViewMessage(
    [property: Key(0)] int YawCentidegrees,
    [property: Key(1)] int PitchCentidegrees);

[Flags]
public enum PawnMoveFlags : byte
{
    None = 0,
    Jump = 1,
    Sprint = 2
}

[MessagePackObject]
public sealed record PawnMoveMessage(
    [property: Key(0)] long MatchId,
    [property: Key(1)] long PawnId,
    [property: Key(2)] long PossessionRevision,
    [property: Key(3)] long Sequence,
    [property: Key(4)] long ClientTick,
    [property: Key(5)] QuantizedInputMessage MovementInput,
    [property: Key(6)] QuantizedViewMessage View,
    [property: Key(7)] PawnMoveFlags Flags,
    [property: Key(8)] QuantizedVector3Message PredictedPosition);

[MessagePackObject]
public sealed record AuthorityStateMessage(
    [property: Key(0)] long ServerTick,
    [property: Key(1)] QuantizedVector3Message Position,
    [property: Key(2)] QuantizedQuaternionMessage Rotation,
    [property: Key(3)] QuantizedVector3Message BaseVelocity,
    [property: Key(4)] byte MovementState,
    [property: Key(5)] bool Grounded,
    [property: Key(6)] QuantizedVector3Message GroundNormal,
    [property: Key(7)] long AttachedBaseId);

[MessagePackObject]
public sealed record AuthoritySnapshotMessage(
    [property: Key(0)] long MatchId,
    [property: Key(1)] long PawnId,
    [property: Key(2)] AuthorityStateMessage State);

public enum OwnerReconcileKind : byte
{
    Ack,
    Correction
}

[MessagePackObject]
public sealed record OwnerReconcileMessage(
    [property: Key(0)] OwnerReconcileKind Kind,
    [property: Key(1)] long AckSequence,
    [property: Key(2)] AuthorityStateMessage AuthorityState,
    [property: Key(3)] string Reason);
