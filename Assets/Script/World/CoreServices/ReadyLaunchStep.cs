using System.Threading;
using System.Threading.Tasks;

namespace CGame
{
    public sealed class ReadyLaunchStep : ILaunchStep
    {
        public string Name => "Ready";

        public Task<LaunchStepResult> ExecuteAsync(
            LaunchContext context,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(LaunchStepResult.Success());
        }

        public Task ExitAsync(LaunchContext context)
        {
            return Task.CompletedTask;
        }
    }
}
