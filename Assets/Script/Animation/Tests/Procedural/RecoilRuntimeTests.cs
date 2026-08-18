using NUnit.Framework;
using UnityEngine;

namespace CGame.Animation.Tests
{
    public sealed class RecoilRuntimeTests
    {
        [Test]
        public void RecoilPulsesAccumulateThenRecoverToNeutral()
        {
            GameObject root = new GameObject(nameof(RecoilRuntimeTests));
            RecoilProfile profile = ScriptableObject.CreateInstance<RecoilProfile>();
            try
            {
                Pawn pawn = new Pawn(root);
                pawn.BindRecoilProfile(profile);
                pawn.NotifySuccessfulShot();
                Vector2 peak = pawn.RecoilRotationOffsetDegrees;
                pawn.NotifySuccessfulShot();
                Assert.That(pawn.RecoilRotationOffsetDegrees.x, Is.GreaterThanOrEqualTo(peak.x));

                pawn.Recoil.Advance(10f);
                Assert.That(pawn.RecoilRotationOffsetDegrees, Is.EqualTo(Vector2.zero));
                Assert.That(pawn.RecoilShotSequence, Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(profile);
                Object.DestroyImmediate(root);
            }
        }
    }
}
