using System;

namespace CGame
{
    public sealed class TickIntervalTimer
    {
        public float AccumulatedTime { get; private set; }

        public bool Advance(float deltaTime, float interval, out float elapsedTime)
        {
            if (deltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime), "Tick delta time cannot be negative.");
            }

            if (interval <= 0f)
            {
                elapsedTime = deltaTime;
                return true;
            }

            AccumulatedTime += deltaTime;
            if (AccumulatedTime < interval)
            {
                elapsedTime = 0f;
                return false;
            }

            elapsedTime = AccumulatedTime;
            AccumulatedTime = 0f;
            return true;
        }

        public void Reset()
        {
            AccumulatedTime = 0f;
        }
    }
}
