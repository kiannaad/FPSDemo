using Fps.Protocol;

namespace Fps.ServerDomain.Matches;

public sealed class AuthorityAnimationActionTimeline
{
    private sealed class ActiveAction
    {
        public required NetworkAnimationActionStartedMessage Started;
        public required Action Commit;
        public required Action Complete;
        public bool Committed;
    }

    private readonly Dictionary<long, ActiveAction> activeBySequence = new();
    private long nextActionSequence;

    public bool TryStart(
        NetworkAnimationActionRequestMessage request,
        long expectedPawnId,
        long expectedPossessionRevision,
        long serverTick,
        int durationTicks,
        int? commitOffsetTicks,
        Action commit,
        out NetworkAnimationActionStartedMessage? started,
        out string reason)
    {
        return TryStart(request, expectedPawnId, expectedPossessionRevision, serverTick, durationTicks, commitOffsetTicks, commit, static () => { }, out started, out reason);
    }

    public bool TryStart(
        NetworkAnimationActionRequestMessage request,
        long expectedPawnId,
        long expectedPossessionRevision,
        long serverTick,
        int durationTicks,
        int? commitOffsetTicks,
        Action commit,
        Action complete,
        out NetworkAnimationActionStartedMessage? started,
        out string reason)
    {
        started = null;
        if (request.PawnId != expectedPawnId || request.PossessionRevision != expectedPossessionRevision)
        {
            reason = "PossessionMismatch";
            return false;
        }
        if (request.PredictionNonce <= 0 || request.EquipmentInstanceId <= 0 ||
            string.IsNullOrWhiteSpace(request.VariantId) ||
            !Enum.IsDefined(request.ActionKind) || durationTicks <= 0 ||
            (commitOffsetTicks.HasValue && (commitOffsetTicks.Value < 0 || commitOffsetTicks.Value > durationTicks)))
        {
            reason = "InvalidActionRequest";
            return false;
        }

        long sequence = checked(++nextActionSequence);
        started = new NetworkAnimationActionStartedMessage(
            request.PawnId,
            request.PossessionRevision,
            request.PredictionNonce,
            sequence,
            serverTick,
            durationTicks,
            commitOffsetTicks.HasValue ? checked(serverTick + commitOffsetTicks.Value) : null,
            request.ActionKind,
            request.VariantId,
            request.EquipmentInstanceId);
        activeBySequence.Add(sequence, new ActiveAction
        {
            Started = started,
            Commit = commit ?? (() => { }),
            Complete = complete ?? (() => { })
        });
        reason = string.Empty;
        return true;
    }

    public IReadOnlyList<NetworkAnimationActionTerminalMessage> AdvanceTo(long serverTick)
    {
        var events = new List<NetworkAnimationActionTerminalMessage>();
        foreach ((long sequence, ActiveAction action) in activeBySequence.ToArray())
        {
            if (!action.Committed && action.Started.CommitTick is long commitTick && serverTick >= commitTick)
            {
                action.Commit();
                action.Committed = true;
                events.Add(Terminal(action, serverTick, NetworkAnimationActionTerminalKind.Committed));
            }
            if (serverTick < action.Started.ServerStartTick + action.Started.DurationTicks) continue;
            events.Add(Terminal(action, serverTick, NetworkAnimationActionTerminalKind.Ended));
            action.Complete();
            activeBySequence.Remove(sequence);
        }
        return events;
    }

    public NetworkAnimationActionTerminalMessage? Cancel(long actionSequence, long serverTick)
    {
        if (!activeBySequence.Remove(actionSequence, out ActiveAction? action)) return null;
        action.Complete();
        return Terminal(action, serverTick, NetworkAnimationActionTerminalKind.Cancelled);
    }

    private static NetworkAnimationActionTerminalMessage Terminal(
        ActiveAction action,
        long serverTick,
        NetworkAnimationActionTerminalKind kind) => new(
            action.Started.PawnId,
            action.Started.PossessionRevision,
            action.Started.ActionSequence,
            serverTick,
            kind);
}
