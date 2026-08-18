using NUnit.Framework;
using UnityEngine;

namespace CGame.Animation.Tests
{
    public sealed class RecoilContractTests
    {
        private GameObject root;
        private Pawn pawn;
        private RecoilProfile profile;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject(nameof(RecoilContractTests));
            pawn = new Pawn(root);
            profile = ScriptableObject.CreateInstance<RecoilProfile>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(profile);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void ProfileAndFrameDataRemainFiniteAndDeterministic()
        {
            profile.Validate();
            pawn.BindRecoilProfile(profile);

            FireResult first = pawn.NotifySuccessfulShot();
            FireResult second = pawn.NotifySuccessfulShot();

            Assert.That(first.Succeeded, Is.True);
            Assert.That(second.Succeeded, Is.True);
            Assert.That(second.ShotSequence, Is.EqualTo(first.ShotSequence + 1));
            Assert.That(float.IsNaN(pawn.RecoilRotationOffsetDegrees.x), Is.False);
            Assert.That(float.IsInfinity(pawn.CameraShakeSample), Is.False);
            Assert.That(pawn.Recoil.FrameData.ShotSequence, Is.EqualTo(2));
        }
    }
}
