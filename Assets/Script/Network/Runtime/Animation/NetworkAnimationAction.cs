using MessagePack;

namespace CGame.Network
{
    public enum NetworkAnimationActionKind : byte
    {
        Reload = 1,
        Melee = 2,
        Equip = 3,
        Unequip = 4,
        Recoil = 5
    }

    public enum NetworkAnimationActionTerminalKind : byte
    {
        Committed = 1,
        Ended = 2,
        Cancelled = 3
    }

    [MessagePackObject]
    public sealed class NetworkAnimationActionRequest
    {
        [Key(0)] public long PawnId { get; set; }
        [Key(1)] public long PossessionRevision { get; set; }
        [Key(2)] public long PredictionNonce { get; set; }
        [Key(3)] public NetworkAnimationActionKind ActionKind { get; set; }
        [Key(4)] public string VariantId { get; set; }
        [Key(5)] public long EquipmentInstanceId { get; set; }
        [Key(6)] public int DurationTicks { get; set; }
        [Key(7)] public int? CommitOffsetTicks { get; set; }
    }

    [MessagePackObject]
    public sealed class NetworkAnimationActionStarted
    {
        [Key(0)] public long PawnId { get; set; }
        [Key(1)] public long PossessionRevision { get; set; }
        [Key(2)] public long PredictionNonce { get; set; }
        [Key(3)] public long ActionSequence { get; set; }
        [Key(4)] public long ServerStartTick { get; set; }
        [Key(5)] public int DurationTicks { get; set; }
        [Key(6)] public long? CommitTick { get; set; }
        [Key(7)] public NetworkAnimationActionKind ActionKind { get; set; }
        [Key(8)] public string VariantId { get; set; }
        [Key(9)] public long EquipmentInstanceId { get; set; }
    }

    [MessagePackObject]
    public sealed class NetworkAnimationActionTerminal
    {
        [Key(0)] public long PawnId { get; set; }
        [Key(1)] public long PossessionRevision { get; set; }
        [Key(2)] public long ActionSequence { get; set; }
        [Key(3)] public long ServerTick { get; set; }
        [Key(4)] public NetworkAnimationActionTerminalKind TerminalKind { get; set; }
        [Key(5)] public int? AuthoritativeMagazineAmmo { get; set; }
        [Key(6)] public int? AuthoritativeReserveAmmo { get; set; }
    }
}
