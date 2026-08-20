using System;
using CGame.Ability;
using CGame.Animation;
using CGame.GameplayTags;
using RuntimeAbilityDefinition = CGame.Ability.AbilityDefinition;

namespace CGame.InventoryEquipment
{
    internal sealed class ConfiguredReloadAbilityDefinition : RuntimeAbilityDefinition
    {
        public ConfiguredReloadAbilityDefinition(
            GameplayTag abilityTag,
            System.Collections.Generic.IEnumerable<GameplayTag> activationOwnedTags,
            System.Collections.Generic.IEnumerable<GameplayTag> blockedOwnedTags,
            System.Collections.Generic.IEnumerable<GameplayTag> cancelAbilityTags,
            WeaponReloadDefinition reloadDefinition)
            : base(
                abilityTag,
                BuildActivationTags(activationOwnedTags, Require(reloadDefinition).ReloadingStateTag),
                null,
                blockedOwnedTags,
                AbilityInputActivationPolicy.OnInputTriggered,
                null,
                cancelAbilityTags)
        {
            reloadDefinition.Validate();
        }

        protected override AbilityInstance CreateInstance()
        {
            return new ReloadWeaponAbilityInstance();
        }

        private static WeaponReloadDefinition Require(WeaponReloadDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            return definition;
        }

        private static System.Collections.Generic.IEnumerable<GameplayTag> BuildActivationTags(
            System.Collections.Generic.IEnumerable<GameplayTag> tags,
            GameplayTag reloadingTag)
        {
            var result = tags == null
                ? new System.Collections.Generic.List<GameplayTag>()
                : new System.Collections.Generic.List<GameplayTag>(tags);
            result.Add(reloadingTag);
            return result;
        }
    }

    internal sealed class ReloadWeaponAbilityInstance : AbilityInstance
    {
        private WeaponInstance weapon;
        private CharacterAnimInstance characterAnimation;
        private AnimationPlaybackHandle characterPlayback;
        private bool reloadCommitted;

        public override bool CanActivate(AbilityActivationContext context)
        {
            if (!(context?.SourceObject is WeaponInstance sourceWeapon)
                || !sourceWeapon.IsArmed
                || sourceWeapon.Item.MagazineAmmo >= sourceWeapon.MagazineCapacity
                || sourceWeapon.Item.ReserveAmmo <= 0
                || !sourceWeapon.CanBeginReloadPresentation
                || !sourceWeapon.TryGetCharacterAnimation(out _))
            {
                return false;
            }

            return true;
        }

        protected override void OnActivate()
        {
            reloadCommitted = false;
            weapon = (WeaponInstance)ActivationContext.SourceObject;
            if (!weapon.TryGetCharacterAnimation(out characterAnimation))
            {
                EndAbility(AbilityEndReason.Failed);
                return;
            }

            StartTask(new WaitGameEventTask(
                weapon.Definition.ReloadDefinition.CommitEventTag,
                AbilityGameEventMatchPolicy.Exact,
                true,
                CommitReload));

            characterPlayback = characterAnimation.PlayAbilityAnimation(
                weapon.Definition.ReloadDefinition.CharacterAnimation,
                0);
            if (characterPlayback == null || characterPlayback.State == AnimationPlaybackState.Failed)
            {
                EndAbility(AbilityEndReason.Failed);
                return;
            }

            if (!weapon.BeginReloadPresentation())
            {
                characterAnimation.StopAbilityAnimation(characterPlayback);
                EndAbility(AbilityEndReason.Failed);
                return;
            }

            StartTask(new WaitReloadPresentationTask(
                weapon,
                characterPlayback,
                FinishReload));
        }

        protected override void OnEnd(AbilityEndReason reason)
        {
            if (characterPlayback != null && !characterPlayback.IsTerminal)
            {
                characterAnimation?.StopAbilityAnimation(characterPlayback);
            }

            weapon?.StopReloadPresentation();
            characterPlayback = null;
            characterAnimation = null;
            weapon = null;
        }

        private void CommitReload(AbilityGameEventPayload payload)
        {
            if (reloadCommitted || State != AbilityInstanceState.Active || weapon == null)
            {
                return;
            }

            reloadCommitted = true;
            if (TryCommit())
            {
                int loadedAmmo = weapon.Item.ReloadMagazine(weapon.MagazineCapacity);
                weapon.NotifyReloadCommitted(loadedAmmo);
            }
        }

        private void FinishReload(AnimationPlaybackState state)
        {
            if (State != AbilityInstanceState.Active)
            {
                return;
            }

            if (state == AnimationPlaybackState.Completed)
            {
                EndAbility(reloadCommitted ? AbilityEndReason.Completed : AbilityEndReason.Failed);
                return;
            }

            EndAbility(AbilityEndReason.Cancelled);
        }
    }

    internal sealed class WaitReloadPresentationTask : AbilityTask
    {
        private readonly WeaponInstance weapon;
        private readonly AnimationPlaybackHandle characterPlayback;
        private readonly Action<AnimationPlaybackState> callback;

        public WaitReloadPresentationTask(
            WeaponInstance weapon,
            AnimationPlaybackHandle characterPlayback,
            Action<AnimationPlaybackState> callback)
        {
            this.weapon = weapon ?? throw new ArgumentNullException(nameof(weapon));
            this.characterPlayback = characterPlayback ?? throw new ArgumentNullException(nameof(characterPlayback));
            this.callback = callback ?? throw new ArgumentNullException(nameof(callback));
        }

        protected override void OnTick(float deltaTime)
        {
            weapon.FinishReloadPresentation();
            if (!characterPlayback.IsTerminal)
            {
                return;
            }

            AnimationPlaybackState terminalState = characterPlayback.State;
            CompleteTask();
            callback(terminalState);
        }

        protected override void OnCancelled()
        {
            weapon.StopReloadPresentation();
        }
    }
}
