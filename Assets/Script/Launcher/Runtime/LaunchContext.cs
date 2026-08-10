namespace CGame
{
    public sealed class LaunchContext
    {
        public LaunchContext(LaunchAttemptId attemptId)
        {
            AttemptId = attemptId;
        }

        public LaunchAttemptId AttemptId { get; }
    }
}
