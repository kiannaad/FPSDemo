using NUnit.Framework;

namespace CGame.Network.Tests
{
    [Category("Network044")]
    public sealed class NetworkFireBridgeTests
    {
        [Test]
        public void ApplyCommitted_ForPredictedShot_ConfirmsOnceWithoutRemoteReplay()
        {
            var bridge = new NetworkFireBridge(7, 3);
            int ownerPredicted = 0;
            int ownerConfirmed = 0;
            int remotePlayed = 0;
            bridge.OwnerPredictionCreated += _ => ownerPredicted++;
            bridge.OwnerPredictionConfirmed += _ => ownerConfirmed++;
            bridge.RemoteFireCommitted += _ => remotePlayed++;

            FireRequest request = bridge.BeginPrediction(9, 30);
            var committed = new FireCommitted
            {
                PawnId = 7,
                PossessionRevision = 3,
                PredictionNonce = request.PredictionNonce,
                ClientShotSequence = request.ClientShotSequence,
                ShotSequence = 41,
                AuthoritativeMagazineAmmo = 29
            };

            Assert.That(bridge.ApplyCommitted(committed), Is.True);
            Assert.That(bridge.ApplyCommitted(committed), Is.False);
            Assert.That(ownerPredicted, Is.EqualTo(1));
            Assert.That(ownerConfirmed, Is.EqualTo(1));
            Assert.That(remotePlayed, Is.Zero);
            Assert.That(bridge.AuthoritativeMagazineAmmo, Is.EqualTo(29));
        }

        [Test]
        public void ApplyCommitted_ForRemoteDuplicateShot_PlaysOnlyOnce()
        {
            var bridge = new NetworkFireBridge(7, 3);
            int remotePlayed = 0;
            bridge.RemoteFireCommitted += _ => remotePlayed++;
            var committed = new FireCommitted
            {
                PawnId = 8,
                PossessionRevision = 2,
                ShotSequence = 52,
                AuthoritativeMagazineAmmo = 28
            };

            Assert.That(bridge.ApplyCommitted(committed), Is.True);
            Assert.That(bridge.ApplyCommitted(committed), Is.False);
            Assert.That(remotePlayed, Is.EqualTo(1));
        }

        [Test]
        public void ApplyRejected_ForOwnerPrediction_ReconcilesAmmoOnce()
        {
            var bridge = new NetworkFireBridge(7, 3);
            int rejected = 0;
            bridge.OwnerPredictionRejected += _ => rejected++;
            FireRequest request = bridge.BeginPrediction(9, 30);
            var response = new FireRejected
            {
                PawnId = 7,
                PossessionRevision = 3,
                PredictionNonce = request.PredictionNonce,
                ClientShotSequence = request.ClientShotSequence,
                Reason = FireRejectionReason.OutOfAmmo,
                AuthoritativeMagazineAmmo = 0
            };

            Assert.That(bridge.ApplyRejected(response), Is.True);
            Assert.That(bridge.ApplyRejected(response), Is.False);
            Assert.That(rejected, Is.EqualTo(1));
            Assert.That(bridge.AuthoritativeMagazineAmmo, Is.Zero);
        }
    }
}
