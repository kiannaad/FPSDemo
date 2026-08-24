using System;
using System.Collections.Generic;
using CGame.Ability;
using CGame.Animation;
using CGame.GameplayTags;
using UnityEngine;
using UnityEngine.Serialization;
using RuntimeAbilityDefinition = CGame.Ability.AbilityDefinition;

namespace CGame.InventoryEquipment
{
    [Serializable]
    public sealed class WeaponAbilitySetDefinition
    {
        [SerializeReference] private List<WeaponAbilityDefinition> abilities = new List<WeaponAbilityDefinition>();
        public IReadOnlyList<WeaponAbilityDefinition> Abilities => abilities;
        public void SetAbilities(IEnumerable<WeaponAbilityDefinition> definitions) => abilities = definitions == null ? new List<WeaponAbilityDefinition>() : new List<WeaponAbilityDefinition>(definitions);
        public AbilitySet CreateAbilitySet(WeaponReloadDefinition reloadDefinition = null)
        {
            var grants = new List<AbilityGrantDefinition>();
            foreach (WeaponAbilityDefinition ability in abilities)
            {
                if (ability == null) throw new InvalidOperationException("Weapon AbilitySet contains a missing ability definition.");
                grants.Add(ability.CreateGrant(reloadDefinition));
            }
            return new AbilitySet(grants);
        }
    }

    [Serializable]
    public abstract class AbilityDefinition
    {
        [SerializeField] protected GameplayTag abilityTag;
        [SerializeField] protected GameplayTag inputTag;
        [SerializeField] private GameplayTag[] activationOwnedTags = Array.Empty<GameplayTag>();
        [SerializeField] private GameplayTag[] blockedOwnedTags = Array.Empty<GameplayTag>();
        [SerializeField] private GameplayTag[] cancelAbilityTags = Array.Empty<GameplayTag>();
        public GameplayTag AbilityTag => abilityTag;
        public GameplayTag InputTag => inputTag;
        public IReadOnlyList<GameplayTag> ActivationOwnedTags => activationOwnedTags;
        public IReadOnlyList<GameplayTag> BlockedOwnedTags => blockedOwnedTags;
        public IReadOnlyList<GameplayTag> CancelAbilityTags => cancelAbilityTags;
        public void Configure(GameplayTag newAbilityTag, GameplayTag newInputTag) { abilityTag = newAbilityTag; inputTag = newInputTag; }
        public void ConfigureTagRules(IEnumerable<GameplayTag> activationTags, IEnumerable<GameplayTag> blockedTags, IEnumerable<GameplayTag> cancelTags)
        { activationOwnedTags = Copy(activationTags); blockedOwnedTags = Copy(blockedTags); cancelAbilityTags = Copy(cancelTags); }
        protected void ConfigureBlockedOwnedTags(IEnumerable<GameplayTag> tags) => blockedOwnedTags = Copy(tags);
        protected AbilityGrantDefinition CreateGrant(RuntimeAbilityDefinition runtimeDefinition)
        {
            if (abilityTag.IsEmpty || inputTag.IsEmpty) throw new InvalidOperationException("Weapon ability and input tags must both be configured.");
            return new AbilityGrantDefinition(runtimeDefinition, inputTag);
        }
        protected static GameplayTag[] Copy(IEnumerable<GameplayTag> tags) => tags == null ? Array.Empty<GameplayTag>() : new List<GameplayTag>(tags).ToArray();
    }

    [Serializable]
    public abstract class WeaponAbilityDefinition : AbilityDefinition
    {
        public virtual AbilityGrantDefinition CreateGrant(WeaponReloadDefinition reloadDefinition = null) => CreateGrant(new WeaponActionAbilityDefinition(abilityTag, Action, InputActivationPolicy, GetActivationOwnedTags(), BlockedOwnedTags, CancelAbilityTags));
        protected abstract WeaponAction Action { get; }
        protected virtual AbilityInputActivationPolicy InputActivationPolicy => AbilityInputActivationPolicy.OnInputTriggered;
        protected virtual IEnumerable<GameplayTag> GetActivationOwnedTags() => ActivationOwnedTags;
    }
    [Serializable] public sealed class FireWeaponAbilityDefinition : WeaponAbilityDefinition { protected override WeaponAction Action => WeaponAction.Fire; }
    [Serializable]
    public sealed class ReloadWeaponAbilityDefinition : WeaponAbilityDefinition
    {
        protected override WeaponAction Action => WeaponAction.Reload;
        public override AbilityGrantDefinition CreateGrant(WeaponReloadDefinition reloadDefinition)
        {
            if (reloadDefinition == null || !reloadDefinition.IsConfigured) throw new InvalidOperationException("Reload Ability requires a configured Reload Definition.");
            reloadDefinition.Validate();
            return CreateGrant(new ConfiguredReloadAbilityDefinition(abilityTag, GetActivationOwnedTags(), BlockedOwnedTags, CancelAbilityTags, reloadDefinition));
        }
    }
    [Serializable] public sealed class RecoilWeaponAbilityDefinition : WeaponAbilityDefinition { protected override WeaponAction Action => WeaponAction.Recoil; }
    [Serializable]
    public sealed class MeleeWeaponAbilityDefinition : WeaponAbilityDefinition
    {
        [SerializeField] private AnimationClipAsset attackClip;
        public AnimationClipAsset AttackClip => attackClip;
        protected override WeaponAction Action => WeaponAction.Melee;

        public void ConfigureAttackClip(AnimationClipAsset clip) => attackClip = clip;

        public override AbilityGrantDefinition CreateGrant(WeaponReloadDefinition reloadDefinition = null)
        {
            return CreateGrant(new WeaponActionAbilityDefinition(
                abilityTag,
                Action,
                InputActivationPolicy,
                GetActivationOwnedTags(),
                BlockedOwnedTags,
                CancelAbilityTags,
                attackClip));
        }
    }
    [Serializable]
    public sealed class AimWeaponAbilityDefinition : WeaponAbilityDefinition
    {
        [SerializeField] private GameplayTag aimingStateTag;
        [SerializeField, FormerlySerializedAs("blockedOwnedTags")] private GameplayTag[] legacyBlockedOwnedTags = Array.Empty<GameplayTag>();
        protected override WeaponAction Action => WeaponAction.Aim;
        protected override AbilityInputActivationPolicy InputActivationPolicy => AbilityInputActivationPolicy.WhileInputActive;
        protected override IEnumerable<GameplayTag> GetActivationOwnedTags()
        {
            var tags = new List<GameplayTag>(ActivationOwnedTags); if (!aimingStateTag.IsEmpty) tags.Add(aimingStateTag); return tags;
        }
        public void ConfigureAimingState(GameplayTag tag, IEnumerable<GameplayTag> blockedTags) { aimingStateTag = tag; ConfigureBlockedOwnedTags(blockedTags); }
        internal IReadOnlyList<GameplayTag> LegacyBlockedOwnedTags => legacyBlockedOwnedTags;
        internal void ClearLegacyBlockedOwnedTags() => legacyBlockedOwnedTags = Array.Empty<GameplayTag>();
    }
    public enum WeaponAction { Fire, Reload, Recoil, Melee, Aim }
    internal sealed class WeaponActionAbilityDefinition : RuntimeAbilityDefinition
    {
        private readonly WeaponAction action;
        private readonly AnimationClipAsset attackClip;
        public WeaponActionAbilityDefinition(GameplayTag tag, WeaponAction action, AbilityInputActivationPolicy policy, IEnumerable<GameplayTag> activationTags, IEnumerable<GameplayTag> blockedTags, IEnumerable<GameplayTag> cancelTags, AnimationClipAsset attackClip = null) : base(tag, activationTags, null, blockedTags, policy, null, cancelTags) { this.action = action; this.attackClip = attackClip; }
        protected override AbilityInstance CreateInstance() => new WeaponActionAbilityInstance(action, attackClip);
    }
    internal sealed class WeaponActionAbilityInstance : AbilityInstance
    {
        private readonly WeaponAction action;
        private readonly AnimationClipAsset attackClip;
        private CharacterAnimInstance characterAnimation;
        private AnimationPlaybackHandle characterPlayback;
        public WeaponActionAbilityInstance(WeaponAction action, AnimationClipAsset attackClip) { this.action = action; this.attackClip = attackClip; }
        protected override void OnActivate()
        {
            if (!(ActivationContext.Spec.SourceObject is WeaponInstance weapon)) { EndAbility(AbilityEndReason.Failed); return; }
            switch (action)
            {
                case WeaponAction.Fire:
                    if (TryPerformShot(weapon).Succeeded)
                    {
                        StartTask(new RepeatFireTask(
                            () => TryPerformShot(weapon),
                            _ => EndAbility(AbilityEndReason.Failed),
                            weapon.Definition.FireInterval));
                    }
                    else
                    {
                        EndAbility(AbilityEndReason.Failed);
                    }
                    break;
                case WeaponAction.Reload: EndAbility(AbilityEndReason.Failed); break;
                case WeaponAction.Melee:
                    if (ActivationContext.AbilitySystem.Avatar is Pawn meleePawn
                        && weapon.IsArmed
                        && attackClip != null
                        && weapon.TryGetCharacterAnimation(out CharacterAnimInstance characterAnimation))
                    {
                        characterPlayback = characterAnimation.PlayAbilityAnimation(attackClip, 0);
                        if (characterPlayback == null || characterPlayback.State == AnimationPlaybackState.Failed)
                        {
                            EndAbility(AbilityEndReason.Failed);
                            return;
                        }

                        meleePawn.NotifyMeleeActivated();
                        StartTask(new WaitWeaponAnimationTask(
                            characterAnimation,
                            characterPlayback,
                            state => EndAbility(state == AnimationPlaybackState.Completed
                                ? AbilityEndReason.Completed
                                : AbilityEndReason.Failed)));
                    }
                    else
                    {
                        EndAbility(AbilityEndReason.Failed);
                    }
                    break;
                case WeaponAction.Aim: if (ActivationContext.AbilitySystem.Avatar is Pawn pawn) pawn.SetAimingFromAbility(true); else EndAbility(AbilityEndReason.Failed); break;
            }
        }

        protected override void OnEnd(AbilityEndReason reason)
        {
            if (characterPlayback != null && !characterPlayback.IsTerminal)
            {
                characterAnimation?.StopAbilityAnimation(characterPlayback);
            }

            characterPlayback = null;
            characterAnimation = null;
            if (action == WeaponAction.Aim && ActivationContext?.AbilitySystem.Avatar is Pawn pawn)
            {
                pawn.SetAimingFromAbility(false);
            }
        }
        protected override void OnInputReleased() { if (action == WeaponAction.Fire || action == WeaponAction.Aim) EndAbility(AbilityEndReason.Cancelled); }

        private FireResult TryPerformShot(WeaponInstance weapon)
        {
            if (!weapon.IsArmed)
            {
                return FireResult.Failed("Weapon is not armed.", 0);
            }

            if (weapon.Definition.RecoilProfile == null)
            {
                return FireResult.Failed("Weapon does not have a RecoilProfile.", 0);
            }

            if (!weapon.Item.TryConsumeMagazineAmmo())
            {
                return FireResult.Failed("Weapon has no ammunition.", 0);
            }

            if (!(ActivationContext?.AbilitySystem.Avatar is Pawn pawn))
            {
                return FireResult.Failed("Weapon Ability requires a Pawn avatar.", 0);
            }

            return pawn.ApplySuccessfulShot(weapon.Definition.RecoilProfile);
        }
    }

    internal sealed class WaitWeaponAnimationTask : AbilityTask
    {
        private readonly CharacterAnimInstance characterAnimation;
        private readonly AnimationPlaybackHandle playback;
        private readonly Action<AnimationPlaybackState> callback;

        public WaitWeaponAnimationTask(
            CharacterAnimInstance characterAnimation,
            AnimationPlaybackHandle playback,
            Action<AnimationPlaybackState> callback)
        {
            this.characterAnimation = characterAnimation ?? throw new ArgumentNullException(nameof(characterAnimation));
            this.playback = playback ?? throw new ArgumentNullException(nameof(playback));
            this.callback = callback ?? throw new ArgumentNullException(nameof(callback));
        }

        protected override void OnTick(float deltaTime)
        {
            if (!playback.IsTerminal) return;
            AnimationPlaybackState state = playback.State;
            CompleteTask();
            callback(state);
        }

        protected override void OnCancelled()
        {
            if (!playback.IsTerminal) characterAnimation.StopAbilityAnimation(playback);
        }
    }

}
