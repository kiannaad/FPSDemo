using System;
using System.Collections.Generic;
using CGame.Ability;
using CGame.GameplayTags;
using UnityEngine;

namespace CGame.InventoryEquipment
{
    [Serializable]
    public sealed class WeaponAbilitySetDefinition
    {
        [SerializeReference] private List<WeaponAbilityDefinition> abilities =
            new List<WeaponAbilityDefinition>();

        public AbilitySet CreateAbilitySet()
        {
            var grants = new List<AbilityGrantDefinition>();
            foreach (WeaponAbilityDefinition ability in abilities)
            {
                if (ability == null)
                {
                    throw new InvalidOperationException("Weapon AbilitySet contains a missing ability definition.");
                }

                grants.Add(ability.CreateGrant());
            }

            return new AbilitySet(grants);
        }
    }

    [Serializable]
    public abstract class WeaponAbilityDefinition
    {
        [SerializeField] private GameplayTag abilityTag;
        [SerializeField] private GameplayTag inputTag;

        public AbilityGrantDefinition CreateGrant()
        {
            if (abilityTag.IsEmpty || inputTag.IsEmpty)
            {
                throw new InvalidOperationException("Weapon ability and input tags must both be configured.");
            }

            return new AbilityGrantDefinition(
                new WeaponActionAbilityDefinition(abilityTag, Action),
                inputTag);
        }

        protected abstract WeaponAction Action { get; }
    }

    [Serializable]
    public sealed class FireWeaponAbilityDefinition : WeaponAbilityDefinition
    {
        protected override WeaponAction Action => WeaponAction.Fire;
    }

    [Serializable]
    public sealed class ReloadWeaponAbilityDefinition : WeaponAbilityDefinition
    {
        protected override WeaponAction Action => WeaponAction.Reload;
    }

    [Serializable]
    public sealed class RecoilWeaponAbilityDefinition : WeaponAbilityDefinition
    {
        protected override WeaponAction Action => WeaponAction.Recoil;
    }

    [Serializable]
    public sealed class MeleeWeaponAbilityDefinition : WeaponAbilityDefinition
    {
        protected override WeaponAction Action => WeaponAction.Melee;
    }

    public enum WeaponAction
    {
        Fire,
        Reload,
        Recoil,
        Melee
    }

    internal sealed class WeaponActionAbilityDefinition : AbilityDefinition
    {
        private readonly WeaponAction action;

        public WeaponActionAbilityDefinition(GameplayTag abilityTag, WeaponAction action)
            : base(abilityTag)
        {
            this.action = action;
        }

        protected override AbilityInstance CreateInstance()
        {
            return new WeaponActionAbilityInstance(action);
        }
    }

    internal sealed class WeaponActionAbilityInstance : AbilityInstance
    {
        private readonly WeaponAction action;

        public WeaponActionAbilityInstance(WeaponAction action)
        {
            this.action = action;
        }

protected override void OnActivate()
        {
            if (!(ActivationContext.Spec.SourceObject is WeaponInstance weapon))
            {
                EndAbility(AbilityEndReason.Failed);
                return;
            }

            switch (action)
            {
                case WeaponAction.Fire:
                    if (weapon.TryFire().Succeeded)
                    {
                        StartTask(new RepeatFireTask(weapon, weapon.Definition.FireInterval));
                    }
                    else
                    {
                        EndAbility(AbilityEndReason.Failed);
                    }
                    break;
                case WeaponAction.Reload:
                    weapon.Reload();
                    EndAbility(AbilityEndReason.Completed);
                    break;
                case WeaponAction.Melee:
                    weapon.Melee();
                    EndAbility(AbilityEndReason.Completed);
                    break;
            }
        }

protected override void OnInputReleased()
        {
            if (action == WeaponAction.Fire)
            {
                EndAbility(AbilityEndReason.Cancelled);
            }
        }

    }
}
