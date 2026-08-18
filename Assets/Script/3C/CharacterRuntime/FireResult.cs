namespace CGame
{
    public readonly struct FireResult
    {
        public FireResult(bool succeeded, int shotSequence, string failureReason = null)
        {
            Succeeded = succeeded;
            ShotSequence = shotSequence;
            FailureReason = failureReason ?? string.Empty;
        }

        public bool Succeeded { get; }
        public int ShotSequence { get; }
        public string FailureReason { get; }

        public static FireResult Failed(string reason, int shotSequence)
            => new FireResult(false, shotSequence, reason);
    }
}
