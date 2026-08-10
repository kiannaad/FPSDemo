using System;

namespace CGame
{
    public sealed class PawnBindingReceipt : IDisposable
    {
        private Action release;

        public PawnBindingReceipt(Action release)
        {
            this.release = release ?? throw new ArgumentNullException(nameof(release));
        }

        public bool IsActive => release != null;

        public void Dispose()
        {
            Action current = release;
            release = null;
            current?.Invoke();
        }
    }
}
