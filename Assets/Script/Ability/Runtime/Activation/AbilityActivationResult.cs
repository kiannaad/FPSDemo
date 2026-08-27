using System;
using System.Collections.Generic;
using CGame.GameplayTags;

namespace CGame.Ability
{
    public readonly struct AbilityActivationResult
    {
        private static readonly GameplayTag[] NoFailureTags = Array.Empty<GameplayTag>();

        private AbilityActivationResult(bool succeeded, AbilityActivationHandle activationHandle, IReadOnlyList<GameplayTag> failureTags)
        {
            Succeeded = succeeded;
            ActivationHandle = activationHandle;
            FailureTags = failureTags ?? NoFailureTags;
        }

        public bool Succeeded { get; }
        public AbilityActivationHandle ActivationHandle { get; }
        public IReadOnlyList<GameplayTag> FailureTags { get; }

        internal static AbilityActivationResult Success(AbilityActivationHandle activationHandle)
        {
            return new AbilityActivationResult(true, activationHandle, NoFailureTags);
        }

        internal static AbilityActivationResult Failure(IReadOnlyList<GameplayTag> failureTags = null)
        {
            return new AbilityActivationResult(false, default, failureTags);
        }
    }
}
