namespace CGame
{
    public readonly struct GameStartRequest
    {
        public GameStartRequest(LaunchAttemptId launchAttemptId)
        {
            LaunchAttemptId = launchAttemptId;
        }

        public LaunchAttemptId LaunchAttemptId { get; }
    }
}
