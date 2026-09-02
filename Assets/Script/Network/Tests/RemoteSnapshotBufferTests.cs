using NUnit.Framework;

namespace CGame.Network.Tests
{
    [Category("Network039")]
    public sealed class RemoteSnapshotBufferTests
    {
        [Test]
        public void Sample_InterpolatesContinuousFieldsAndKeepsOlderDiscreteState()
        {
            var buffer = new RemoteSnapshotBuffer();
            buffer.Add(Snapshot(10, 0, 0, false));
            buffer.Add(Snapshot(20, 1000, 100, true));

            Assert.That(buffer.TrySample(15, out AuthorityState sampled), Is.True);
            Assert.That(sampled.Position.XMillimeters, Is.EqualTo(500));
            Assert.That(sampled.BaseVelocity.XMillimeters, Is.EqualTo(50));
            Assert.That(sampled.MovementState, Is.EqualTo(0));
            Assert.That(sampled.Grounded, Is.False);
        }

        [Test]
        public void Add_RejectsDuplicateAndOldServerTicks()
        {
            var buffer = new RemoteSnapshotBuffer();
            Assert.That(buffer.Add(Snapshot(20, 0, 0, false)), Is.True);
            Assert.That(buffer.Add(Snapshot(20, 1, 0, false)), Is.False);
            Assert.That(buffer.Add(Snapshot(19, 1, 0, false)), Is.False);
            Assert.That(buffer.Count, Is.EqualTo(1));
        }

        [Test]
        public void Digest_ReportsTheExactMismatchedField()
        {
            AuthorityState left = Snapshot(20, 1000, 100, true).State;
            AuthorityState right = new AuthorityState(
                left.ServerTick,
                left.Position,
                left.Rotation,
                left.BaseVelocity,
                left.MovementState,
                left.Grounded,
                left.GroundNormal,
                91);

            Assert.That(AuthorityStateDigest.ExactEquals(left, right, out string mismatch), Is.False);
            Assert.That(mismatch, Is.EqualTo("AttachedBaseId"));
        }

        private static AuthoritySnapshot Snapshot(long tick, int x, int velocityX, bool grounded)
        {
            return new AuthoritySnapshot(17, 200, new AuthorityState(
                tick,
                new QuantizedVector3(x, 0, 0),
                new QuantizedQuaternion(0, 0, 0, short.MaxValue),
                new QuantizedVector3(velocityX, 0, 0),
                grounded ? (byte)1 : (byte)0,
                grounded,
                new QuantizedVector3(0, 1000, 0),
                0));
        }
    }
}
