using NUnit.Framework;

namespace CGame.Network.Tests
{
    public sealed class NetworkLoopAnimationPhaseSynchronizerTests
    {
        [Test]
        public void CalculateNormalizedPhase_UsesSharedServerTime()
        {
            float first = NetworkLoopAnimationPhaseSynchronizer.CalculateNormalizedPhase(90, 1f);
            float second = NetworkLoopAnimationPhaseSynchronizer.CalculateNormalizedPhase(150, 1f);

            Assert.That(first, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(second, Is.EqualTo(0.5f).Within(0.0001f));
        }
    }
}
