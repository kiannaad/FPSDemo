using System;

namespace CGame
{
    public sealed class FixedStepAccumulator
    {
        private float accumulatedTime;

        public FixedStepAccumulator(float stepSeconds)
        {
            if (stepSeconds <= 0f) throw new ArgumentOutOfRangeException(nameof(stepSeconds));
            StepSeconds = stepSeconds;
        }

        public float StepSeconds { get; }

        public int Consume(float elapsedSeconds)
        {
            if (elapsedSeconds <= 0f) return 0;
            accumulatedTime += elapsedSeconds;
            int stepCount = 0;
            while (accumulatedTime >= StepSeconds)
            {
                accumulatedTime -= StepSeconds;
                stepCount++;
            }

            return stepCount;
        }
    }
}
