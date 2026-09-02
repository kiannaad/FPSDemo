using NUnit.Framework;

namespace CGame.CharacterPhysics.Tests
{
    public sealed class FixedStepAccumulatorTests
    {
        [Test]
        public void Consume_AccumulatesRenderFramesUntilOneFixedStepIsDue()
        {
            var accumulator = new FixedStepAccumulator(1f / 60f);

            int firstStepCount = accumulator.Consume(0.01f);
            int secondStepCount = accumulator.Consume(0.007f);

            Assert.That(firstStepCount, Is.Zero);
            Assert.That(secondStepCount, Is.EqualTo(1));
        }

        [Test]
        public void Consume_ProducesTwoFixedStepsForOneThirtiethSecondRenderFrame()
        {
            var accumulator = new FixedStepAccumulator(1f / 60f);

            int stepCount = accumulator.Consume(1f / 30f);

            Assert.That(stepCount, Is.EqualTo(2));
        }
    }
}
