namespace Fps.ServerDomain.Matches;

public sealed record AuthorityTargetDefinition(string TargetId, float MaxHealth);

public sealed record AuthorityTargetState(string TargetId, float Health, float MaxHealth, long Revision, bool IsDead);

public sealed record AuthorityTargetStateChange(
    AuthorityTargetState State,
    long CausingPawnId,
    long CausingShotSequence);

public sealed class AuthorityTargetRegistry
{
    private readonly Dictionary<string, AuthorityTargetState> statesByTargetId;

    private AuthorityTargetRegistry(Dictionary<string, AuthorityTargetState> statesByTargetId)
    {
        this.statesByTargetId = statesByTargetId;
    }

    public static AuthorityTargetRegistry Create(IEnumerable<AuthorityTargetDefinition> definitions)
    {
        if (definitions is null) throw new ArgumentNullException(nameof(definitions));
        var states = new Dictionary<string, AuthorityTargetState>(StringComparer.Ordinal);
        foreach (AuthorityTargetDefinition definition in definitions)
        {
            if (string.IsNullOrWhiteSpace(definition.TargetId))
                throw new ArgumentException("TargetId is required.", nameof(definitions));
            if (definition.MaxHealth <= 0f)
                throw new ArgumentOutOfRangeException(nameof(definitions), "Target MaxHealth must be positive.");
            if (!states.TryAdd(definition.TargetId, new AuthorityTargetState(
                    definition.TargetId, definition.MaxHealth, definition.MaxHealth, 0, false)))
                throw new ArgumentException($"TargetId is duplicated: {definition.TargetId}.", nameof(definitions));
        }

        return new AuthorityTargetRegistry(states);
    }

    public IReadOnlyList<AuthorityTargetState> Snapshot() => statesByTargetId.Values.OrderBy(state => state.TargetId, StringComparer.Ordinal).ToArray();

    public AuthorityTargetStateChange? ApplyAcceptedHit(string? targetId, long causingPawnId, long causingShotSequence, float damage)
    {
        if (string.IsNullOrWhiteSpace(targetId) || causingPawnId <= 0 || causingShotSequence <= 0 || damage <= 0f ||
            !statesByTargetId.TryGetValue(targetId, out AuthorityTargetState? current) || current.IsDead)
            return null;

        float health = Math.Max(0f, current.Health - damage);
        var next = current with { Health = health, Revision = checked(current.Revision + 1), IsDead = health <= 0f };
        statesByTargetId[targetId] = next;
        return new AuthorityTargetStateChange(next, causingPawnId, causingShotSequence);
    }
}
