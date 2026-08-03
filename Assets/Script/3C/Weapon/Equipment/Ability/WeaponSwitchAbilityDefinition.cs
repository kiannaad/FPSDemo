using CGame.Ability;

namespace CGame
{
    public sealed class WeaponSwitchAbilityDefinition : AbilityDefinition
    {
        public WeaponSwitchAbilityDefinition()
            : base(
                WeaponSwitchGameplayTags.SwitchAbility,
                activationOwnedTags: new[] { WeaponSwitchGameplayTags.SwitchState },
                blockedOwnedTags: new[]
                {
                    WeaponActionGameplayTags.ActionState,
                    WeaponSwitchGameplayTags.SwitchState
                })
        {
        }

        protected override AbilityInstance CreateInstance()
        {
            return new WeaponSwitchAbilityInstance();
        }
    }
}
