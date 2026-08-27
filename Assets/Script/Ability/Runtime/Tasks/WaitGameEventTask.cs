using System;
using CGame.GameplayTags;

namespace CGame.Ability
{
    public sealed class WaitGameEventTask : AbilityTask
    {
        private readonly GameplayTag eventTag;
        private readonly AbilityGameEventMatchPolicy matchPolicy;
        private readonly bool onlyTriggerOnce;
        private readonly Action<AbilityGameEventPayload> callback;
        private AbilityGameEventRegistration registration;
        private bool isDispatching;

        public WaitGameEventTask(
            GameplayTag eventTag,
            AbilityGameEventMatchPolicy matchPolicy,
            bool onlyTriggerOnce,
            Action<AbilityGameEventPayload> callback)
        {
            this.eventTag = eventTag;
            this.matchPolicy = matchPolicy;
            this.onlyTriggerOnce = onlyTriggerOnce;
            this.callback = callback ?? throw new ArgumentNullException(nameof(callback));
        }

        public GameplayTag EventTag => eventTag;
        public AbilityGameEventMatchPolicy MatchPolicy => matchPolicy;
        public bool OnlyTriggerOnce => onlyTriggerOnce;
        public bool IsListening => registration != null && registration.IsActive;

        protected override void OnActivate()
        {
            registration = Owner.ActivationContext.AbilitySystem.RegisterGameEvent(
                eventTag,
                matchPolicy,
                HandleGameEvent);
        }

        protected override void OnCompleted()
        {
            StopListening();
        }

        protected override void OnCancelled()
        {
            StopListening();
        }

        private void HandleGameEvent(AbilityGameEventPayload payload)
        {
            if (State != AbilityTaskState.Active || isDispatching)
            {
                return;
            }

            AbilityActivationContext context = Owner.ActivationContext;
            if (context == null ||
                (payload.ActivationHandle.IsValid &&
                 !payload.ActivationHandle.Equals(context.ActivationHandle)) ||
                (payload.SourceObject != null &&
                 !ReferenceEquals(payload.SourceObject, context.SourceObject)) ||
                !ReferenceEquals(payload.Avatar, context.Avatar))
            {
                return;
            }

            isDispatching = true;
            try
            {
                if (onlyTriggerOnce)
                {
                    CompleteTask();
                }

                callback(payload);
            }
            finally
            {
                isDispatching = false;
            }
        }

        private void StopListening()
        {
            AbilityGameEventRegistration current = registration;
            registration = null;
            current?.Dispose();
        }
    }
}
