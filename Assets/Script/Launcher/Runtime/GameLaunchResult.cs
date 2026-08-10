namespace CGame
{
    public sealed class GameLaunchResult
    {
        private GameLaunchResult(bool succeeded, GameStartRequest request, LaunchFailure failure)
        {
            Succeeded = succeeded;
            Request = request;
            Failure = failure;
        }

        public bool Succeeded { get; }

        public GameStartRequest Request { get; }

        public LaunchFailure Failure { get; }

        public static GameLaunchResult Success(GameStartRequest request) =>
            new GameLaunchResult(true, request, null);

        public static GameLaunchResult Fail(LaunchFailure failure) =>
            new GameLaunchResult(false, default, failure);
    }
}
