namespace Fps.ClientNet;

public interface IClientTransport : IDisposable
{
    event Action? Connected;

    event Action<RpcResponse>? ResponseReceived;

    event Action<string>? Disconnected;

    bool IsConnected { get; }

    void Connect(string host, int port, string connectionKey);

    void Send(byte[] packet);

    void PollEvents();
}
