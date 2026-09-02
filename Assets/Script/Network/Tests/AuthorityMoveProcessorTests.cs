using NUnit.Framework;

namespace CGame.Network.Tests
{
    [Category("Network038")]
    public sealed class AuthorityMoveProcessorTests
    {
        [Test]
        public void Process_WithinPositionThreshold_SimulatesOnceAndAcknowledges()
        {
            var simulation = new RecordingAuthoritySimulation(new AuthorityState(101, new QuantizedVector3(1040, 0, 0)));
            var processor = new AuthorityMoveProcessor(
                new AuthorityMoveValidator("connection-a", 7, 100, 3),
                simulation,
                positionErrorThresholdMillimeters: 50);

            OwnerReconcile reconcile = processor.Process("connection-a", CreateMove(1, 101, 1000), 101);

            Assert.That(simulation.SimulatedSequences, Is.EqualTo(new long[] { 1 }));
            Assert.That(reconcile.Kind, Is.EqualTo(OwnerReconcileKind.Ack));
            Assert.That(reconcile.AckSequence, Is.EqualTo(1));
        }

        [Test]
        public void Process_OutsidePositionThreshold_ReturnsCorrection()
        {
            var authority = new AuthorityState(101, new QuantizedVector3(1100, 0, 0));
            var processor = new AuthorityMoveProcessor(
                new AuthorityMoveValidator("connection-a", 7, 100, 3),
                new RecordingAuthoritySimulation(authority),
                positionErrorThresholdMillimeters: 50);

            OwnerReconcile reconcile = processor.Process("connection-a", CreateMove(1, 101, 1000), 101);

            Assert.That(reconcile.Kind, Is.EqualTo(OwnerReconcileKind.Correction));
            Assert.That(reconcile.AuthorityState, Is.EqualTo(authority));
            Assert.That(reconcile.Reason, Is.EqualTo("PositionError"));
        }

        [Test]
        public void Process_InvalidMove_DoesNotSimulateAndReturnsRejectedCorrection()
        {
            var simulation = new RecordingAuthoritySimulation(new AuthorityState());
            var processor = new AuthorityMoveProcessor(
                new AuthorityMoveValidator("connection-a", 7, 100, 3),
                simulation,
                50);

            OwnerReconcile reconcile = processor.Process("connection-b", CreateMove(1, 101, 1000), 101);

            Assert.That(simulation.SimulatedSequences, Is.Empty);
            Assert.That(reconcile.Kind, Is.EqualTo(OwnerReconcileKind.Correction));
            Assert.That(reconcile.Reason, Is.EqualTo(AuthorityMoveRejection.Connection.ToString()));
        }

        private static PawnMove CreateMove(long sequence, long tick, int predictedX) => new PawnMove(
            7, 100, 3, sequence, tick,
            new QuantizedInput(100, 0), new QuantizedView(0, 0), PawnMoveFlags.None,
            new QuantizedVector3(predictedX, 0, 0));

        private sealed class RecordingAuthoritySimulation : IAuthorityMoveSimulation
        {
            private readonly AuthorityState state;
            public RecordingAuthoritySimulation(AuthorityState state) { this.state = state; }
            public System.Collections.Generic.List<long> SimulatedSequences { get; } = new();
            public AuthorityState Simulate(PawnMove move, long serverTick)
            {
                SimulatedSequences.Add(move.Sequence);
                return state;
            }
            public AuthorityState Capture(long serverTick) => state;
        }
    }
}
