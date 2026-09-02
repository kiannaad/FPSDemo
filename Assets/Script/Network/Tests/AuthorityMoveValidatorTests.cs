using NUnit.Framework;

namespace CGame.Network.Tests
{
    [Category("Network038")]
    public sealed class AuthorityMoveValidatorTests
    {
        [Test]
        public void Validate_ValidFirstMove_AcceptsExactlyOnce()
        {
            AuthorityMoveValidator validator = CreateValidator();
            PawnMove move = CreateMove(sequence: 1, clientTick: 100);

            AuthorityMoveValidation first = validator.Validate("connection-a", move, serverTick: 100);
            AuthorityMoveValidation duplicate = validator.Validate("connection-a", move, serverTick: 100);

            Assert.That(first.Accepted, Is.True);
            Assert.That(duplicate.Rejection, Is.EqualTo(AuthorityMoveRejection.DuplicateSequence));
        }

        [TestCase("connection-b", 7, 100, 3, 2, 101, AuthorityMoveRejection.Connection)]
        [TestCase("connection-a", 8, 100, 3, 2, 101, AuthorityMoveRejection.Match)]
        [TestCase("connection-a", 7, 200, 3, 2, 101, AuthorityMoveRejection.Pawn)]
        [TestCase("connection-a", 7, 100, 2, 2, 101, AuthorityMoveRejection.PossessionRevision)]
        public void Validate_InvalidIdentityOrSequence_RejectsWithoutAdvancing(
            string connectionId,
            long matchId,
            long pawnId,
            long revision,
            long sequence,
            long clientTick,
            AuthorityMoveRejection expected)
        {
            AuthorityMoveValidator validator = CreateValidator();

            AuthorityMoveValidation result = validator.Validate(
                connectionId,
                CreateMove(matchId, pawnId, revision, sequence, clientTick),
                serverTick: 101);

            Assert.That(result.Rejection, Is.EqualTo(expected));
            Assert.That(validator.LastAcceptedSequence, Is.Zero);
        }

        [Test]
        public void Validate_SequencedTransportGap_AcceptsNewestMove()
        {
            AuthorityMoveValidator validator = CreateValidator();

            AuthorityMoveValidation result = validator.Validate(
                "connection-a",
                CreateMove(sequence: 3, clientTick: 101),
                serverTick: 200);

            Assert.That(result.Accepted, Is.True);
            Assert.That(validator.LastAcceptedSequence, Is.EqualTo(3));
        }

        [Test]
        public void Validate_LowerObservedOffset_RecalibratesWithoutRejectingJitter()
        {
            AuthorityMoveValidator validator = CreateValidator();
            Assert.That(validator.Validate(
                "connection-a",
                CreateMove(sequence: 1, clientTick: 1),
                serverTick: 30).Accepted, Is.True);

            AuthorityMoveValidation result = validator.Validate(
                "connection-a",
                CreateMove(sequence: 10, clientTick: 10),
                serverTick: 31);

            Assert.That(result.Accepted, Is.True);
            Assert.That(validator.LastAcceptedClientTick, Is.EqualTo(10));
        }

        [Test]
        public void Validate_ClientTemporarilyFallsBehindAuthority_AcceptsNewestIncrementalMove()
        {
            AuthorityMoveValidator validator = CreateValidator();
            Assert.That(validator.Validate(
                "connection-a",
                CreateMove(sequence: 1, clientTick: 100),
                serverTick: 101).Accepted, Is.True);

            AuthorityMoveValidation result = validator.Validate(
                "connection-a",
                CreateMove(sequence: 2, clientTick: 101),
                serverTick: 130);

            Assert.That(result.Accepted, Is.True);
            Assert.That(validator.LastAcceptedSequence, Is.EqualTo(2));
        }

        [TestCase(79)]
        [TestCase(104)]
        public void Validate_DiagnosticTickOutsideAuthorityWindow_AcceptsMonotonicSequence(long clientTick)
        {
            AuthorityMoveValidator validator = CreateValidator();
            Assert.That(validator.Validate(
                "connection-a",
                CreateMove(sequence: 1, clientTick: 100),
                serverTick: 101).Accepted, Is.True);

            AuthorityMoveValidation result = validator.Validate(
                "connection-a",
                CreateMove(sequence: 2, clientTick: clientTick),
                serverTick: 101);

            Assert.That(result.Accepted, Is.True);
            Assert.That(validator.LastAcceptedSequence, Is.EqualTo(2));
            Assert.That(validator.LastAcceptedClientTick, Is.EqualTo(clientTick));
        }

        private static AuthorityMoveValidator CreateValidator() =>
            new AuthorityMoveValidator("connection-a", 7, 100, 3);

        private static PawnMove CreateMove(long sequence, long clientTick) =>
            CreateMove(7, 100, 3, sequence, clientTick);

        private static PawnMove CreateMove(
            long matchId,
            long pawnId,
            long revision,
            long sequence,
            long clientTick) => new PawnMove(
                matchId, pawnId, revision, sequence, clientTick,
                new QuantizedInput(100, 0),
                new QuantizedView(0, 0),
                PawnMoveFlags.None,
                new QuantizedVector3(0, 0, 0));
    }
}
