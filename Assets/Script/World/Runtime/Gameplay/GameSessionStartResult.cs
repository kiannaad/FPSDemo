namespace CGame
{
    public sealed class GameSessionStartResult
    {
        private GameSessionStartResult(bool succeeded, GameSessionId sessionId, string failure)
        {
            Succeeded = succeeded;
            SessionId = sessionId;
            Failure = failure ?? string.Empty;
        }

        public bool Succeeded { get; }

        public GameSessionId SessionId { get; }

        public string Failure { get; }

        public static GameSessionStartResult Success(GameSessionId sessionId) =>
            new GameSessionStartResult(true, sessionId, string.Empty);

        public static GameSessionStartResult Fail(string failure) =>
            new GameSessionStartResult(false, default, failure);
    }
}
