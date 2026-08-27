using System;
using CGame.Ability;
using CGame.Animation;
using CGame.GameplayTags;
using RuntimeAbilityDefinition = CGame.Ability.AbilityDefinition;
using UnityEngine;

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
            if (!(context?.SourceObject is WeaponInstance sourceWeapon))
            {
                Debug.LogWarning("[ReloadTrace] CanActivate rejected: SourceObject is not WeaponInstance.");
                return false;
            }

            if (!sourceWeapon.IsArmed)
            {
                Debug.LogWarning("[ReloadTrace] CanActivate rejected: weapon is not armed.");
                return false;
            }

            Debug.Log($"[ReloadTrace] CanActivate check: weapon={sourceWeapon.Definition.name}, magazine={sourceWeapon.Item.MagazineAmmo}/{sourceWeapon.MagazineCapacity}, reserve={sourceWeapon.Item.ReserveAmmo}, weaponPresentation={sourceWeapon.CanBeginReloadPresentation}");
            if (sourceWeapon.Item.MagazineAmmo >= sourceWeapon.MagazineCapacity || sourceWeapon.Item.ReserveAmmo <= 0)
            {
                Debug.LogWarning("[ReloadTrace] CanActivate rejected: magazine is full or reserve ammo is empty.");
                return false;
            }

            if (!sourceWeapon.CanBeginReloadPresentation)
            {
                Debug.LogWarning("[ReloadTrace] CanActivate rejected: weapon reload presentation is unavailable.");
                return false;
            }

            if (!sourceWeapon.TryGetCharacterAnimation(out _))
            {
                Debug.LogWarning("[ReloadTrace] CanActivate rejected: character animation is unavailable.");
                return false;
            }

            return true;
        }

        protected override void OnActivate()
        {
            reloadCommitted = false;
            weapon = (WeaponInstance)ActivationContext.SourceObject;
            Debug.Log($"[ReloadTrace] Ability activated: weapon={weapon.Definition.name}, magazine={weapon.Item.MagazineAmmo}, reserve={weapon.Item.ReserveAmmo}");
            if (!weapon.TryGetCharacterAnimation(out characterAnimation))
            {
                Debug.LogWarning("[ReloadTrace] Activation failed: character animation lookup failed.");
                EndAbility(AbilityEndReason.Failed);
                return;
            }

            characterPlayback = characterAnimation.PlayAbilityAnimation(
                weapon.Definition.ReloadDefinition.CharacterAnimation,
                0);
            Debug.Log($"[ReloadTrace] Character reload playback: asset={weapon.Definition.ReloadDefinition.CharacterAnimation?.name}, clip={characterPlayback?.Clip?.name}, state={characterPlayback?.State}");
            if (characterPlayback == null || characterPlayback.State == AnimationPlaybackState.Failed)
            {
                Debug.LogWarning("[ReloadTrace] Activation failed: character reload animation playback failed.");
                EndAbility(AbilityEndReason.Failed);
                return;
            }

            if (!weapon.BeginReloadPresentation())
            {
                Debug.LogWarning("[ReloadTrace] Activation failed: weapon reload presentation could not start.");
                characterAnimation.StopAbilityAnimation(characterPlayback);
                EndAbility(AbilityEndReason.Failed);
                return;
            }

            Debug.Log($"[ReloadTrace] Animations started: characterClock=true, weaponController={weapon.Definition.WeaponAnimatorController != null}");

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

        private void FinishReload(AnimationPlaybackState state)
        {
            if (State != AbilityInstanceState.Active)
            {
                return;
            }

            if (state == AnimationPlaybackState.Completed)
            {
                if (!reloadCommitted && TryCommit())
                {
                    reloadCommitted = true;
                    int loadedAmmo = weapon.Item.ReloadMagazine(weapon.MagazineCapacity);
                    weapon.NotifyReloadCommitted(loadedAmmo);
                    Debug.Log($"[ReloadTrace] Animation finished -> reload committed: loaded={loadedAmmo}, magazine={weapon.Item.MagazineAmmo}, reserve={weapon.Item.ReserveAmmo}");
                }

                Debug.Log($"[ReloadTrace] Animation finished: committed={reloadCommitted}, magazine={weapon.Item.MagazineAmmo}, reserve={weapon.Item.ReserveAmmo}");
                EndAbility(reloadCommitted ? AbilityEndReason.Completed : AbilityEndReason.Failed);
                return;
            }

            Debug.LogWarning($"[ReloadTrace] Animation ended abnormally: state={state}, committed={reloadCommitted}");
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
