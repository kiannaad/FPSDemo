using System.Threading;
using System.Threading.Tasks;

namespace CGame
{
    public interface IWorldCoreService
    {
        string Name { get; }

        Task InitializeAsync(CancellationToken cancellationToken);

        Task ShutdownAsync();
    }
}
