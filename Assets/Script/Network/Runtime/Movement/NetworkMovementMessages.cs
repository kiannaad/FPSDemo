using System;

namespace CGame.Network
{
    [Flags]
    public enum PawnMoveFlags : byte
    {
        None = 0,
        Jump = 1,
        Sprint = 2
    }

    public readonly struct PawnMove
    {
        public PawnMove(
            long matchId,
            long pawnId,
            long possessionRevision,
            long sequence,
            long clientTick,
            QuantizedInput movementInput,
            QuantizedView view,
            PawnMoveFlags flags,
            QuantizedVector3 predictedPosition)
        {
            MatchId = matchId;
            PawnId = pawnId;
            PossessionRevision = possessionRevision;
            Sequence = sequence;
            ClientTick = clientTick;
            MovementInput = movementInput;
            View = view;
            Flags = flags;
            PredictedPosition = predictedPosition;
        }

        public long MatchId { get; }
        public long PawnId { get; }
        public long PossessionRevision { get; }
        public long Sequence { get; }
        public long ClientTick { get; }
        public QuantizedInput MovementInput { get; }
        public QuantizedView View { get; }
        public PawnMoveFlags Flags { get; }
        public QuantizedVector3 PredictedPosition { get; }
    }

    public readonly struct AuthorityState : IEquatable<AuthorityState>
    {
        public AuthorityState(long serverTick, QuantizedVector3 position)
            : this(serverTick, position, new QuantizedQuaternion(0, 0, 0, short.MaxValue), default, 0, false, new QuantizedVector3(0, 1000, 0), 0)
        {
        }

        public AuthorityState(
            long serverTick,
            QuantizedVector3 position,
            QuantizedQuaternion rotation,
            QuantizedVector3 baseVelocity,
            byte movementState,
            bool grounded,
            QuantizedVector3 groundNormal,
            long attachedBaseId)
        {
            ServerTick = serverTick;
            Position = position;
            Rotation = rotation;
            BaseVelocity = baseVelocity;
            MovementState = movementState;
            Grounded = grounded;
            GroundNormal = groundNormal;
            AttachedBaseId = attachedBaseId;
        }

        public long ServerTick { get; }
        public QuantizedVector3 Position { get; }
        public QuantizedQuaternion Rotation { get; }
        public QuantizedVector3 BaseVelocity { get; }
        public byte MovementState { get; }
        public bool Grounded { get; }
        public QuantizedVector3 GroundNormal { get; }
        public long AttachedBaseId { get; }
        public bool Equals(AuthorityState other) =>
            ServerTick == other.ServerTick && Position.Equals(other.Position) && Rotation.Equals(other.Rotation) &&
            BaseVelocity.Equals(other.BaseVelocity) && MovementState == other.MovementState && Grounded == other.Grounded &&
            GroundNormal.Equals(other.GroundNormal) && AttachedBaseId == other.AttachedBaseId;
        public override bool Equals(object obj) => obj is AuthorityState other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(ServerTick, Position, Rotation, BaseVelocity, MovementState, Grounded, GroundNormal, AttachedBaseId);
    }

    public readonly struct AuthoritySnapshot
    {
        public AuthoritySnapshot(long matchId, long pawnId, AuthorityState state)
        {
            MatchId = matchId;
            PawnId = pawnId;
            State = state;
        }

        public long MatchId { get; }
        public long PawnId { get; }
        public AuthorityState State { get; }
    }
}
