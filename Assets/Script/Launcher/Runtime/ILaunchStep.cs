using System.Threading;
using System.Threading.Tasks;

namespace CGame
{
    public interface ILaunchStep
    {
        string Name { get; }

        Task<LaunchStepResult> ExecuteAsync(LaunchContext context, CancellationToken cancellationToken);

        Task ExitAsync(LaunchContext context);
    }
}
