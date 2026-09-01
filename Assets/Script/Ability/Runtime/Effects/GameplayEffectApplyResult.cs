using System;
using System.Collections.Generic;
using CGame.Ability.Attributes;

namespace CGame.Ability.Effects
{
    public readonly struct GameplayEffectAttributeChange
    {
        public GameplayEffectAttributeChange(GameplayAttribute attribute, float previousValue, float currentValue)
        {
            Attribute = attribute ?? throw new ArgumentNullException(nameof(attribute));
            PreviousValue = previousValue;
            CurrentValue = currentValue;
        }

        public GameplayAttribute Attribute { get; }
        public float PreviousValue { get; }
        public float CurrentValue { get; }
    }

    public readonly struct GameplayEffectApplyResult
    {
        private GameplayEffectApplyResult(
            bool succeeded,
            GameplayEffectFailureReason failureReason,
            IReadOnlyList<GameplayEffectAttributeChange> committedChanges)
        {
            Succeeded = succeeded;
            FailureReason = failureReason;
            CommittedChanges = committedChanges ?? Array.Empty<GameplayEffectAttributeChange>();
        }

        public bool Succeeded { get; }
        public GameplayEffectFailureReason FailureReason { get; }
        public IReadOnlyList<GameplayEffectAttributeChange> CommittedChanges { get; }

        public static GameplayEffectApplyResult Success(IReadOnlyList<GameplayEffectAttributeChange> committedChanges)
        {
            return new GameplayEffectApplyResult(true, GameplayEffectFailureReason.None, committedChanges);
        }

        public static GameplayEffectApplyResult Failure(GameplayEffectFailureReason reason)
        {
            return new GameplayEffectApplyResult(false, reason, null);
        }
    }
}
