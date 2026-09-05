using MessagePack;

namespace CGame.Network
{
    [MessagePackObject]
    public sealed class PawnMoveWireMessage
    {
        [Key(0)] public long MatchId { get; set; }
        [Key(1)] public long PawnId { get; set; }
        [Key(2)] public long PossessionRevision { get; set; }
        [Key(3)] public long Sequence { get; set; }
        [Key(4)] public long ClientTick { get; set; }
        [Key(5)] public QuantizedInputWireMessage MovementInput { get; set; }
        [Key(6)] public QuantizedViewWireMessage View { get; set; }
        [Key(7)] public PawnMoveFlags Flags { get; set; }
        [Key(8)] public QuantizedVector3WireMessage PredictedPosition { get; set; }

        public PawnMove ToMove() => new PawnMove(
            MatchId,
            PawnId,
            PossessionRevision,
            Sequence,
            ClientTick,
            MovementInput.ToValue(),
            View.ToValue(),
            Flags,
            PredictedPosition.ToValue());

        public static PawnMoveWireMessage FromMove(PawnMove move) => new PawnMoveWireMessage
        {
            MatchId = move.MatchId,
            PawnId = move.PawnId,
            PossessionRevision = move.PossessionRevision,
            Sequence = move.Sequence,
            ClientTick = move.ClientTick,
            MovementInput = QuantizedInputWireMessage.FromValue(move.MovementInput),
            View = QuantizedViewWireMessage.FromValue(move.View),
            Flags = move.Flags,
            PredictedPosition = QuantizedVector3WireMessage.FromValue(move.PredictedPosition)
        };
    }

    [MessagePackObject]
    public sealed class QuantizedInputWireMessage
    {
        [Key(0)] public short X { get; set; }
        [Key(1)] public short Y { get; set; }
        public QuantizedInput ToValue() => new QuantizedInput(X, Y);
        public static QuantizedInputWireMessage FromValue(QuantizedInput value) =>
            new QuantizedInputWireMessage { X = value.X, Y = value.Y };
    }

    [MessagePackObject]
    public sealed class QuantizedViewWireMessage
    {
        [Key(0)] public int YawCentidegrees { get; set; }
        [Key(1)] public int PitchCentidegrees { get; set; }
        public QuantizedView ToValue() => new QuantizedView(YawCentidegrees, PitchCentidegrees);
        public static QuantizedViewWireMessage FromValue(QuantizedView value) => new QuantizedViewWireMessage
        {
            YawCentidegrees = value.YawCentidegrees,
            PitchCentidegrees = value.PitchCentidegrees
        };
    }

    [MessagePackObject]
    public sealed class QuantizedVector3WireMessage
    {
        [Key(0)] public int XMillimeters { get; set; }
        [Key(1)] public int YMillimeters { get; set; }
        [Key(2)] public int ZMillimeters { get; set; }
        public QuantizedVector3 ToValue() => new QuantizedVector3(XMillimeters, YMillimeters, ZMillimeters);

        public static QuantizedVector3WireMessage FromValue(QuantizedVector3 value) => new QuantizedVector3WireMessage
        {
            XMillimeters = value.XMillimeters,
            YMillimeters = value.YMillimeters,
            ZMillimeters = value.ZMillimeters
        };
    }

    [MessagePackObject]
    public sealed class OwnerReconcileWireMessage
    {
        [Key(0)] public OwnerReconcileKind Kind { get; set; }
        [Key(1)] public long AckSequence { get; set; }
        [Key(2)] public AuthorityStateWireMessage AuthorityState { get; set; }
        [Key(3)] public string Reason { get; set; }

        public OwnerReconcile ToValue() => new OwnerReconcile(
            Kind,
            AckSequence,
            AuthorityState.ToValue(),
            Reason);
    }

    [MessagePackObject]
    public sealed class AuthorityStateWireMessage
    {
        [Key(0)] public long ServerTick { get; set; }
        [Key(1)] public QuantizedVector3WireMessage Position { get; set; }
        [Key(2)] public short[] Rotation { get; set; } = { 0, 0, 0, short.MaxValue };
        [Key(3)] public QuantizedVector3WireMessage BaseVelocity { get; set; } = new QuantizedVector3WireMessage();
        [Key(4)] public byte MovementState { get; set; }
        [Key(5)] public bool Grounded { get; set; }
        [Key(6)] public QuantizedVector3WireMessage GroundNormal { get; set; } = new QuantizedVector3WireMessage { YMillimeters = 1000 };
        [Key(7)] public long AttachedBaseId { get; set; }
        [Key(8)] public short[] ControlRotation { get; set; } = { 0, 0, 0, short.MaxValue };
        [Key(9)] public bool IsAiming { get; set; }

        public static AuthorityStateWireMessage FromValue(AuthorityState state) => new AuthorityStateWireMessage
        {
            ServerTick = state.ServerTick,
            Position = QuantizedVector3WireMessage.FromValue(state.Position),
            Rotation = new[] { state.Rotation.X, state.Rotation.Y, state.Rotation.Z, state.Rotation.W },
            BaseVelocity = QuantizedVector3WireMessage.FromValue(state.BaseVelocity),
            MovementState = state.MovementState,
            Grounded = state.Grounded,
            GroundNormal = QuantizedVector3WireMessage.FromValue(state.GroundNormal),
            AttachedBaseId = state.AttachedBaseId,
            ControlRotation = new[] { state.ControlRotation.X, state.ControlRotation.Y, state.ControlRotation.Z, state.ControlRotation.W },
            IsAiming = state.IsAiming
        };

        public AuthorityState ToValue() => new AuthorityState(
            ServerTick,
            Position.ToValue(),
            new QuantizedQuaternion(Rotation[0], Rotation[1], Rotation[2], Rotation[3]),
            BaseVelocity.ToValue(),
            MovementState,
            Grounded,
            GroundNormal.ToValue(),
            AttachedBaseId,
            new QuantizedQuaternion(ControlRotation[0], ControlRotation[1], ControlRotation[2], ControlRotation[3]),
            IsAiming);
    }

    [MessagePackObject]
    public sealed class AuthoritySnapshotWireMessage
    {
        [Key(0)] public long MatchId { get; set; }
        [Key(1)] public long PawnId { get; set; }
        [Key(2)] public AuthorityStateWireMessage State { get; set; }

        public AuthoritySnapshot ToValue() => new AuthoritySnapshot(MatchId, PawnId, State.ToValue());

        public static AuthoritySnapshotWireMessage FromValue(AuthoritySnapshot snapshot) => new AuthoritySnapshotWireMessage
        {
            MatchId = snapshot.MatchId,
            PawnId = snapshot.PawnId,
            State = AuthorityStateWireMessage.FromValue(snapshot.State)
        };
    }
}
