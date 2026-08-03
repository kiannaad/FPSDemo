using CGame.Ability;
using CGame.Ability.Animation;
using CGame.Animation;

namespace CGame
{
    public sealed class WeaponSwitchAbilityInstance : AbilityInstance
    {
        private EquipmentInstance equipment;
        private EquipmentSlot slot;
        private IWeaponSwitchPresentation presentation;
        private WeaponSwitchFact startedSwitch;
        private IEquipmentDefinitionLoadOperation loadOperation;
        private IEquipmentDefinitionLease targetLease;
        private WeaponAnimationDefinition targetDefinition;
        private IAbilityAnimationPlayback targetPose;
        private IAbilityAnimationPlayback restorePose;
        private IWeaponSwitchPresentationReplacement presentationReplacement;
        private EquipmentReplacement equipmentReplacement;
        private bool targetEquipCompleted;
        private bool transactionCommitted;
        private bool isRestoring;
        private bool restorePromoted;
        private WeaponSwitchEndReason pendingFailure;

        public WeaponSwitchFact StartedSwitch => startedSwitch;

        protected override void OnActivate()
        {
            equipment = ActivationContext.SourceObject as EquipmentInstance;
            slot = equipment?.OwnerSlot;
            presentation = slot?.SwitchPresentation;
            startedSwitch = equipment?.WeaponRuntime.ActiveSwitch ?? default;
            if (equipment == null
                || equipment.IsDisposed
                || !ReferenceEquals(slot?.Current, equipment)
                || !MatchesActiveSwitch()
                || startedSwitch.FromWeaponId != equipment.WeaponId)
            {
                EndAbility(AbilityEndReason.Failed);
                return;
            }

            if (slot.DefinitionLoader == null)
            {
                FailSwitch(WeaponSwitchEndReason.TargetLoadFailed);
                return;
            }

            if (presentation == null)
            {
                FailSwitch(WeaponSwitchEndReason.TargetPlaybackFailed);
                return;
            }

            presentation.Updated += HandlePresentationUpdated;
            loadOperation = slot.DefinitionLoader.BeginLoad(
                startedSwitch.ToWeaponId);
            if (loadOperation == null)
            {
                FailSwitch(WeaponSwitchEndReason.TargetLoadFailed);
                return;
            }

            ContinueLoading();
        }

        protected override void OnEnd(AbilityEndReason reason)
        {
            if (presentation != null)
            {
                presentation.Updated -= HandlePresentationUpdated;
            }

            if (!transactionCommitted
                && reason == AbilityEndReason.Cancelled
                && targetPose != null
                && !isRestoring)
            {
                RestoreImmediatelyForCancellation();
            }

            loadOperation?.Dispose();
            loadOperation = null;
            if (!transactionCommitted)
            {
                StopIfPlaying(targetPose);
                if (!restorePromoted)
                {
                    StopIfPlaying(restorePose);
                }
                presentationReplacement?.Dispose();
                equipmentReplacement?.Dispose();
                targetLease?.Dispose();
                CancelMatchingRuntimeSwitch(reason);
            }

            equipment = null;
            slot = null;
            presentation = null;
            targetLease = null;
            targetDefinition = null;
            targetPose = null;
            restorePose = null;
            presentationReplacement = null;
            equipmentReplacement = null;
            startedSwitch = default;
            targetEquipCompleted = false;
            transactionCommitted = false;
            isRestoring = false;
            restorePromoted = false;
            pendingFailure = WeaponSwitchEndReason.None;
        }

        private void HandlePresentationUpdated()
        {
            ContinueLoading();
            TryCommitTarget();
        }

        private void ContinueLoading()
        {
            if (State != AbilityInstanceState.Active
                || loadOperation == null
                || !loadOperation.IsDone)
            {
                return;
            }

            IEquipmentDefinitionLoadOperation completed = loadOperation;
            loadOperation = null;
            if (!completed.TryTakeLease(out targetLease))
            {
                completed.Dispose();
                FailSwitch(WeaponSwitchEndReason.TargetLoadFailed);
                return;
            }

            completed.Dispose();
            targetDefinition = targetLease.Definition;
            StartTask(new PlayAnimationAbilityTask(
                presentation,
                equipment.Definition.Unequip,
                GetRequestId(0),
                HandleUnequipEnded));
        }

        private void HandleUnequipEnded(AbilityAnimationPlaybackState state)
        {
            if (State != AbilityInstanceState.Active)
            {
                return;
            }

            if (state != AbilityAnimationPlaybackState.Completed)
            {
                BeginRestore(WeaponSwitchEndReason.UnequipFailed);
                return;
            }

            presentationReplacement = presentation.PrepareReplacement(
                targetDefinition,
                checked(equipment.WeaponRuntime.Snapshot.Generation + 1u));
            if (presentationReplacement == null
                || !presentationReplacement.IsValid)
            {
                FailSwitch(WeaponSwitchEndReason.TargetPlaybackFailed);
                return;
            }

            targetPose = presentation.PlayPose(
                targetDefinition.OverlayPose,
                GetRequestId(1));
            if (targetPose == null
                || targetPose.State == AbilityAnimationPlaybackState.Failed)
            {
                FailSwitch(WeaponSwitchEndReason.TargetPlaybackFailed);
                return;
            }

            StartTask(new PlayAnimationAbilityTask(
                presentation,
                targetDefinition.Equip,
                GetRequestId(1),
                HandleTargetEquipEnded));
        }

        private void HandleTargetEquipEnded(AbilityAnimationPlaybackState state)
        {
            if (State != AbilityInstanceState.Active)
            {
                return;
            }

            if (state != AbilityAnimationPlaybackState.Completed)
            {
                BeginRestore(WeaponSwitchEndReason.TargetPlaybackFailed);
                return;
            }

            targetEquipCompleted = true;
            TryCommitTarget();
        }

        private void TryCommitTarget()
        {
            if (State != AbilityInstanceState.Active
                || isRestoring
                || !targetEquipCompleted
                || targetPose == null)
            {
                return;
            }

            if (targetPose.IsTerminal
                || targetPose.State != AbilityAnimationPlaybackState.Playing)
            {
                FailSwitch(WeaponSwitchEndReason.TargetPlaybackFailed);
                return;
            }

            AbilitySet[] targetSets =
            {
                WeaponActionAbilitySetFactory.Create(targetDefinition),
                WeaponSwitchAbilitySetFactory.Create()
            };
            if (!slot.TryPrepareReplacement(
                    targetLease,
                    targetSets,
                    out equipmentReplacement))
            {
                targetLease = null;
                FailSwitch(WeaponSwitchEndReason.TargetLoadFailed);
                return;
            }

            targetLease = null;
            if (!MatchesActiveSwitch()
                || !equipmentReplacement.Commit(
                    out EquipmentInstance previousEquipment))
            {
                FailSwitch(WeaponSwitchEndReason.TargetPlaybackFailed);
                return;
            }

            presentationReplacement.Commit();
            presentation.PromotePose(targetPose);
            transactionCommitted = true;
            if (!equipment.WeaponRuntime.CompleteSwitch(
                    startedSwitch.SwitchId,
                    targetDefinition.Capabilities))
            {
                throw new System.InvalidOperationException(
                    "A validated switch transaction could not commit its runtime fact.");
            }

            EndAbility(AbilityEndReason.Completed);
            previousEquipment.Dispose();
        }

        private void FailSwitch(WeaponSwitchEndReason reason)
        {
            if (MatchesActiveSwitch())
            {
                equipment.WeaponRuntime.FailSwitch(
                    startedSwitch.SwitchId,
                    reason);
            }

            EndAbility(AbilityEndReason.Failed);
        }

        private void BeginRestore(WeaponSwitchEndReason failure)
        {
            if (State != AbilityInstanceState.Active || isRestoring)
            {
                return;
            }

            isRestoring = true;
            pendingFailure = failure;
            StopIfPlaying(targetPose);
            presentationReplacement?.Dispose();
            presentationReplacement = null;
            restorePose = presentation.PlayPose(
                equipment.Definition.OverlayPose,
                GetRequestId(2));
            if (restorePose == null
                || restorePose.State == AbilityAnimationPlaybackState.Failed)
            {
                FailSwitch(WeaponSwitchEndReason.RestoreFailed);
                return;
            }

            StartTask(new PlayAnimationAbilityTask(
                presentation,
                equipment.Definition.Equip,
                GetRequestId(2),
                HandleRestoreEquipEnded));
        }

        private void RestoreImmediatelyForCancellation()
        {
            StopIfPlaying(targetPose);
            presentationReplacement?.Dispose();
            presentationReplacement = null;
            restorePose = presentation.PlayPose(
                equipment.Definition.OverlayPose,
                GetRequestId(2));
            presentation.PlayAnimation(
                equipment.Definition.Equip,
                GetRequestId(2));
            if (restorePose != null
                && !restorePose.IsTerminal
                && restorePose.State == AbilityAnimationPlaybackState.Playing)
            {
                presentation.PromotePose(restorePose);
                restorePromoted = true;
            }
        }

        private void HandleRestoreEquipEnded(AbilityAnimationPlaybackState state)
        {
            if (State != AbilityInstanceState.Active)
            {
                return;
            }

            if (state != AbilityAnimationPlaybackState.Completed
                || restorePose == null
                || restorePose.IsTerminal
                || restorePose.State != AbilityAnimationPlaybackState.Playing)
            {
                FailSwitch(WeaponSwitchEndReason.RestoreFailed);
                return;
            }

            presentation.PromotePose(restorePose);
            restorePromoted = true;
            FailSwitch(pendingFailure);
        }

        private bool MatchesActiveSwitch()
        {
            if (equipment == null || !startedSwitch.IsValid)
            {
                return false;
            }

            WeaponSwitchFact active = equipment.WeaponRuntime.ActiveSwitch;
            return active.IsValid
                && active.SwitchId == startedSwitch.SwitchId
                && active.FromWeaponId == startedSwitch.FromWeaponId
                && active.ToWeaponId == startedSwitch.ToWeaponId;
        }

        private void CancelMatchingRuntimeSwitch(AbilityEndReason reason)
        {
            if (!MatchesActiveSwitch())
            {
                return;
            }

            WeaponSwitchEndReason switchReason =
                reason == AbilityEndReason.SourceRemoved
                    || reason == AbilityEndReason.AvatarChanged
                    ? WeaponSwitchEndReason.OwnerDisposed
                    : WeaponSwitchEndReason.Cancelled;
            equipment.WeaponRuntime.CancelSwitch(
                startedSwitch.SwitchId,
                switchReason);
        }

        private void StopIfPlaying(IAbilityAnimationPlayback playback)
        {
            if (playback != null && !playback.IsTerminal)
            {
                presentation?.StopAnimation(playback);
            }
        }

        private long GetRequestId(int phaseOffset)
        {
            return checked((long)startedSwitch.SwitchId * 4L + phaseOffset);
        }
    }
}
