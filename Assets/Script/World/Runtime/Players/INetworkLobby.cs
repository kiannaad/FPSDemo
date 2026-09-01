using System.Threading;
using System.Threading.Tasks;

namespace CGame
{
    public interface INetworkLobby
    {
        string RoomId { get; }
        string NetworkStatus { get; }
        Task CreateRoomAsync();
        Task JoinRoomAsync(string roomId);
        Task SetReadyAsync(bool isReady);
    }

    public interface INetworkPrePlayExecution
    {
        void PumpPrePlay();
        Task WaitForInitialOwnerPawnAsync(CancellationToken cancellationToken);
    }
}
