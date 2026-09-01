using Fps.Protocol;

namespace Fps.ClientNet;

public sealed class RpcClient : IDisposable
{
    private readonly IClientTransport transport;
    private readonly PendingRequestRegistry pendingRequests = new();
    private readonly MainThreadEventQueue mainThreadEvents = new();
    private ulong nextRequestId;
    private bool disposed;

    public RpcClient(IClientTransport transport)
    {
        this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
        transport.ResponseReceived += HandleResponse;
        transport.Disconnected += HandleDisconnected;
    }

    public bool IsConnected => transport.IsConnected;

    public event Action<RpcResponse>? EventReceived;

    public void Connect(string host, int port, string connectionKey)
    {
        ThrowIfDisposed();
        transport.Connect(host, port, connectionKey);
    }

    public Task<RpcResponse> RequestAsync(MessageId messageId, byte[] payload, TimeSpan timeout)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(payload);
        if (!transport.IsConnected)
        {
            throw new InvalidOperationException("Cannot send an RPC before the client transport is connected.");
        }

        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        ulong requestId = checked(++nextRequestId);
        Task<RpcResponse> responseTask = pendingRequests.Register(requestId, DateTimeOffset.UtcNow.Add(timeout));
        var header = new PacketHeader(ProtocolVersion.Current, messageId, PacketFlags.Request, requestId, 0);
        transport.Send(PacketCodec.Encode(header, payload));
        return responseTask;
    }

    public void Tick()
    {
        ThrowIfDisposed();
        transport.PollEvents();
        mainThreadEvents.Drain();
        pendingRequests.Sweep(DateTimeOffset.UtcNow);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        transport.ResponseReceived -= HandleResponse;
        transport.Disconnected -= HandleDisconnected;
        pendingRequests.Disconnect("The RPC client was disposed.");
    }

    private void HandleResponse(RpcResponse response)
    {
        mainThreadEvents.Enqueue(() =>
        {
            if ((response.Header.Flags & PacketFlags.Response) == 0)
            {
                EventReceived?.Invoke(response);
                return;
            }

            pendingRequests.TryComplete(response);
        });
    }

    private void HandleDisconnected(string reason)
    {
        mainThreadEvents.Enqueue(() => pendingRequests.Disconnect(reason));
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }
}
