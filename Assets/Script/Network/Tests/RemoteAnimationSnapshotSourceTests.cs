using NUnit.Framework;
using UnityEngine;

namespace CGame.Network.Tests
{
    [Category("Network040")]
    public sealed class RemoteAnimationSnapshotSourceTests
    {
        [Test]
        public void Apply_RebuildsAnimationFactsFromSnapshotOnly()
        {
            var root = new GameObject("RemoteAnimationSnapshotSourceTest");
            try
            {
                var source = new RemoteAnimationSnapshotSource(root.transform);
                AuthorityState state = State(1, 100, 2500, false, (byte)MovementState.Falling);

                Assert.That(source.Apply(state), Is.False);
                Assert.That(source.Velocity.z, Is.EqualTo(2.5f).Within(0.001f));
                Assert.That(source.IsGrounded, Is.False);
                Assert.That(source.MovementState, Is.EqualTo((byte)MovementState.Falling));
                Assert.That(source.AirborneAppliedCount, Is.EqualTo(1));
                Assert.That(source.MovingAppliedCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Apply_HardJumpMarksExactlyOneDiscontinuity()
        {
            var root = new GameObject("RemoteAnimationSnapshotDiscontinuityTest");
            try
            {
                var source = new RemoteAnimationSnapshotSource(root.transform);
                Assert.That(source.Apply(State(1, 0, 0, true, 0)), Is.False);
                Assert.That(source.Apply(State(2, 2000, 0, true, 0)), Is.True);
                Assert.That(source.Apply(State(3, 2050, 0, true, 0)), Is.False);
                Assert.That(source.DiscontinuityCount, Is.EqualTo(1));
                Assert.That(source.GroundedTransitionCount, Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static AuthorityState State(long tick, int x, int velocityZ, bool grounded, byte movementState) =>
            new AuthorityState(
                tick,
                new QuantizedVector3(x, 0, 0),
                new QuantizedQuaternion(0, 0, 0, short.MaxValue),
                new QuantizedVector3(0, 0, velocityZ),
                movementState,
                grounded,
                new QuantizedVector3(0, 1000, 0),
                0);
    }
}
