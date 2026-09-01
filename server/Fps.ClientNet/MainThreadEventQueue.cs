using System.Collections.Concurrent;

namespace Fps.ClientNet;

public sealed class MainThreadEventQueue
{
    private readonly ConcurrentQueue<Action> pendingActions = new();

    public void Enqueue(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        pendingActions.Enqueue(action);
    }

    public void Drain()
    {
        while (pendingActions.TryDequeue(out Action? action))
        {
            action();
        }
    }
}
