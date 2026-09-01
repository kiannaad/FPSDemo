using CGame.Ability.Cues;

namespace CGame.Ability.Targeting
{
    public sealed class SingleTargetHitData
    {
        public SingleTargetHitData(int traceIndex, GameplayHitResult hitResult)
        {
            TraceIndex = traceIndex;
            HitResult = hitResult;
        }

        public int TraceIndex { get; }
        public GameplayHitResult HitResult { get; }
        public bool HitReplaced => false;
    }
}
