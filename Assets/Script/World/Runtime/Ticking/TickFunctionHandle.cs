using System;

namespace CGame
{
    public sealed class TickFunctionHandle : IDisposable
    {
        private TickScheduler scheduler;

        internal TickFunctionHandle(TickScheduler scheduler, long registrationId, string name, TickGroup group)
        {
            this.scheduler = scheduler;
            RegistrationId = registrationId;
            Name = name;
            Group = group;
        }

        public long RegistrationId { get; }

        public string Name { get; }

        public TickGroup Group { get; }

        public bool IsRegistered => scheduler != null && scheduler.IsRegistered(RegistrationId);

        public bool IsEnabled => scheduler != null && scheduler.IsEnabled(RegistrationId);

        public bool IsFaulted => scheduler != null && scheduler.IsFaulted(RegistrationId);

        public void SetEnabled(bool enabled)
        {
            EnsureRegistered().SetEnabled(RegistrationId, enabled);
        }

        public void AddPrerequisite(TickFunctionHandle prerequisite)
        {
            EnsureRegistered().AddPrerequisite(this, prerequisite);
        }

        public void Dispose()
        {
            TickScheduler current = scheduler;
            scheduler = null;
            current?.Unregister(RegistrationId);
        }

        internal bool BelongsTo(TickScheduler candidate) => ReferenceEquals(scheduler, candidate);

        private TickScheduler EnsureRegistered()
        {
            if (scheduler == null || !scheduler.IsRegistered(RegistrationId))
            {
                throw new InvalidOperationException($"TickFunction {Name} is not registered.");
            }

            return scheduler;
        }
    }
}
