using System;

namespace CGame.Ability
{
    public abstract class AbilityTask
    {
        public AbilityInstance Owner { get; private set; }
        public AbilityTaskState State { get; private set; } = AbilityTaskState.Created;
        public bool IsTerminal => State == AbilityTaskState.Completed || State == AbilityTaskState.Cancelled;

        internal void AttachAndActivate(AbilityInstance owner)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            if (Owner != null || State != AbilityTaskState.Created)
            {
                throw new InvalidOperationException("An AbilityTask can be started by exactly one AbilityInstance.");
            }

            Owner = owner;
            State = AbilityTaskState.Active;
            OnActivate();
        }

        internal bool CancelFromOwner()
        {
            if (State != AbilityTaskState.Active)
            {
                return false;
            }

            State = AbilityTaskState.Cancelled;
            OnCancelled();
            Owner.NotifyTaskTerminated(this);
            return true;
        }

        internal void Tick(float deltaTime)
        {
            if (State == AbilityTaskState.Active)
            {
                OnTick(deltaTime);
            }
        }

        protected bool CompleteTask()
        {
            if (State != AbilityTaskState.Active)
            {
                return false;
            }

            State = AbilityTaskState.Completed;
            OnCompleted();
            Owner.NotifyTaskTerminated(this);
            return true;
        }

        protected virtual void OnActivate()
        {
        }

        protected virtual void OnTick(float deltaTime)
        {
        }

        protected virtual void OnCompleted()
        {
        }

        protected virtual void OnCancelled()
        {
        }
    }
}
