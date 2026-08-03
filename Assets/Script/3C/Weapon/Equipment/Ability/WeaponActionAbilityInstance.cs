using CGame.Ability;
using CGame.Ability.Animation;

namespace CGame
{
    public sealed class WeaponActionAbilityInstance : AbilityInstance
    {
        private readonly WeaponActionAbilityDefinition definition;
        private EquipmentInstance equipment;
        private WeaponActionFact startedAction;

        public WeaponActionAbilityInstance(WeaponActionAbilityDefinition definition)
        {
            this.definition = definition ?? throw new System.ArgumentNullException(nameof(definition));
        }

        public WeaponActionFact StartedAction => startedAction;
        public WaitGameEventTask EventTask { get; private set; }
        public PlayAnimationAbilityTask AnimationTask { get; private set; }

        protected override void OnActivate()
        {
            equipment = ActivationContext.SourceObject as EquipmentInstance;
            if (!CanUseEquipment() || !BeginAction(out startedAction))
            {
                EndAbility(AbilityEndReason.Failed);
                return;
            }

            EventTask = StartTask(new WaitGameEventTask(
                definition.EventTag,
                AbilityGameEventMatchPolicy.Exact,
                onlyTriggerOnce: true,
                HandleGameEvent));
            AnimationTask = StartTask(new PlayAnimationAbilityTask(
                equipment.AnimationPlayer,
                definition.Animation,
                checked((long)startedAction.ActionId),
                HandlePlaybackEnded));
        }

        protected override void OnEnd(AbilityEndReason reason)
        {
            CancelRuntimeAction(reason);
            equipment = null;
            startedAction = default;
        }

        private bool CanUseEquipment()
        {
            if (equipment == null || equipment.IsDisposed || equipment.AnimationPlayer == null
                || definition.Animation == null)
            {
                return false;
            }

            WeaponEquipmentSnapshot snapshot = equipment.WeaponRuntime.Snapshot;
            return snapshot.IsEquipped && snapshot.EquippedWeaponId == equipment.WeaponId;
        }

        private bool BeginAction(out WeaponActionFact action)
        {
            switch (definition.ActionKind)
            {
                case WeaponActionKind.Fire:
                    return equipment.WeaponRuntime.RequestFire(out action);
                case WeaponActionKind.Reload:
                    return equipment.WeaponRuntime.RequestReload(out action);
                case WeaponActionKind.MeleeAttack:
                    return equipment.WeaponRuntime.RequestMeleeAttack(out action);
                default:
                    action = default;
                    return false;
            }
        }

        private void HandleGameEvent(AbilityGameEventPayload payload)
        {
            if (!MatchesActiveAction() || HasCommitted || !TryCommit())
            {
                return;
            }

            equipment.WeaponRuntime.CommitAction(startedAction.ActionId);
        }

        private void HandlePlaybackEnded(AbilityAnimationPlaybackState state)
        {
            if (State != AbilityInstanceState.Active)
            {
                return;
            }

            if (state == AbilityAnimationPlaybackState.Completed && HasCommitted
                && MatchesActiveAction()
                && equipment.WeaponRuntime.CompleteAction(startedAction.ActionId))
            {
                EndAbility(AbilityEndReason.Completed);
                return;
            }

            WeaponActionEndReason reason = state == AbilityAnimationPlaybackState.Interrupted
                ? WeaponActionEndReason.AnimationInterrupted
                : state == AbilityAnimationPlaybackState.Cancelled
                    ? WeaponActionEndReason.AnimationCancelled
                    : WeaponActionEndReason.AnimationFailed;
            equipment.WeaponRuntime.CancelAction(startedAction.ActionId, reason);
            EndAbility(AbilityEndReason.Failed);
        }

        private bool MatchesActiveAction()
        {
            if (equipment == null || !startedAction.IsValid)
            {
                return false;
            }

            WeaponActionFact active = equipment.WeaponRuntime.ActiveAction;
            return active.IsValid
                && active.ActionId == startedAction.ActionId
                && active.Generation == startedAction.Generation
                && active.WeaponId == startedAction.WeaponId;
        }

        private void CancelRuntimeAction(AbilityEndReason reason)
        {
            if (!MatchesActiveAction())
            {
                return;
            }

            WeaponActionEndReason actionReason = reason == AbilityEndReason.SourceRemoved
                ? WeaponActionEndReason.Unequipped
                : reason == AbilityEndReason.AvatarChanged
                    ? WeaponActionEndReason.EquipmentChanged
                    : WeaponActionEndReason.Cancelled;
            equipment.WeaponRuntime.CancelAction(startedAction.ActionId, actionReason);
        }
    }
}
