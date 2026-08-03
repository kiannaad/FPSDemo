using System;

namespace CGame
{
    public sealed class WeaponRuntime
    {
        private WeaponId equippedWeaponId;
        private uint generation;
        private ulong nextActionId;
        private ulong nextSwitchId;
        private WeaponActionFact activeAction;
        private WeaponSwitchFact activeSwitch;
        private WeaponRuntimeCapabilities capabilities;
        private bool isInitialized;
        private bool activeActionCommitted;

        public event Action<WeaponEquipmentSnapshot> EquipmentChanged;
        public event Action<WeaponActionFact> ActionChanged;
        public event Action<WeaponActionFact> FireCommitted;
        public event Action<WeaponSwitchFact> SwitchChanged;

        public WeaponEquipmentSnapshot Snapshot => new WeaponEquipmentSnapshot(equippedWeaponId, generation);
        public WeaponActionFact ActiveAction => activeAction;
        public WeaponRuntimeCapabilities Capabilities => capabilities;
        public bool IsInitialized => isInitialized;
        public WeaponSwitchFact ActiveSwitch => activeSwitch;
        public bool IsSwitching => activeSwitch.IsValid;
        public bool IsActiveActionCommitted => activeActionCommitted;

        public bool Initialize(
            WeaponId weaponId,
            WeaponRuntimeCapabilities initialCapabilities)
        {
            if (isInitialized
                || !weaponId.IsValid
                || !initialCapabilities.IsValid)
            {
                return false;
            }

            equippedWeaponId = weaponId;
            capabilities = initialCapabilities;
            generation = 1;
            isInitialized = true;
            EquipmentChanged?.Invoke(Snapshot);
            return true;
        }

        public bool RequestEquip(WeaponId weaponId)
        {
            return RequestSwitchWeapon(
                    weaponId,
                    out _)
                == WeaponSwitchRequestResult.Started;
        }

        public bool RequestUnequip()
        {
            return false;
        }

        public WeaponSwitchRequestResult RequestSwitchWeapon(
            WeaponId targetWeaponId,
            out WeaponSwitchFact started)
        {
            if (!isInitialized)
            {
                started = default;
                return WeaponSwitchRequestResult.NotInitialized;
            }

            if (!targetWeaponId.IsValid)
            {
                started = default;
                return WeaponSwitchRequestResult.InvalidWeaponId;
            }

            if (IsSwitching)
            {
                started = default;
                return WeaponSwitchRequestResult.AlreadySwitching;
            }

            if (targetWeaponId == equippedWeaponId)
            {
                started = default;
                return WeaponSwitchRequestResult.AlreadyEquipped;
            }

            EndActiveAction(
                WeaponActionPhase.Cancelled,
                WeaponActionEndReason.Superseded);
            started = new WeaponSwitchFact(
                ++nextSwitchId,
                equippedWeaponId,
                targetWeaponId,
                WeaponSwitchPhase.Started);
            activeSwitch = started;
            SwitchChanged?.Invoke(started);
            return WeaponSwitchRequestResult.Started;
        }

        public bool RequestPrimaryAction(
            out WeaponActionFact started,
            double authoritativeStartTime = 0d)
        {
            if (IsSwitching)
            {
                started = default;
                return false;
            }

            if (capabilities.SupportsFire)
            {
                return RequestFire(out started, authoritativeStartTime);
            }

            if (capabilities.SupportsMeleeAttack)
            {
                return RequestMeleeAttack(
                    out started,
                    authoritativeStartTime);
            }

            started = default;
            return false;
        }

        public bool RequestFire(out WeaponActionFact started, double authoritativeStartTime = 0d)
        {
            if (!isInitialized
                || !equippedWeaponId.IsValid
                || IsSwitching
                || !capabilities.SupportsFire)
            {
                started = default;
                return false;
            }

            EndActiveAction(WeaponActionPhase.Cancelled, WeaponActionEndReason.Superseded);
            started = new WeaponActionFact(
                ++nextActionId,
                generation,
                equippedWeaponId,
                WeaponActionKind.Fire,
                WeaponActionPhase.Started,
                WeaponActionEndReason.None,
                authoritativeStartTime);
            activeAction = started;
            activeActionCommitted = false;
            ActionChanged?.Invoke(started);
            return true;
        }

        public bool RequestReload(out WeaponActionFact started, double authoritativeStartTime = 0d)
        {
            if (!isInitialized
                || !equippedWeaponId.IsValid
                || IsSwitching
                || !capabilities.SupportsReload)
            {
                started = default;
                return false;
            }

            EndActiveAction(WeaponActionPhase.Cancelled, WeaponActionEndReason.Superseded);
            started = new WeaponActionFact(
                ++nextActionId,
                generation,
                equippedWeaponId,
                WeaponActionKind.Reload,
                WeaponActionPhase.Started,
                WeaponActionEndReason.None,
                authoritativeStartTime);
            activeAction = started;
            activeActionCommitted = false;
            ActionChanged?.Invoke(started);
            return true;
        }

        public bool RequestMeleeAttack(
            out WeaponActionFact started,
            double authoritativeStartTime = 0d)
        {
            if (!isInitialized
                || !equippedWeaponId.IsValid
                || IsSwitching
                || activeAction.IsValid
                || !capabilities.SupportsMeleeAttack)
            {
                started = default;
                return false;
            }

            started = new WeaponActionFact(
                ++nextActionId,
                generation,
                equippedWeaponId,
                WeaponActionKind.MeleeAttack,
                WeaponActionPhase.Started,
                WeaponActionEndReason.None,
                authoritativeStartTime);
            activeAction = started;
            activeActionCommitted = false;
            ActionChanged?.Invoke(started);
            return true;
        }

        public bool CompleteAction(ulong actionId)
        {
            return activeAction.IsValid
                && activeAction.ActionId == actionId
                && EndActiveAction(WeaponActionPhase.Completed, WeaponActionEndReason.Completed);
        }

        public bool CommitAction(ulong actionId)
        {
            if (!activeAction.IsValid
                || activeAction.ActionId != actionId
                || activeActionCommitted)
            {
                return false;
            }

            activeActionCommitted = true;
            if (activeAction.Kind == WeaponActionKind.Fire)
            {
                FireCommitted?.Invoke(activeAction);
            }

            return true;
        }

        public bool CancelAction(ulong actionId, WeaponActionEndReason reason = WeaponActionEndReason.Cancelled)
        {
            return activeAction.IsValid
                && activeAction.ActionId == actionId
                && EndActiveAction(WeaponActionPhase.Cancelled, reason);
        }

        public bool DisposeActiveAction()
        {
            return EndActiveAction(WeaponActionPhase.Cancelled, WeaponActionEndReason.OwnerDisposed);
        }

        public bool CompleteSwitch(
            ulong switchId,
            WeaponRuntimeCapabilities targetCapabilities)
        {
            if (!MatchesActiveSwitch(switchId)
                || !targetCapabilities.IsValid)
            {
                return false;
            }

            WeaponSwitchFact completed = activeSwitch.End(
                WeaponSwitchPhase.Completed,
                WeaponSwitchEndReason.Completed);
            equippedWeaponId = activeSwitch.ToWeaponId;
            capabilities = targetCapabilities;
            generation++;
            activeSwitch = default;
            EquipmentChanged?.Invoke(Snapshot);
            SwitchChanged?.Invoke(completed);
            return true;
        }

        public bool FailSwitch(
            ulong switchId,
            WeaponSwitchEndReason reason)
        {
            return EndSwitch(
                switchId,
                WeaponSwitchPhase.Failed,
                reason);
        }

        public bool CancelSwitch(
            ulong switchId,
            WeaponSwitchEndReason reason =
                WeaponSwitchEndReason.Cancelled)
        {
            return EndSwitch(
                switchId,
                WeaponSwitchPhase.Cancelled,
                reason);
        }

        public bool DisposeActiveSwitch()
        {
            return activeSwitch.IsValid
                && CancelSwitch(
                    activeSwitch.SwitchId,
                    WeaponSwitchEndReason.OwnerDisposed);
        }

        private bool EndActiveAction(WeaponActionPhase phase, WeaponActionEndReason reason)
        {
            if (!activeAction.IsValid)
            {
                return false;
            }

            WeaponActionFact ended = activeAction.End(phase, reason);
            activeAction = default;
            activeActionCommitted = false;
            ActionChanged?.Invoke(ended);
            return true;
        }

        private bool EndSwitch(
            ulong switchId,
            WeaponSwitchPhase phase,
            WeaponSwitchEndReason reason)
        {
            if (!MatchesActiveSwitch(switchId))
            {
                return false;
            }

            WeaponSwitchFact ended = activeSwitch.End(phase, reason);
            activeSwitch = default;
            SwitchChanged?.Invoke(ended);
            return true;
        }

        private bool MatchesActiveSwitch(ulong switchId)
        {
            return activeSwitch.IsValid
                && activeSwitch.SwitchId == switchId;
        }
    }
}
