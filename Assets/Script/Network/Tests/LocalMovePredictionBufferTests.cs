using System.Collections.Generic;
using NUnit.Framework;

namespace CGame.Network.Tests
{
    [Category("Network038")]
    public sealed class LocalMovePredictionBufferTests
    {
        [Test]
        public void Constructor_ForRemoteRole_RejectsSavedMoveOwnership()
        {
            Assert.Throws<System.InvalidOperationException>(() =>
                new LocalMovePredictionBuffer(NetworkPawnRole.RemoteSimulated));
        }

        [Test]
        public void Acknowledge_RemovesOnlyConfirmedPrefix()
        {
            var buffer = new LocalMovePredictionBuffer(NetworkPawnRole.LocalAutonomous);
            buffer.Add(CreateMove(1));
            buffer.Add(CreateMove(2));
            buffer.Add(CreateMove(3));

            buffer.Acknowledge(2);

            Assert.That(buffer.SavedMoves, Has.Count.EqualTo(1));
            Assert.That(buffer.SavedMoves[0].Sequence, Is.EqualTo(3));
        }

        [Test]
        public void Correct_AppliesAuthorityThenReplaysUnacknowledgedMoves()
        {
            var buffer = new LocalMovePredictionBuffer(NetworkPawnRole.LocalAutonomous);
            buffer.Add(CreateMove(1));
            buffer.Add(CreateMove(2));
            buffer.Add(CreateMove(3));
            var target = new RecordingReplayTarget();
            var state = new AuthorityState(50, new QuantizedVector3(100, 0, 0));

            LocalCorrectionResult result = buffer.Correct(1, state, target);

            Assert.That(target.AppliedState, Is.EqualTo(state));
            Assert.That(target.ReplayedSequences, Is.EqualTo(new long[] { 2, 3 }));
            Assert.That(result.ReplayedMoveCount, Is.EqualTo(2));
            Assert.That(buffer.SavedMoves, Has.Count.EqualTo(2));
        }

        [Test]
        public void Correct_WhenHistoryIsMissing_AppliesAuthorityAndClearsHistory()
        {
            var buffer = new LocalMovePredictionBuffer(NetworkPawnRole.LocalAutonomous);
            buffer.Add(CreateMove(5));
            var target = new RecordingReplayTarget();

            LocalCorrectionResult result = buffer.Correct(
                3,
                new AuthorityState(50, new QuantizedVector3(100, 0, 0)),
                target);

            Assert.That(result.HistoryFound, Is.False);
            Assert.That(buffer.SavedMoves, Is.Empty);
            Assert.That(target.ReplayedSequences, Is.Empty);
        }

        private static PawnMove CreateMove(long sequence) => new PawnMove(
            7, 100, 3, sequence, 1000 + sequence,
            new QuantizedInput(100, 0),
            new QuantizedView(0, 0),
            PawnMoveFlags.None,
            new QuantizedVector3((int)sequence, 0, 0));

        private sealed class RecordingReplayTarget : ILocalMoveReplayTarget
        {
            public AuthorityState AppliedState { get; private set; }
            public List<long> ReplayedSequences { get; } = new List<long>();

            public void ApplyAuthorityState(AuthorityState state) => AppliedState = state;
            public void Replay(PawnMove move) => ReplayedSequences.Add(move.Sequence);
        }
    }
}
