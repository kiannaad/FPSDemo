using System;

namespace CGame.Animation
{
    public sealed class WeaponAnimationSequencer : IDisposable
    {
        private readonly CharacterPlayablesController playablesController;
        private IWeaponAnimationDefinitionProvider definitionProvider;
        private ResolvedWeaponAnimationDefinitionLease currentDefinitionLease;
        private ResolvedWeaponAnimationDefinitionLease targetDefinitionLease;
        private IWeaponAnimationDefinitionResolveOperation targetResolveOperation;
        private WeaponRuntime boundRuntime;
        private WeaponActionFact currentAction;
        private AnimationPlaybackHandle currentHandle;
        private WeaponSwitchFact currentSwitch;
        private WeaponSwitchAnimationStage switchStage;
        private WeaponSwitchEndReason pendingSwitchFailure;
        private AnimationPlaybackHandle currentOverlayHandle;
        private AnimationPlaybackHandle unequipHandle;
        private AnimationPlaybackHandle targetOverlayHandle;
        private AnimationPlaybackHandle targetEquipHandle;
        private AnimationPlaybackHandle restoreOverlayHandle;
        private AnimationPlaybackHandle restoreEquipHandle;
        private bool isDisposed;

        public WeaponAnimationSequencer(
            CharacterPlayablesController playablesController,
            WeaponAnimationDefinition definition)
            : this(
                playablesController,
                null,
                definition == null
                    ? null
                    : new ResolvedWeaponAnimationDefinitionLease(
                        definition),
                null)
        {
        }

        public WeaponAnimationSequencer(
            CharacterPlayablesController playablesController,
            IWeaponAnimationDefinitionProvider definitionProvider,
            ResolvedWeaponAnimationDefinitionLease
                initialDefinitionLease,
            AnimationPlaybackHandle initialOverlayHandle)
        {
            this.playablesController =
                playablesController
                ?? throw new ArgumentNullException(
                    nameof(playablesController));
            currentDefinitionLease =
                initialDefinitionLease
                ?? throw new ArgumentNullException(
                    nameof(initialDefinitionLease));
            if (currentDefinitionLease.IsReleased
                || currentDefinitionLease.Definition == null)
            {
                throw new ArgumentException(
                    "A live initial weapon definition lease is required.",
                    nameof(initialDefinitionLease));
            }

            this.definitionProvider = definitionProvider;
            currentOverlayHandle = initialOverlayHandle;
        }

        public WeaponRuntime BoundRuntime => boundRuntime;
        public WeaponActionFact CurrentAction => currentAction;
        public AnimationPlaybackHandle CurrentHandle => currentHandle;
        public WeaponSwitchFact CurrentSwitch => currentSwitch;
        public WeaponSwitchAnimationStage SwitchStage => switchStage;
        public AnimationPlaybackHandle CurrentOverlayHandle =>
            currentOverlayHandle;
        public WeaponAnimationDefinition CurrentDefinition =>
            currentDefinitionLease?.Definition;
        public bool HasActiveAction =>
            currentAction.IsValid
            && currentHandle != null
            && !currentHandle.IsTerminal;
        public bool HasActiveSwitch =>
            currentSwitch.IsValid
            && switchStage != WeaponSwitchAnimationStage.None;
        internal AnimationPlaybackHandle UnequipHandle =>
            unequipHandle;
        internal AnimationPlaybackHandle TargetOverlayHandle =>
            targetOverlayHandle;
        internal AnimationPlaybackHandle TargetEquipHandle =>
            targetEquipHandle;
        internal AnimationPlaybackHandle RestoreOverlayHandle =>
            restoreOverlayHandle;
        internal AnimationPlaybackHandle RestoreEquipHandle =>
            restoreEquipHandle;

        public void BindRuntime(WeaponRuntime runtime)
        {
            if (isDisposed || boundRuntime == runtime)
            {
                return;
            }

            CancelCurrentAction(
                WeaponActionEndReason.OwnerDisposed,
                true);
            CancelCurrentSwitch(
                WeaponSwitchEndReason.OwnerDisposed,
                true);
            boundRuntime = runtime;
        }

        public bool Consume(WeaponActionFact fact)
        {
            if (isDisposed
                || !fact.IsValid
                || boundRuntime == null
                || boundRuntime.IsSwitching)
            {
                return false;
            }

            WeaponEquipmentSnapshot snapshot = boundRuntime.Snapshot;
            if (fact.Generation != snapshot.Generation
                || fact.WeaponId != snapshot.EquippedWeaponId)
            {
                return false;
            }

            if (fact.Phase == WeaponActionPhase.Started)
            {
                return StartAction(fact);
            }

            if (!currentAction.IsValid
                || fact.ActionId != currentAction.ActionId)
            {
                return false;
            }

            StopIfPlaying(currentHandle);
            ClearCurrentAction();
            return true;
        }

        public bool Consume(WeaponSwitchFact fact)
        {
            if (isDisposed
                || !fact.IsValid
                || boundRuntime == null)
            {
                return false;
            }

            if (fact.Phase == WeaponSwitchPhase.Started)
            {
                return StartSwitch(fact);
            }

            if (!currentSwitch.IsValid
                || fact.SwitchId != currentSwitch.SwitchId)
            {
                return false;
            }

            if (fact.Phase == WeaponSwitchPhase.Completed
                || fact.Phase == WeaponSwitchPhase.Failed
                || fact.Phase == WeaponSwitchPhase.Cancelled)
            {
                ClearSwitchState(false);
                return true;
            }

            return false;
        }

        public void Update()
        {
            if (isDisposed)
            {
                return;
            }

            if (HasActiveSwitch)
            {
                UpdateSwitch();
                return;
            }

            UpdateAction();
            if (!currentAction.IsValid)
            {
                EnsureCurrentOverlay();
            }
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            CancelCurrentAction(
                WeaponActionEndReason.OwnerDisposed,
                true);
            CancelCurrentSwitch(
                WeaponSwitchEndReason.OwnerDisposed,
                true);
            currentDefinitionLease?.Dispose();
            currentDefinitionLease = null;
            definitionProvider = null;
            boundRuntime = null;
            isDisposed = true;
        }

        private bool StartAction(WeaponActionFact fact)
        {
            if (currentAction.IsValid
                && currentAction.ActionId == fact.ActionId)
            {
                return true;
            }

            AnimationClipAsset asset = ResolveActionAsset(fact.Kind);
            if (asset == null
                || !asset.IsValid
                || fact.ActionId > long.MaxValue)
            {
                boundRuntime.CancelAction(
                    fact.ActionId,
                    WeaponActionEndReason.AnimationFailed);
                return false;
            }

            StopIfPlaying(currentHandle);
            currentAction = fact;
            currentHandle = playablesController.PlayAnimation(
                asset,
                (long)fact.ActionId);
            if (IsFailed(currentHandle))
            {
                ulong actionId = fact.ActionId;
                ClearCurrentAction();
                boundRuntime.CancelAction(
                    actionId,
                    WeaponActionEndReason.AnimationFailed);
                return false;
            }

            return true;
        }

        private bool StartSwitch(WeaponSwitchFact fact)
        {
            if (currentSwitch.IsValid
                && currentSwitch.SwitchId == fact.SwitchId)
            {
                return true;
            }

            if (definitionProvider == null
                || fact.SwitchId
                    > (ulong)((long.MaxValue - 3L) / 4L)
                || !MatchesActiveRuntimeSwitch(fact)
                || CurrentDefinition == null
                || CurrentDefinition.WeaponId != fact.FromWeaponId)
            {
                boundRuntime.FailSwitch(
                    fact.SwitchId,
                    WeaponSwitchEndReason.TargetLoadFailed);
                return false;
            }

            CancelCurrentAction(
                WeaponActionEndReason.Superseded,
                false);
            ClearSwitchState(true);
            currentSwitch = fact;
            switchStage = WeaponSwitchAnimationStage.Loading;
            try
            {
                targetResolveOperation =
                    definitionProvider.BeginResolve(fact.ToWeaponId);
            }
            catch
            {
                targetResolveOperation = null;
            }

            if (targetResolveOperation == null)
            {
                FailSwitchWithoutRestore(
                    WeaponSwitchEndReason.TargetLoadFailed);
                return false;
            }

            return true;
        }

        private void UpdateAction()
        {
            if (!currentAction.IsValid || currentHandle == null)
            {
                return;
            }

            if (boundRuntime == null
                || !MatchesActiveRuntimeAction(currentAction))
            {
                StopIfPlaying(currentHandle);
                ClearCurrentAction();
                return;
            }

            WeaponActionEndReason reason;
            bool complete;
            switch (currentHandle.State)
            {
                case AnimationPlaybackState.Completed:
                    reason = WeaponActionEndReason.Completed;
                    complete = true;
                    break;
                case AnimationPlaybackState.Interrupted:
                    reason =
                        WeaponActionEndReason.AnimationInterrupted;
                    complete = false;
                    break;
                case AnimationPlaybackState.Cancelled:
                    reason =
                        WeaponActionEndReason.AnimationCancelled;
                    complete = false;
                    break;
                case AnimationPlaybackState.Failed:
                    reason = WeaponActionEndReason.AnimationFailed;
                    complete = false;
                    break;
                default:
                    return;
            }

            ulong actionId = currentAction.ActionId;
            ClearCurrentAction();
            if (complete)
            {
                boundRuntime.CompleteAction(actionId);
            }
            else
            {
                boundRuntime.CancelAction(actionId, reason);
            }
        }

        private void EnsureCurrentOverlay()
        {
            if (currentOverlayHandle != null
                && !currentOverlayHandle.IsTerminal)
            {
                return;
            }

            WeaponAnimationDefinition definition =
                CurrentDefinition;
            if (definition == null
                || definition.OverlayPose == null
                || !definition.OverlayPose.IsValid)
            {
                return;
            }

            AnimationPlaybackHandle restored =
                playablesController.PlayPoseImmediate(
                    definition.OverlayPose);
            if (!IsFailed(restored))
            {
                currentOverlayHandle = restored;
            }
        }

        private void UpdateSwitch()
        {
            if (boundRuntime == null
                || !MatchesActiveRuntimeSwitch(currentSwitch))
            {
                ClearSwitchState(true);
                return;
            }

            switch (switchStage)
            {
                case WeaponSwitchAnimationStage.Loading:
                    UpdateSwitchLoading();
                    break;
                case WeaponSwitchAnimationStage.Unequipping:
                    UpdateSwitchUnequipping();
                    break;
                case WeaponSwitchAnimationStage.WaitingForTarget:
                    UpdateSwitchTarget();
                    break;
                case WeaponSwitchAnimationStage.Restoring:
                    UpdateSwitchRestore();
                    break;
            }
        }

        private void UpdateSwitchLoading()
        {
            if (targetResolveOperation == null
                || !targetResolveOperation.IsCompleted)
            {
                return;
            }

            WeaponAnimationDefinitionResolveResult result =
                targetResolveOperation.Result;
            targetResolveOperation = null;
            if (!result.IsSuccess
                || result.Definition.WeaponId
                    != currentSwitch.ToWeaponId)
            {
                result.Lease?.Dispose();
                FailSwitchWithoutRestore(
                    WeaponSwitchEndReason.TargetLoadFailed);
                return;
            }

            targetDefinitionLease = result.Lease;
            unequipHandle = playablesController.PlayAnimation(
                CurrentDefinition.Unequip,
                GetSwitchRequestId(0));
            if (IsFailed(unequipHandle))
            {
                FailSwitchWithoutRestore(
                    WeaponSwitchEndReason.UnequipFailed);
                return;
            }

            switchStage =
                WeaponSwitchAnimationStage.Unequipping;
        }

        private void UpdateSwitchUnequipping()
        {
            if (unequipHandle == null
                || !unequipHandle.IsTerminal)
            {
                return;
            }

            if (unequipHandle.State
                != AnimationPlaybackState.Completed)
            {
                FailSwitchWithoutRestore(
                    WeaponSwitchEndReason.UnequipFailed);
                return;
            }

            WeaponAnimationDefinition targetDefinition =
                targetDefinitionLease?.Definition;
            if (targetDefinition == null)
            {
                FailSwitchWithoutRestore(
                    WeaponSwitchEndReason.TargetLoadFailed);
                return;
            }

            targetOverlayHandle =
                playablesController.PlayPose(
                    targetDefinition.OverlayPose,
                    GetSwitchRequestId(1));
            targetEquipHandle =
                playablesController.PlayAnimation(
                    targetDefinition.Equip,
                    GetSwitchRequestId(1));
            if (IsFailed(targetOverlayHandle)
                || IsFailed(targetEquipHandle))
            {
                BeginRestore(
                    WeaponSwitchEndReason.TargetPlaybackFailed);
                return;
            }

            switchStage =
                WeaponSwitchAnimationStage.WaitingForTarget;
        }

        private void UpdateSwitchTarget()
        {
            if (HasUnexpectedTerminal(targetOverlayHandle)
                || HasUnexpectedTerminal(
                    targetEquipHandle,
                    AnimationPlaybackState.Completed))
            {
                BeginRestore(
                    WeaponSwitchEndReason.TargetPlaybackFailed);
                return;
            }

            if (targetOverlayHandle?.State
                    != AnimationPlaybackState.Playing
                || targetEquipHandle?.State
                    != AnimationPlaybackState.Completed)
            {
                return;
            }

            ulong switchId = currentSwitch.SwitchId;
            WeaponRuntimeCapabilities targetCapabilities =
                targetDefinitionLease.Definition.Capabilities;
            if (!boundRuntime.CompleteSwitch(
                    switchId,
                    targetCapabilities))
            {
                BeginRestore(
                    WeaponSwitchEndReason.TargetPlaybackFailed);
                return;
            }

            ResolvedWeaponAnimationDefinitionLease previousLease =
                currentDefinitionLease;
            currentDefinitionLease = targetDefinitionLease;
            targetDefinitionLease = null;
            currentOverlayHandle = targetOverlayHandle;
            targetOverlayHandle = null;
            previousLease.Dispose();
            ClearSwitchState(false);
        }

        private void BeginRestore(
            WeaponSwitchEndReason failure)
        {
            pendingSwitchFailure = failure;
            StopIfPlaying(targetOverlayHandle);
            StopIfPlaying(targetEquipHandle);
            restoreOverlayHandle =
                playablesController.PlayPose(
                    CurrentDefinition.OverlayPose,
                    GetSwitchRequestId(2));
            restoreEquipHandle =
                playablesController.PlayAnimation(
                    CurrentDefinition.Equip,
                    GetSwitchRequestId(2));
            if (IsFailed(restoreOverlayHandle)
                || IsFailed(restoreEquipHandle))
            {
                FailSwitchAfterRestore(
                    WeaponSwitchEndReason.RestoreFailed);
                return;
            }

            switchStage =
                WeaponSwitchAnimationStage.Restoring;
        }

        private void UpdateSwitchRestore()
        {
            if (HasUnexpectedTerminal(restoreOverlayHandle)
                || HasUnexpectedTerminal(
                    restoreEquipHandle,
                    AnimationPlaybackState.Completed))
            {
                FailSwitchAfterRestore(
                    WeaponSwitchEndReason.RestoreFailed);
                return;
            }

            if (restoreOverlayHandle?.State
                    != AnimationPlaybackState.Playing
                || restoreEquipHandle?.State
                    != AnimationPlaybackState.Completed)
            {
                return;
            }

            currentOverlayHandle = restoreOverlayHandle;
            restoreOverlayHandle = null;
            FailSwitchAfterRestore(pendingSwitchFailure);
        }

        private void FailSwitchWithoutRestore(
            WeaponSwitchEndReason reason)
        {
            ulong switchId = currentSwitch.SwitchId;
            ReleaseTargetResources();
            currentSwitch = default;
            switchStage = WeaponSwitchAnimationStage.None;
            pendingSwitchFailure = WeaponSwitchEndReason.None;
            unequipHandle = null;
            boundRuntime?.FailSwitch(switchId, reason);
        }

        private void FailSwitchAfterRestore(
            WeaponSwitchEndReason reason)
        {
            ulong switchId = currentSwitch.SwitchId;
            ReleaseTargetResources();
            currentSwitch = default;
            switchStage = WeaponSwitchAnimationStage.None;
            pendingSwitchFailure = WeaponSwitchEndReason.None;
            unequipHandle = null;
            restoreEquipHandle = null;
            boundRuntime?.FailSwitch(switchId, reason);
        }

        private AnimationClipAsset ResolveActionAsset(
            WeaponActionKind kind)
        {
            WeaponAnimationDefinition definition =
                CurrentDefinition;
            if (definition == null)
            {
                return null;
            }

            switch (kind)
            {
                case WeaponActionKind.Fire:
                    return definition.SupportsFire
                        ? definition.Fire
                        : null;
                case WeaponActionKind.Reload:
                    return definition.SupportsReload
                        ? definition.Reload
                        : null;
                case WeaponActionKind.MeleeAttack:
                    return definition.SupportsMeleeAttack
                        ? definition.MeleeAttack
                        : null;
                default:
                    return null;
            }
        }

        private bool MatchesActiveRuntimeAction(
            WeaponActionFact fact)
        {
            WeaponActionFact activeAction =
                boundRuntime.ActiveAction;
            return activeAction.IsValid
                && activeAction.ActionId == fact.ActionId
                && activeAction.Generation == fact.Generation
                && activeAction.WeaponId == fact.WeaponId;
        }

        private bool MatchesActiveRuntimeSwitch(
            WeaponSwitchFact fact)
        {
            WeaponSwitchFact active = boundRuntime.ActiveSwitch;
            return active.IsValid
                && active.SwitchId == fact.SwitchId
                && active.FromWeaponId == fact.FromWeaponId
                && active.ToWeaponId == fact.ToWeaponId;
        }

        private void CancelCurrentAction(
            WeaponActionEndReason reason,
            bool writeBack)
        {
            if (!currentAction.IsValid)
            {
                ClearCurrentAction();
                return;
            }

            ulong actionId = currentAction.ActionId;
            StopIfPlaying(currentHandle);
            ClearCurrentAction();
            if (writeBack && boundRuntime != null)
            {
                boundRuntime.CancelAction(actionId, reason);
            }
        }

        private void CancelCurrentSwitch(
            WeaponSwitchEndReason reason,
            bool writeBack)
        {
            if (!currentSwitch.IsValid)
            {
                ClearSwitchState(true);
                return;
            }

            ulong switchId = currentSwitch.SwitchId;
            ClearSwitchState(true);
            if (writeBack && boundRuntime != null)
            {
                boundRuntime.CancelSwitch(switchId, reason);
            }
        }

        private void ClearCurrentAction()
        {
            currentAction = default;
            currentHandle = null;
        }

        private void ClearSwitchState(bool releaseTarget)
        {
            if (targetResolveOperation != null)
            {
                targetResolveOperation.Dispose();
                targetResolveOperation = null;
            }

            StopIfPlaying(unequipHandle);
            StopIfPlaying(targetOverlayHandle);
            StopIfPlaying(targetEquipHandle);
            StopIfPlaying(restoreOverlayHandle);
            StopIfPlaying(restoreEquipHandle);
            if (releaseTarget)
            {
                targetDefinitionLease?.Dispose();
            }

            targetDefinitionLease = null;
            currentSwitch = default;
            switchStage = WeaponSwitchAnimationStage.None;
            pendingSwitchFailure = WeaponSwitchEndReason.None;
            unequipHandle = null;
            targetOverlayHandle = null;
            targetEquipHandle = null;
            restoreOverlayHandle = null;
            restoreEquipHandle = null;
        }

        private void ReleaseTargetResources()
        {
            StopIfPlaying(targetOverlayHandle);
            StopIfPlaying(targetEquipHandle);
            targetDefinitionLease?.Dispose();
            targetDefinitionLease = null;
            targetOverlayHandle = null;
            targetEquipHandle = null;
        }

        private void StopIfPlaying(
            AnimationPlaybackHandle handle)
        {
            if (handle != null && !handle.IsTerminal)
            {
                playablesController.Stop(handle);
            }
        }

        private static bool IsFailed(
            AnimationPlaybackHandle handle)
        {
            return handle == null
                || handle.State == AnimationPlaybackState.Failed;
        }

        private static bool HasUnexpectedTerminal(
            AnimationPlaybackHandle handle,
            AnimationPlaybackState allowed =
                AnimationPlaybackState.Playing)
        {
            return handle == null
                || (handle.IsTerminal
                    && handle.State != allowed);
        }

        private long GetSwitchRequestId(int phaseOffset)
        {
            return checked(
                (long)currentSwitch.SwitchId * 4L
                + phaseOffset);
        }
    }
}
