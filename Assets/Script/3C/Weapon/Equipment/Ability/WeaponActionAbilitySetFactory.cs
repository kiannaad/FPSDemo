using System.Collections.Generic;
using CGame.Ability;
using CGame.Animation;

namespace CGame
{
    public static class WeaponActionAbilitySetFactory
    {
        public static AbilitySet Create(WeaponAnimationDefinition definition)
        {
            if (definition == null)
            {
                throw new System.ArgumentNullException(nameof(definition));
            }

            var abilities = new List<AbilityDefinition>();
            if (definition.SupportsFire)
            {
                abilities.Add(CreateDefinition(WeaponActionKind.Fire, definition.Fire));
            }

            if (definition.SupportsReload)
            {
                abilities.Add(CreateDefinition(WeaponActionKind.Reload, definition.Reload));
            }

            if (definition.SupportsMeleeAttack)
            {
                abilities.Add(CreateDefinition(WeaponActionKind.MeleeAttack, definition.MeleeAttack));
            }

            return new AbilitySet(abilities);
        }

        private static WeaponActionAbilityDefinition CreateDefinition(
            WeaponActionKind kind,
            CGame.Animation.AnimationClipAsset animation)
        {
            return new WeaponActionAbilityDefinition(
                WeaponActionGameplayTags.GetAbilityTag(kind),
                WeaponActionGameplayTags.GetEventTag(kind),
                kind,
                animation);
        }
    }
}
