using CGame.GameplayTags;

namespace CGame
{
    public static class WeaponSwitchGameplayTags
    {
        public static GameplayTag SwitchAbility =>
            GameplayTagManager.Instance.RequestTag("Ability.Weapon.Switch");

        public static GameplayTag SwitchState =>
            GameplayTagManager.Instance.RequestTag("State.Weapon.Switch");
    }
}
