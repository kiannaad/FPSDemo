using System;

namespace CGame
{
    public sealed class LaunchFailure
    {
        public LaunchFailure(string stepName, string message, Exception exception = null, bool wasCancelled = false)
        {
            StepName = stepName ?? string.Empty;
            Message = string.IsNullOrWhiteSpace(message) ? "Launch failed." : message;
            Exception = exception;
            WasCancelled = wasCancelled;
        }

        public string StepName { get; }

        public string Message { get; }

        public Exception Exception { get; }

        public bool WasCancelled { get; }

        public override string ToString() => string.IsNullOrEmpty(StepName)
            ? Message
            : $"{StepName}: {Message}";
    }
}
