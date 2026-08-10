namespace CGame
{
    public sealed class LaunchStepResult
    {
        private LaunchStepResult(bool succeeded, LaunchFailure failure)
        {
            Succeeded = succeeded;
            Failure = failure;
        }

        public bool Succeeded { get; }

        public LaunchFailure Failure { get; }

        public static LaunchStepResult Success() => new LaunchStepResult(true, null);

        public static LaunchStepResult Fail(string message) =>
            new LaunchStepResult(false, new LaunchFailure(string.Empty, message));

        public static LaunchStepResult Fail(LaunchFailure failure) =>
            new LaunchStepResult(false, failure ?? new LaunchFailure(string.Empty, "Launch step failed."));
    }
}
