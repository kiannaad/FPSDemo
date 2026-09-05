using NUnit.Framework;

namespace CGame.Network.Tests
{
    public sealed class AuthorityMoveInboxTests
    {
        [Test]
        public void ConsecutiveMovesReceivedBeforeTick_AreDequeuedInOrderWithoutLoss()
        {
            var inbox = new AuthorityMoveInbox();
            var identity = new DedicatedDataIdentity(7, 3, "connection");
            inbox.Enqueue(identity, Move(7, 41));
            inbox.Enqueue(identity, Move(7, 42));

            Assert.That(inbox.TryDequeue(out AcceptedAuthorityMove first), Is.True);
            Assert.That(inbox.TryDequeue(out AcceptedAuthorityMove second), Is.True);
            Assert.That(first.Move.Sequence, Is.EqualTo(41));
            Assert.That(second.Move.Sequence, Is.EqualTo(42));
            Assert.That(inbox.Count, Is.Zero);
        }

        [Test]
        public void SimulationStepCount_BacklogExceedsTarget_IsCapped()
        {
            var inbox = new AuthorityMoveInbox();
            var identity = new DedicatedDataIdentity(7, 3, "connection");
            for (long sequence = 1; sequence <= 19; sequence++) inbox.Enqueue(identity, Move(7, sequence));

            int stepCount = inbox.GetSimulationStepCount(targetQueuedMoves: 2, maxSimulationSteps: 4);

            Assert.That(stepCount, Is.EqualTo(4));
        }

        private static PawnMove Move(long pawnId, long sequence) => new PawnMove(
            1,
            pawnId,
            3,
            sequence,
            sequence,
            default,
            default,
            PawnMoveFlags.None,
            default);
    }
}
