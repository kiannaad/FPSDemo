using System;
using System.Collections.Generic;
using CGame.Ability;
using CGame.Ability.Cues;
using CGame.Ability.Effects;
using CGame.Ability.Targeting;
using CGame.GameplayTags;
using UnityEngine;

namespace CGame.InventoryEquipment
{
    public sealed class WeaponDamageApplication
    {
        private readonly IAbilitySystemTargetResolver targetResolver;
        private readonly GameplayTag fireTag;
        private readonly GameplayTag impactTag;

        public WeaponDamageApplication(IAbilitySystemTargetResolver targetResolver = null)
        {
            this.targetResolver = targetResolver ?? new AbilitySystemTargetResolver();
            GameplayTag.TryCreateSerialized("GameplayCue.Weapon.Fire", out fireTag);
            GameplayTag.TryCreateSerialized("GameplayCue.Weapon.Impact", out impactTag);
        }

        public WeaponDamageApplicationResult Apply(
            AbilitySystemComponent sourceAbilitySystem,
            GameplayEffectDefinition damageEffect,
            object effectCauser,
            object sourceObject,
            Vector3 fireLocation,
            GameplayAbilityTargetDataHandle targetData)
        {
            if (sourceAbilitySystem == null) throw new ArgumentNullException(nameof(sourceAbilitySystem));
            if (damageEffect == null) throw new ArgumentNullException(nameof(damageEffect));
            if (targetData == null) throw new ArgumentNullException(nameof(targetData));

            var failures = new List<WeaponDamageEntryFailure>();
            var fireContext = new GameplayEffectContext(
                sourceAbilitySystem.Avatar,
                effectCauser,
                sourceObject,
                null,
                null,
                fireLocation,
                true);
            sourceAbilitySystem.ExecuteGameplayCue(
                fireTag,
                new GameplayCueParameters(fireContext, location: fireLocation, hasLocation: true));
            GameplayEffectSpec baseSpec = sourceAbilitySystem.MakeOutgoingSpec(
                damageEffect,
                1f,
                fireContext);
            int appliedCount = 0;
            foreach (SingleTargetHitData entry in targetData)
            {
                GameplayHitResult hit = entry.HitResult;
                var hitContext = new GameplayEffectContext(
                    sourceAbilitySystem.Avatar,
                    effectCauser,
                    sourceObject,
                    null,
                    hit,
                    hit.TraceStart,
                    true);
                sourceAbilitySystem.ExecuteGameplayCue(
                    impactTag,
                    new GameplayCueParameters(
                        hitContext,
                        location: hit.Location,
                        hasLocation: true,
                        normal: hit.Normal,
                        hasNormal: true,
                        physicMaterial: hit.PhysicMaterial));

                if (!targetResolver.TryResolve(hit.Collider, out AbilitySystemComponent targetAbilitySystem))
                {
                    continue;
                }

                GameplayEffectApplyResult applyResult = sourceAbilitySystem.ApplyGameplayEffectSpecToTarget(
                    baseSpec.CloneWithContext(hitContext),
                    targetAbilitySystem);
                if (applyResult.Succeeded)
                {
                    appliedCount++;
                }
                else
                {
                    failures.Add(new WeaponDamageEntryFailure(
                        targetData.ShotId,
                        entry.TraceIndex,
                        applyResult.FailureReason));
                }
            }

            return new WeaponDamageApplicationResult(targetData.Count, appliedCount, failures);
        }
    }

    public readonly struct WeaponDamageEntryFailure
    {
        public WeaponDamageEntryFailure(
            ulong shotId,
            int traceIndex,
            GameplayEffectFailureReason failureReason)
        {
            ShotId = shotId;
            TraceIndex = traceIndex;
            FailureReason = failureReason;
        }

        public ulong ShotId { get; }
        public int TraceIndex { get; }
        public GameplayEffectFailureReason FailureReason { get; }
    }

    public sealed class WeaponDamageApplicationResult
    {
        public WeaponDamageApplicationResult(
            int impactCount,
            int appliedCount,
            IReadOnlyList<WeaponDamageEntryFailure> failures)
        {
            ImpactCount = impactCount;
            AppliedCount = appliedCount;
            Failures = failures ?? Array.Empty<WeaponDamageEntryFailure>();
        }

        public int ImpactCount { get; }
        public int AppliedCount { get; }
        public IReadOnlyList<WeaponDamageEntryFailure> Failures { get; }
    }
}
