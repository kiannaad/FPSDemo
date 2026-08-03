namespace CGame.Ability
{
    public sealed class AbilityActivationContext
    {
        internal AbilityActivationContext(
            AbilitySystemComponent abilitySystem,
            AbilitySpec spec,
            object avatar,
            AbilityActivationHandle activationHandle)
        {
            AbilitySystem = abilitySystem;
            Spec = spec;
            Avatar = avatar;
            SourceObject = spec.SourceObject;
            ActivationHandle = activationHandle;
        }

        public AbilitySystemComponent AbilitySystem { get; }
        public AbilitySpec Spec { get; }
        public object Avatar { get; }
        public object SourceObject { get; }
        public AbilityActivationHandle ActivationHandle { get; }
    }
}
