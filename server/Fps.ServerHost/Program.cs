namespace Fps.ServerHost;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            ServerHostOptions options = ServerHostCommandLine.Parse(args);
            await using var host = new ServerHost();
            await host.StartAsync(options);
            Console.WriteLine($"ServerHost running: udp={host.Health.Port}; health=http://127.0.0.1:{host.Health.HealthPort}/health; content={host.Health.ContentVersion}");

            using var shutdown = new CancellationTokenSource();
            Console.CancelKeyPress += (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                shutdown.Cancel();
            };
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, shutdown.Token);
            }
            catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
            {
            }

            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"ServerHost failed: {exception.Message}");
            return 1;
        }
    }
}
