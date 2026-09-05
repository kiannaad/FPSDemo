using NUnit.Framework;
using UnityEngine;
using CGame;

namespace CGame.Network.Tests
{
    [Category("Network040")]
    public sealed class RemoteSnapshotPresentationTests
    {
        [Test]
        public void Apply_SynchronizesRemoteAnimationFactsWithAuthorityState()
        {
            var root = new GameObject("RemoteSnapshotPresentationTest");
            try
            {
                var pawn = new Pawn(root);
                var presentation = new RemoteSnapshotPresentation(pawn);
                Quaternion authorityRotation = Quaternion.Euler(0f, 137f, 0f);
                Quaternion controlRotation = Quaternion.Euler(-41f, 137f, 0f);
                var state = new AuthorityState(
                    1,
                    new QuantizedVector3(100, 0, 200),
                    QuantizedQuaternion.FromQuaternion(authorityRotation),
                    new QuantizedVector3(0, 0, 0),
                    (byte)MovementState.Walking,
                    true,
                    new QuantizedVector3(0, 1000, 0),
                    0,
                    QuantizedQuaternion.FromQuaternion(controlRotation),
                    true);

                presentation.Apply(state);

                Assert.That(
                    Quaternion.Angle(pawn.ControlRotation, controlRotation),
                    Is.LessThan(0.1f));
                Assert.That(pawn.IsAiming, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Apply_UsesSuccessiveAuthorityControlRotationsAsRemoteViewDelta()
        {
            var root = new GameObject("RemoteSnapshotPresentationTurnTest");
            try
            {
                var pawn = new Pawn(root);
                var presentation = new RemoteSnapshotPresentation(pawn);

                presentation.Apply(State(1, Quaternion.Euler(-10f, 30f, 0f)));
                presentation.Apply(State(2, Quaternion.Euler(-25f, 55f, 0f)));

                Assert.That(pawn.ViewDeltaDegrees.x, Is.EqualTo(25f).Within(0.1f));
                Assert.That(pawn.ViewDeltaDegrees.y, Is.EqualTo(15f).Within(0.1f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static AuthorityState State(long serverTick, Quaternion controlRotation) =>
            new AuthorityState(
                serverTick,
                new QuantizedVector3(100, 0, 200),
                QuantizedQuaternion.FromQuaternion(Quaternion.identity),
                new QuantizedVector3(0, 0, 0),
                (byte)MovementState.Walking,
                true,
                new QuantizedVector3(0, 1000, 0),
                0,
                QuantizedQuaternion.FromQuaternion(controlRotation),
                false);
    }
}
