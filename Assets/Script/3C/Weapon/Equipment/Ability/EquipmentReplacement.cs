using System;

namespace CGame
{
    public sealed class EquipmentReplacement : IDisposable
    {
        private readonly EquipmentSlot slot;
        private readonly EquipmentInstance expectedCurrent;
        private EquipmentInstance candidate;

        internal EquipmentReplacement(
            EquipmentSlot slot,
            EquipmentInstance expectedCurrent,
            EquipmentInstance candidate)
        {
            this.slot = slot ?? throw new ArgumentNullException(nameof(slot));
            this.expectedCurrent = expectedCurrent;
            this.candidate = candidate ?? throw new ArgumentNullException(nameof(candidate));
        }

        public EquipmentInstance Candidate => candidate;
        public bool IsCommitted { get; private set; }
        public bool IsDisposed { get; private set; }
        public bool IsValid => !IsDisposed && !IsCommitted && candidate != null;

        public bool Commit(out EquipmentInstance previous)
        {
            previous = null;
            if (!IsValid
                || !slot.TryCommitReplacement(
                    this,
                    expectedCurrent,
                    candidate,
                    out previous))
            {
                return false;
            }

            IsCommitted = true;
            candidate = null;
            return true;
        }

        public void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }

            IsDisposed = true;
            if (!IsCommitted)
            {
                candidate?.Dispose();
            }

            candidate = null;
        }
    }
}
