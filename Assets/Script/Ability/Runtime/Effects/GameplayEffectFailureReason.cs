namespace CGame.Ability.Effects
{
    public enum GameplayEffectFailureReason
    {
        None,
        InvalidDefinition,
        InvalidLevel,
        UnsupportedDuration,
        MissingSourceAttributeSet,
        MissingTargetAttributeSet,
        InvalidMagnitude,
        ExecutionFailed
    }
}
