using NUnit.Framework;

namespace CGame.Animation.Tests
{
    public sealed class CharacterAnimationCurveMixingTests
    {
        [TestCase(1f, 0f)]
        [TestCase(0.75f, 0.25f)]
        [TestCase(0.25f, 0.75f)]
        [TestCase(0f, 1f)]
        public void MaskDefaultWeight_ReturnsAsReloadSlotBlendsOut(
            float reloadInputWeight,
            float expectedAttachHandWeight)
        {
            float resolved = CharacterAnimationChannelMixer.ResolveCurveValueWithDefault(
                weightedCurveValue: 0f,
                matchedInputWeight: reloadInputWeight,
                defaultValue: 1f);

            Assert.That(resolved, Is.EqualTo(expectedAttachHandWeight).Within(0.0001f));
        }

        [Test]
        public void ZeroDefault_DoesNotChangeWeightedWeaponBoneCurve()
        {
            float resolved = CharacterAnimationChannelMixer.ResolveCurveValueWithDefault(
                weightedCurveValue: 0.4f,
                matchedInputWeight: 0.4f,
                defaultValue: 0f);

            Assert.That(resolved, Is.EqualTo(0.4f).Within(0.0001f));
        }
    }
}
