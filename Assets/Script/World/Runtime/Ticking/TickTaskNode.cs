using System;
using System.Collections.Generic;

namespace CGame
{
    public sealed class TickTaskNode
    {
        private readonly List<TickTaskNode> prerequisites = new List<TickTaskNode>();
        private float tickInterval;
        private bool enabled = true;

        public TickTaskNode(string name, TickGroup group, Action<float> callback, float tickInterval = 0f)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            Name = string.IsNullOrWhiteSpace(name) ? "TickTask" : name;
            Group = group;
            Callback = callback;
            this.tickInterval = tickInterval;
            Timer = new TickIntervalTimer();
        }

        public string Name { get; }

        public TickGroup Group { get; }

        public long RegistrationId { get; internal set; }

        public bool IsRegistered { get; internal set; }

        public bool Enabled => enabled;

        public float TickInterval => tickInterval;

        public IReadOnlyList<TickTaskNode> Prerequisites => prerequisites;

        internal Action<float> Callback { get; }

        internal TickIntervalTimer Timer { get; }

        internal bool StructureFrozen { get; set; }

        public void AddPrerequisite(TickTaskNode prerequisite)
        {
            if (StructureFrozen)
            {
                throw new InvalidOperationException("TickTaskNode prerequisites are frozen after registration.");
            }

            if (prerequisite == null)
            {
                throw new ArgumentNullException(nameof(prerequisite));
            }

            if (ReferenceEquals(this, prerequisite))
            {
                throw new InvalidOperationException("A TickTaskNode cannot depend on itself.");
            }

            if (!prerequisites.Contains(prerequisite))
            {
                prerequisites.Add(prerequisite);
            }
        }

        public void SetEnabled(bool value)
        {
            if (enabled == value)
            {
                return;
            }

            enabled = value;
            Timer.Reset();
        }

        public void SetTickInterval(float value)
        {
            tickInterval = value;
            Timer.Reset();
        }
    }
}
