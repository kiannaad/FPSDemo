using Fps.Protocol;

namespace Fps.ClientNet;

public readonly record struct RpcResponse(PacketHeader Header, byte[] Payload);

public sealed class PendingRequestRegistry
{
    private readonly object syncRoot = new();
    private readonly Dictionary<ulong, PendingRequest> pendingRequests = new();

    public int PendingCount
    {
        get
        {
            lock (syncRoot)
            {
                return pendingRequests.Count;
            }
        }
    }

    public Task<RpcResponse> Register(ulong requestId, DateTimeOffset deadline)
    {
        if (requestId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(requestId), "RequestId must be non-zero.");
        }

        var completionSource = new TaskCompletionSource<RpcResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (syncRoot)
        {
            if (!pendingRequests.TryAdd(requestId, new PendingRequest(deadline, completionSource)))
            {
                throw new InvalidOperationException($"RequestId {requestId} is already pending.");
            }
        }

        return completionSource.Task;
    }

    public bool TryComplete(RpcResponse response)
    {
        if ((response.Header.Flags & PacketFlags.Response) == 0)
        {
            return false;
        }

        PendingRequest pendingRequest;
        lock (syncRoot)
        {
            if (!pendingRequests.Remove(response.Header.RequestId, out pendingRequest))
            {
                return false;
            }
        }

        return pendingRequest.CompletionSource.TrySetResult(response);
    }

    public void Sweep(DateTimeOffset now)
    {
        List<PendingRequest> expiredRequests = new();
        lock (syncRoot)
        {
            foreach ((ulong requestId, PendingRequest pendingRequest) in pendingRequests.ToArray())
            {
                if (pendingRequest.Deadline > now)
                {
                    continue;
                }

                pendingRequests.Remove(requestId);
                expiredRequests.Add(pendingRequest);
            }
        }

        foreach (PendingRequest expiredRequest in expiredRequests)
        {
            expiredRequest.CompletionSource.TrySetException(new TimeoutException("The RPC response timed out."));
        }
    }

    public void Disconnect(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Disconnect reason is required.", nameof(reason));
        }

        PendingRequest[] disconnectedRequests;
        lock (syncRoot)
        {
            disconnectedRequests = pendingRequests.Values.ToArray();
            pendingRequests.Clear();
        }

        foreach (PendingRequest pendingRequest in disconnectedRequests)
        {
            pendingRequest.CompletionSource.TrySetException(new IOException(reason));
        }
    }

    private readonly record struct PendingRequest(
        DateTimeOffset Deadline,
        TaskCompletionSource<RpcResponse> CompletionSource);
}
