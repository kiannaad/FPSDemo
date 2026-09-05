using MessagePack;

namespace CGame.Network
{
    [MessagePackObject]
    public sealed class EnemySpawnedEvent
    {
        [Key(0)] public long EnemyId { get; set; }
        [Key(1)] public string ArchetypeId { get; set; }
        [Key(2)] public QuantizedVector3WireMessage Position { get; set; }
        [Key(3)] public QuantizedQuaternionWireMessage Rotation { get; set; }
        [Key(4)] public long AuthorityServerTick { get; set; }
        [Key(5)] public int Health { get; set; }
    }

    [MessagePackObject]
    public sealed class EnemySnapshotEvent
    {
        [Key(0)] public long EnemyId { get; set; }
        [Key(1)] public long AuthorityServerTick { get; set; }
        [Key(2)] public QuantizedVector3WireMessage Position { get; set; }
        [Key(3)] public QuantizedQuaternionWireMessage Rotation { get; set; }
        [Key(4)] public QuantizedVector3WireMessage PlanarVelocity { get; set; }
        [Key(5)] public long PoseDiscontinuitySequence { get; set; }
        [Key(6)] public int Health { get; set; }
        [Key(7)] public bool IsGrounded { get; set; }
        [Key(8)] public EnemyBrainState BrainState { get; set; }
        [Key(9)] public long TargetPawnId { get; set; }
        [Key(10)] public string CoverPointId { get; set; }
    }

    public enum EnemyActionKind : byte
    {
        Fire = 1,
        Hit = 2,
        Death = 3,
        NoAmmo = 4
    }

    [MessagePackObject]
    public sealed class EnemyActionEvent
    {
        [Key(0)] public long EnemyId { get; set; }
        [Key(1)] public long ActionSequence { get; set; }
        [Key(2)] public EnemyActionKind ActionKind { get; set; }
        [Key(3)] public long AuthorityServerTick { get; set; }
        [Key(4)] public long PoseDiscontinuitySequence { get; set; }
    }

    [MessagePackObject]
    public sealed class EnemyResyncRequest : System.IEquatable<EnemyResyncRequest>
    {
        [Key(0)] public long EnemyId { get; set; }
        [Key(1)] public long LastKnownAuthorityServerTick { get; set; }

        public bool Equals(EnemyResyncRequest other) => other != null &&
            EnemyId == other.EnemyId && LastKnownAuthorityServerTick == other.LastKnownAuthorityServerTick;

        public override bool Equals(object obj) => Equals(obj as EnemyResyncRequest);
        public override int GetHashCode() => System.HashCode.Combine(EnemyId, LastKnownAuthorityServerTick);
    }

    [MessagePackObject]
    public sealed class OwnerGameplayStateEvent
    {
        [Key(0)] public long PawnId { get; set; }
        [Key(1)] public long VitalsRevision { get; set; }
        [Key(2)] public int Health { get; set; }
        [Key(3)] public int MaxHealth { get; set; }
        [Key(4)] public bool IsDead { get; set; }
        [Key(5)] public long EquipmentRevision { get; set; }
        [Key(6)] public string WeaponName { get; set; }
        [Key(7)] public int MagazineAmmo { get; set; }
        [Key(8)] public int MagazineCapacity { get; set; }
    }

    [MessagePackObject]
    public sealed class QuantizedQuaternionWireMessage
    {
        [Key(0)] public short X { get; set; }
        [Key(1)] public short Y { get; set; }
        [Key(2)] public short Z { get; set; }
        [Key(3)] public short W { get; set; }

        public QuantizedQuaternion ToValue() => new QuantizedQuaternion(X, Y, Z, W);

        public static QuantizedQuaternionWireMessage FromValue(QuantizedQuaternion value) =>
            new QuantizedQuaternionWireMessage { X = value.X, Y = value.Y, Z = value.Z, W = value.W };
    }
}
