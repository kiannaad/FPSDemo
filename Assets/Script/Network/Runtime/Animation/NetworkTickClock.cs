using System;

namespace CGame.Network
{
    public sealed class NetworkTickClock
    {
        public const int TicksPerSecond = 60;

        public bool HasObservation { get; private set; }
        public long EstimatedServerTick { get; private set; }
        public long LastObservedServerTick { get; private set; }

        public void Observe(long serverTick)
        {
            if (serverTick < 0) throw new ArgumentOutOfRangeException(nameof(serverTick));
            if (HasObservation && serverTick <= LastObservedServerTick) return;
            LastObservedServerTick = serverTick;
            EstimatedServerTick = serverTick;
            HasObservation = true;
        }

        public void AdvanceOneTick()
        {
            if (!HasObservation) return;
            EstimatedServerTick++;
        }

        public long ElapsedTicksSince(long serverStartTick) =>
            HasObservation ? Math.Max(0, EstimatedServerTick - serverStartTick) : 0;

        public float TicksToSeconds(long ticks) => Math.Max(0, ticks) / (float)TicksPerSecond;
    }
}
