using System;

namespace CGame.Network
{
    public enum OwnerReconcileKind
    {
        Ack,
        Correction
    }

    public readonly struct OwnerReconcile
    {
        public OwnerReconcile(
            OwnerReconcileKind kind,
            long ackSequence,
            AuthorityState authorityState,
            string reason)
        {
            Kind = kind;
            AckSequence = ackSequence;
            AuthorityState = authorityState;
            Reason = reason;
        }

        public OwnerReconcileKind Kind { get; }
        public long AckSequence { get; }
        public AuthorityState AuthorityState { get; }
        public string Reason { get; }
    }

    public interface IAuthorityMoveSimulation
    {
        AuthorityState Simulate(PawnMove move, long serverTick);
        AuthorityState Capture(long serverTick);
    }

    public sealed class AuthorityMoveProcessor
    {
        private readonly AuthorityMoveValidator validator;
        private readonly IAuthorityMoveSimulation simulation;
        private readonly int positionErrorThresholdMillimeters;

        public AuthorityMoveProcessor(
            AuthorityMoveValidator validator,
            IAuthorityMoveSimulation simulation,
            int positionErrorThresholdMillimeters)
        {
            this.validator = validator ?? throw new ArgumentNullException(nameof(validator));
            this.simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
            this.positionErrorThresholdMillimeters = positionErrorThresholdMillimeters;
        }

        public OwnerReconcile Process(string connectionId, PawnMove move, long serverTick)
        {
            AuthorityMoveValidation validation = validator.Validate(connectionId, move, serverTick);
            if (!validation.Accepted)
            {
                return new OwnerReconcile(
                    OwnerReconcileKind.Correction,
                    validator.LastAcceptedSequence,
                    simulation.Capture(serverTick),
                    validation.Rejection.ToString());
            }

            AuthorityState authorityState = simulation.Simulate(move, serverTick);
            if (ExceedsPositionThreshold(move.PredictedPosition, authorityState.Position))
            {
                return new OwnerReconcile(
                    OwnerReconcileKind.Correction,
                    move.Sequence,
                    authorityState,
                    "PositionError");
            }

            return new OwnerReconcile(OwnerReconcileKind.Ack, move.Sequence, authorityState, null);
        }

        public AuthorityMoveValidation Validate(string connectionId, PawnMove move, long serverTick) =>
            validator.Validate(connectionId, move, serverTick);

        public OwnerReconcile Reject(AuthorityMoveValidation validation, long serverTick) =>
            new OwnerReconcile(
                OwnerReconcileKind.Correction,
                validator.LastAcceptedSequence,
                simulation.Capture(serverTick),
                validation.Rejection.ToString());

        public OwnerReconcile ReconcileAccepted(PawnMove move, AuthorityState authorityState)
        {
            if (ExceedsPositionThreshold(move.PredictedPosition, authorityState.Position))
            {
                return new OwnerReconcile(
                    OwnerReconcileKind.Correction,
                    move.Sequence,
                    authorityState,
                    "PositionError");
            }

            return new OwnerReconcile(OwnerReconcileKind.Ack, move.Sequence, authorityState, null);
        }

        public AuthorityState Capture(long serverTick) => simulation.Capture(serverTick);

        private bool ExceedsPositionThreshold(QuantizedVector3 predicted, QuantizedVector3 authority)
        {
            long x = (long)predicted.XMillimeters - authority.XMillimeters;
            long y = (long)predicted.YMillimeters - authority.YMillimeters;
            long z = (long)predicted.ZMillimeters - authority.ZMillimeters;
            long threshold = positionErrorThresholdMillimeters;
            return x * x + y * y + z * z > threshold * threshold;
        }
    }
}
