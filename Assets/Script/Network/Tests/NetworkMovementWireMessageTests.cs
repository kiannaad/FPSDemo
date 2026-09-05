using NUnit.Framework;

namespace CGame.Network.Tests
{
    [Category("Network038")]
    public sealed class NetworkMovementWireMessageTests
    {
        [Test]
        public void PawnMoveWireMessage_DeserializesProtocolShape()
        {
            byte[] bytes = NetworkMessageSerializer.Serialize(new object[]
            {
                17L, 200L, 9L, 3L, 44L,
                new object[] { (short)120, (short)-80 },
                new object[] { 9000, -1200 },
                (byte)PawnMoveFlags.Sprint,
                new object[] { 1000, 2000, -3000 }
            });

            PawnMove move = NetworkMessageSerializer.Deserialize<PawnMoveWireMessage>(bytes).ToMove();

            Assert.That(move.MatchId, Is.EqualTo(17));
            Assert.That(move.PawnId, Is.EqualTo(200));
            Assert.That(move.PossessionRevision, Is.EqualTo(9));
            Assert.That(move.PredictedPosition.ZMillimeters, Is.EqualTo(-3000));
        }

        [Test]
        public void AuthoritySnapshotWireMessage_DeserializesProtocolShape()
        {
            byte[] bytes = NetworkMessageSerializer.Serialize(new object[]
            {
                17L,
                200L,
                new object[]
                {
                    177L,
                    new object[] { 101, 202, 303 },
                    new object[] { (short)1, (short)2, (short)3, (short)32760 },
                    new object[] { -4, 5, -6 },
                    (byte)2,
                    true,
                    new object[] { 7, 999, 8 },
                    91L,
                    new object[] { (short)9, (short)8, (short)7, (short)32760 },
                    true
                }
            });

            AuthoritySnapshot snapshot = NetworkMessageSerializer
                .Deserialize<AuthoritySnapshotWireMessage>(bytes)
                .ToValue();

            Assert.That(snapshot.MatchId, Is.EqualTo(17));
            Assert.That(snapshot.PawnId, Is.EqualTo(200));
            Assert.That(snapshot.State.ServerTick, Is.EqualTo(177));
            Assert.That(snapshot.State.AttachedBaseId, Is.EqualTo(91));
            Assert.That(snapshot.State.ControlRotation.X, Is.EqualTo(9));
            Assert.That(snapshot.State.IsAiming, Is.True);
        }
    }
}
