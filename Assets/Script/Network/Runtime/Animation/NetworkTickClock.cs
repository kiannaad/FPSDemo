using System;

namespace CGame.Network
{
    public sealed class NetworkTickClock
    {
        public const int TicksPerSecond = 60;

        public bool HasObservation { get; private set; }
        public long EstimatedServerTick { get; private set; }

        public void Observe(long serverTick)
        {
            if (serverTick < 0) throw new ArgumentOutOfRangeException(nameof(serverTick));
            if (HasObservation && serverTick <= EstimatedServerTick) return;
            EstimatedServerTick = serverTick;
            HasObservation = true;
        }

        public long ElapsedTicksSince(long serverStartTick) =>
            HasObservation ? Math.Max(0, EstimatedServerTick - serverStartTick) : 0;

        public float TicksToSeconds(long ticks) => Math.Max(0, ticks) / (float)TicksPerSecond;
    }
}
