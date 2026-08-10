namespace CGame
{
    public sealed class WorldStartResult
    {
        private WorldStartResult(bool succeeded, GameStartRequest request, string error)
        {
            Succeeded = succeeded;
            Request = request;
            Error = error;
        }

        public bool Succeeded { get; }

        public GameStartRequest Request { get; }

        public string Error { get; }

        public static WorldStartResult Success(GameStartRequest request) =>
            new WorldStartResult(true, request, string.Empty);

        public static WorldStartResult Fail(string error) =>
            new WorldStartResult(false, default, error);
    }
}
