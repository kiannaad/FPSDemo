using MessagePack;

namespace Fps.Protocol;

[MessagePackObject]
public sealed record EnemySpawnedMessage(
    [property: Key(0)] long EnemyId,
    [property: Key(1)] string ArchetypeId,
    [property: Key(2)] QuantizedVector3Message Position,
    [property: Key(3)] QuantizedQuaternionMessage Rotation,
    [property: Key(4)] long AuthorityServerTick,
    [property: Key(5)] int Health);

[MessagePackObject]
public sealed record EnemySnapshotMessage(
    [property: Key(0)] long EnemyId,
    [property: Key(1)] long AuthorityServerTick,
    [property: Key(2)] QuantizedVector3Message Position,
    [property: Key(3)] QuantizedQuaternionMessage Rotation,
    [property: Key(4)] QuantizedVector3Message PlanarVelocity,
    [property: Key(5)] long PoseDiscontinuitySequence,
    [property: Key(6)] int Health);

public enum EnemyActionKind : byte
{
    Fire = 1,
    Hit = 2,
    Death = 3
}

[MessagePackObject]
public sealed record EnemyActionMessage(
    [property: Key(0)] long EnemyId,
    [property: Key(1)] long ActionSequence,
    [property: Key(2)] EnemyActionKind ActionKind,
    [property: Key(3)] long AuthorityServerTick,
    [property: Key(4)] long PoseDiscontinuitySequence);

[MessagePackObject]
public sealed record EnemyResyncRequestMessage(
    [property: Key(0)] long EnemyId,
    [property: Key(1)] long LastKnownAuthorityServerTick);
