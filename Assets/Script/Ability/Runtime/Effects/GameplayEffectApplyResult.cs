namespace CGame.Ability.Effects
{
    public readonly struct GameplayEffectApplyResult
    {
        private GameplayEffectApplyResult(bool succeeded, GameplayEffectFailureReason failureReason)
        {
            Succeeded = succeeded;
            FailureReason = failureReason;
        }

        public bool Succeeded { get; }
        public GameplayEffectFailureReason FailureReason { get; }

        public static GameplayEffectApplyResult Success()
        {
            return new GameplayEffectApplyResult(true, GameplayEffectFailureReason.None);
        }

        public static GameplayEffectApplyResult Failure(GameplayEffectFailureReason reason)
        {
            return new GameplayEffectApplyResult(false, reason);
        }
    }
}
