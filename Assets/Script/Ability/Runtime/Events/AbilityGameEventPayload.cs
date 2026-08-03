using System;

namespace CGame.Ability
{
    public sealed class AbilityGameEventPayload
    {
        public AbilityGameEventPayload(
            AbilitySystemComponent targetAbilitySystem,
            object avatar,
            object sourceObject,
            AbilityActivationHandle activationHandle)
        {
            TargetAbilitySystem = targetAbilitySystem ?? throw new ArgumentNullException(nameof(targetAbilitySystem));
            Avatar = avatar;
            SourceObject = sourceObject;
            ActivationHandle = activationHandle;
        }

        public AbilitySystemComponent TargetAbilitySystem { get; }
        public object Avatar { get; }
        public object SourceObject { get; }
        public AbilityActivationHandle ActivationHandle { get; }
    }
}
