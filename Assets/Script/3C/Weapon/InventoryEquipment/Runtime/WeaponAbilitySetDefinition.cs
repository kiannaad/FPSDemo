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

        public IReadOnlyList<WeaponAbilityDefinition> Abilities => abilities;

        public void SetAbilities(IEnumerable<WeaponAbilityDefinition> definitions)
        {
            abilities = definitions == null
                ? new List<WeaponAbilityDefinition>()
                : new List<WeaponAbilityDefinition>(definitions);
        }

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
        [SerializeField] protected GameplayTag abilityTag;
        [SerializeField] protected GameplayTag inputTag;

        public void Configure(GameplayTag newAbilityTag, GameplayTag newInputTag)
        {
            abilityTag = newAbilityTag;
            inputTag = newInputTag;
        }

        public virtual AbilityGrantDefinition CreateGrant()
        {
            if (abilityTag.IsEmpty || inputTag.IsEmpty)
            {
                throw new InvalidOperationException("Weapon ability and input tags must both be configured.");
            }

            return new AbilityGrantDefinition(
                new WeaponActionAbilityDefinition(
                    abilityTag,
                    Action,
                    InputActivationPolicy,
                    ActivationOwnedTags,
                    BlockedOwnedTags),
                inputTag);
        }

        protected abstract WeaponAction Action { get; }
        protected virtual AbilityInputActivationPolicy InputActivationPolicy => AbilityInputActivationPolicy.OnInputTriggered;
        protected virtual IEnumerable<GameplayTag> ActivationOwnedTags => null;
        protected virtual IEnumerable<GameplayTag> BlockedOwnedTags => null;
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

    [Serializable]
    public sealed class AimWeaponAbilityDefinition : WeaponAbilityDefinition
    {
        [SerializeField] private GameplayTag aimingStateTag;
        [SerializeField] private GameplayTag[] blockedOwnedTags = Array.Empty<GameplayTag>();

        protected override WeaponAction Action => WeaponAction.Aim;
        protected override AbilityInputActivationPolicy InputActivationPolicy => AbilityInputActivationPolicy.WhileInputActive;
        protected override IEnumerable<GameplayTag> ActivationOwnedTags => new[] { aimingStateTag };
        protected override IEnumerable<GameplayTag> BlockedOwnedTags => blockedOwnedTags;

        public void ConfigureAimingState(GameplayTag newAimingStateTag, IEnumerable<GameplayTag> newBlockedOwnedTags)
        {
            aimingStateTag = newAimingStateTag;
            blockedOwnedTags = newBlockedOwnedTags == null
                ? Array.Empty<GameplayTag>()
                : new List<GameplayTag>(newBlockedOwnedTags).ToArray();
        }
    }

    public enum WeaponAction
    {
        Fire,
        Reload,
        Recoil,
        Melee,
        Aim
    }

    internal sealed class WeaponActionAbilityDefinition : AbilityDefinition
    {
        private readonly WeaponAction action;

        public WeaponActionAbilityDefinition(
            GameplayTag abilityTag,
            WeaponAction action,
            AbilityInputActivationPolicy inputActivationPolicy,
            IEnumerable<GameplayTag> activationOwnedTags,
            IEnumerable<GameplayTag> blockedOwnedTags)
            : base(abilityTag, activationOwnedTags, null, blockedOwnedTags, inputActivationPolicy)
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
                case WeaponAction.Aim:
                    if (ActivationContext.AbilitySystem.Avatar is Pawn pawn)
                    {
                        pawn.SetAimingFromAbility(true);
                    }
                    else
                    {
                        EndAbility(AbilityEndReason.Failed);
                    }
                    break;
            }
        }

protected override void OnInputReleased()
        {
            if (action == WeaponAction.Fire || action == WeaponAction.Aim)
            {
                EndAbility(AbilityEndReason.Cancelled);
            }
        }

        protected override void OnEnd(AbilityEndReason reason)
        {
            if (action == WeaponAction.Aim && ActivationContext?.AbilitySystem.Avatar is Pawn pawn)
            {
                pawn.SetAimingFromAbility(false);
            }
        }

    }
}
