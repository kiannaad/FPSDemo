using CGame.Ability.Cues;

namespace CGame.Ability.Targeting
{
    public sealed class SingleTargetHitData
    {
        public SingleTargetHitData(ulong shotId, int traceIndex, GameplayHitResult hitResult)
        {
            ShotId = shotId;
            TraceIndex = traceIndex;
            HitResult = hitResult;
        }

        public ulong ShotId { get; }
        public int TraceIndex { get; }
        public GameplayHitResult HitResult { get; }
        public bool HitReplaced => false;
    }
}
