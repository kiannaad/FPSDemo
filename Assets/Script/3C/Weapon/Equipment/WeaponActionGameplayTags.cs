using CGame.GameplayTags;

namespace CGame
{
    public static class WeaponActionGameplayTags
    {
        public static GameplayTag FireAbility => Request("Ability.Weapon.Fire");
        public static GameplayTag ReloadAbility => Request("Ability.Weapon.Reload");
        public static GameplayTag MeleeAbility => Request("Ability.Weapon.Melee");
        public static GameplayTag FireEvent => Request("Event.Weapon.Fire");
        public static GameplayTag ReloadEvent => Request("Event.Weapon.Reload");
        public static GameplayTag MeleeEvent => Request("Event.Weapon.Melee");
        public static GameplayTag ActionState => Request("State.Weapon.Action");

        public static GameplayTag GetAbilityTag(WeaponActionKind kind)
        {
            return kind == WeaponActionKind.Fire ? FireAbility
                : kind == WeaponActionKind.Reload ? ReloadAbility
                : kind == WeaponActionKind.MeleeAttack ? MeleeAbility
                : default;
        }

        public static GameplayTag GetEventTag(WeaponActionKind kind)
        {
            return kind == WeaponActionKind.Fire ? FireEvent
                : kind == WeaponActionKind.Reload ? ReloadEvent
                : kind == WeaponActionKind.MeleeAttack ? MeleeEvent
                : default;
        }

        private static GameplayTag Request(string path)
        {
            return GameplayTagManager.Instance.RequestTag(path);
        }
    }
}
