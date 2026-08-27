using CGame.Ability.Cues;

namespace CGame
{
    public readonly struct FireResult
    {
        public FireResult(
            bool succeeded,
            int shotSequence,
            GameplayHitResult? hitResult = null,
            string failureReason = null)
        {
            Succeeded = succeeded;
            ShotSequence = shotSequence;
            HitResult = hitResult;
            FailureReason = failureReason ?? string.Empty;
        }

        public bool Succeeded { get; }
        public int ShotSequence { get; }
        public GameplayHitResult? HitResult { get; }
        public bool HasHitResult => HitResult.HasValue;
        public string FailureReason { get; }

        public static FireResult Failed(string reason, int shotSequence)
            => new FireResult(false, shotSequence, null, reason);

        public static FireResult Queued(int shotSequence)
            => new FireResult(true, shotSequence);
    }
}
