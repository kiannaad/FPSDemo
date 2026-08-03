using CGame.Ability;

namespace CGame
{
    public static class WeaponSwitchAbilitySetFactory
    {
        public static AbilitySet Create()
        {
            return new AbilitySet(
                new AbilityDefinition[]
                {
                    new WeaponSwitchAbilityDefinition()
                });
        }
    }
}
