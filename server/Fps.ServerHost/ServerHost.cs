using Fps.ServerHost.Content;
using Fps.ServerHost.DedicatedServer;
using Fps.ServerNet;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fps.ServerHost;

public sealed class ServerHost : IAsyncDisposable
{
    private readonly SemaphoreSlim lifecycleLock = new(1, 1);
    private LiteNetServerTransport? netTransport;
    private HttpListener? healthListener;
    private CancellationTokenSource? tickCancellation;
    private Task? tickTask;
    private Task? healthTask;
    private HttpClient? dedicatedHealthClient;

    public ServerHostHealth Health { get; private set; } = new(ServerHostStatus.Stopped, 0, 0, null);

    public async Task StartAsync(ServerHostOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        await lifecycleLock.WaitAsync(cancellationToken);
        try
        {
            if (Health.Status != ServerHostStatus.Stopped)
            {
                throw new InvalidOperationException("Server host is already started.");
            }

            Health = Health with { Status = ServerHostStatus.Starting };
            LevelServerDefinition content = await LevelServerDefinition.LoadAsync(options.ContentPath, cancellationToken);
            Fps.ServerNet.Matches.IMatchPhysicsServerLifecycle? physicsLifecycle = null;
            if (!string.IsNullOrWhiteSpace(options.DedicatedExecutablePath))
            {
                dedicatedHealthClient = new HttpClient();
                var processManager = new DedicatedServerProcessManager(
                    new SystemDedicatedServerProcessFactory(),
                    new HttpDedicatedServerHealthProbe(dedicatedHealthClient));
                physicsLifecycle = new MatchPhysicsServerLifecycle(
                    processManager,
                    options.DedicatedExecutablePath,
                    options.LevelId!,
                    options.ContentVersion!,
                    options.PhysicsReadyTimeout!.Value,
                    options.DedicatedLogRoot!);
            }

            var transport = new LiteNetServerTransport(new MessageRouter(
                "fps-server-v1",
                content.SpawnPoints.Select(point => point.Id),
                physicsLifecycle));
            transport.Start(options.Port);
            netTransport = transport;
            tickCancellation = new CancellationTokenSource();
            int healthPort = options.HealthPort == 0 ? AllocateLoopbackPort() : options.HealthPort;
            healthListener = new HttpListener();
            healthListener.Prefixes.Add($"http://127.0.0.1:{healthPort}/");
            healthListener.Start();
            tickTask = RunTickLoopAsync(transport, options.TickRate, tickCancellation.Token);
            Health = new ServerHostHealth(ServerHostStatus.Running, transport.Port, healthPort, content.ContentVersion, options.LevelId);
            healthTask = RunHealthLoopAsync(healthListener, tickCancellation.Token);
        }
        catch
        {
            await StopStartedResourcesAsync();
            Health = new ServerHostHealth(ServerHostStatus.Stopped, 0, 0, null);
            throw;
        }
        finally
        {
            lifecycleLock.Release();
        }
    }

    public async Task StopAsync()
    {
        await lifecycleLock.WaitAsync();
        try
        {
            if (Health.Status == ServerHostStatus.Stopped)
            {
                return;
            }

            Health = Health with { Status = ServerHostStatus.Stopping };
            tickCancellation?.Cancel();
            if (tickTask is not null)
            {
                await tickTask;
            }

            await StopStartedResourcesAsync();
            Health = new ServerHostHealth(ServerHostStatus.Stopped, 0, 0, null);
        }
        finally
        {
            lifecycleLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        lifecycleLock.Dispose();
    }

    private static async Task RunTickLoopAsync(LiteNetServerTransport transport, int tickRate, CancellationToken cancellationToken)
    {
        TimeSpan period = TimeSpan.FromSeconds(1d / tickRate);
        using var timer = new PeriodicTimer(period);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await transport.PollEventsAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task StopStartedResourcesAsync()
    {
        tickCancellation?.Cancel();
        healthListener?.Close();
        if (healthTask is not null)
        {
            await healthTask;
        }

        netTransport?.Dispose();
        netTransport = null;
        healthListener = null;
        tickTask = null;
        healthTask = null;
        tickCancellation?.Dispose();
        tickCancellation = null;
        dedicatedHealthClient?.Dispose();
        dedicatedHealthClient = null;
    }

    private async Task RunHealthLoopAsync(HttpListener listener, CancellationToken cancellationToken)
    {
        try
        {
            while (listener.IsListening)
            {
                HttpListenerContext context = await listener.GetContextAsync().WaitAsync(cancellationToken);
                if (!string.Equals(context.Request.Url?.AbsolutePath, "/health", StringComparison.Ordinal))
                {
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    context.Response.Close();
                    continue;
                }

                byte[] payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
                    Health,
                    new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } }));
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                context.Response.ContentType = "application/json";
                context.Response.ContentLength64 = payload.Length;
                await context.Response.OutputStream.WriteAsync(payload, cancellationToken);
                context.Response.Close();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (HttpListenerException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private static int AllocateLoopbackPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
