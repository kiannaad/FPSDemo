using System.Collections.Generic;

namespace CGame.Ability
{
    public abstract class AbilityInstance
    {
        private readonly List<GameplayTagGrantHandle> activationTagGrants = new List<GameplayTagGrantHandle>();
        private readonly List<AbilityTask> activeTasks = new List<AbilityTask>();
        private AbilityActivationContext activationContext;

        public AbilityInstanceState State { get; private set; } = AbilityInstanceState.Inactive;
        public bool HasCommitted { get; private set; }
        public AbilityEndReason? LastEndReason { get; private set; }
        public AbilityActivationContext ActivationContext => activationContext;
        public int ActiveTaskCount => activeTasks.Count;

        internal void Activate(AbilityActivationContext context)
        {
            activationContext = context;
            State = AbilityInstanceState.Activating;
            HasCommitted = false;
            LastEndReason = null;

            foreach (var tag in context.Spec.Definition.ActivationOwnedTags)
            {
                activationTagGrants.Add(context.AbilitySystem.AddOwnedTag(tag));
            }

            context.Spec.ActiveInstanceCount++;
            State = AbilityInstanceState.Active;
            OnActivate();
        }

        public bool TryCommit()
        {
            if (State != AbilityInstanceState.Active || HasCommitted)
            {
                return false;
            }

            HasCommitted = true;
            OnCommit();
            return true;
        }

        public bool EndAbility(AbilityEndReason reason)
        {
            if (State != AbilityInstanceState.Active && State != AbilityInstanceState.Activating)
            {
                return false;
            }

            State = AbilityInstanceState.Ending;
            CancelActiveTasks();
            OnEnd(reason);

            for (int index = activationTagGrants.Count - 1; index >= 0; index--)
            {
                activationContext.AbilitySystem.RemoveOwnedTag(activationTagGrants[index]);
            }

            activationTagGrants.Clear();
            activationContext.Spec.ActiveInstanceCount--;
            activationContext.AbilitySystem.NotifyAbilityEnded(activationContext.Spec.Handle);
            LastEndReason = reason;
            activationContext = null;
            State = AbilityInstanceState.Inactive;
            return true;
        }

        protected T StartTask<T>(T task) where T : AbilityTask
        {
            if (task == null)
            {
                throw new System.ArgumentNullException(nameof(task));
            }

            if (State != AbilityInstanceState.Active)
            {
                throw new System.InvalidOperationException("Tasks can only start while their AbilityInstance is active.");
            }

            activeTasks.Add(task);
            try
            {
                task.AttachAndActivate(this);
            }
            catch
            {
                if (ReferenceEquals(task.Owner, this))
                {
                    task.CancelFromOwner();
                }

                activeTasks.Remove(task);
                throw;
            }

            return task;
        }

        internal void NotifyTaskTerminated(AbilityTask task)
        {
            activeTasks.Remove(task);
        }

        private void CancelActiveTasks()
        {
            for (int index = activeTasks.Count - 1; index >= 0; index--)
            {
                activeTasks[index].CancelFromOwner();
            }

            activeTasks.Clear();
        }

        protected virtual void OnActivate()
        {
        }

        protected virtual void OnCommit()
        {
        }

        protected virtual void OnInputPressed()
        {
        }

        protected virtual void OnInputReleased()
        {
        }

        internal void NotifyInputPressed()
        {
            if (State == AbilityInstanceState.Active)
            {
                OnInputPressed();
            }
        }

        internal void NotifyInputReleased()
        {
            if (State == AbilityInstanceState.Active)
            {
                OnInputReleased();
            }
        }

        protected virtual void OnEnd(AbilityEndReason reason)
        {
        }
    }
}
