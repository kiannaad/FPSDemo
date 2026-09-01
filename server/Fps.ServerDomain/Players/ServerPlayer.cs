namespace Fps.ServerDomain.Players;

public sealed class ServerPlayer
{
    public ServerPlayer(long playerId, string connectionId)
    {
        if (playerId <= 0) throw new ArgumentOutOfRangeException(nameof(playerId));
        if (string.IsNullOrWhiteSpace(connectionId)) throw new ArgumentException("Connection id is required.", nameof(connectionId));
        PlayerId = playerId;
        ConnectionId = connectionId;
    }

    public long PlayerId { get; }
    public string ConnectionId { get; }
    public long ControlledPawnId { get; private set; }
    public long PossessionRevision { get; private set; }

    public void Possess(long pawnId)
    {
        if (pawnId <= 0) throw new ArgumentOutOfRangeException(nameof(pawnId));
        ControlledPawnId = pawnId;
        PossessionRevision = checked(PossessionRevision + 1);
    }

    public void Unpossess()
    {
        if (ControlledPawnId == 0) return;
        ControlledPawnId = 0;
        PossessionRevision = checked(PossessionRevision + 1);
    }

    public bool AcceptsPossessionRevision(long revision) => revision == PossessionRevision;
}
