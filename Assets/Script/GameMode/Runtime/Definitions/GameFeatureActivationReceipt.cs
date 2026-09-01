using System;

namespace CGame
{
    public sealed class GameFeatureActivationReceipt : IDisposable
    {
        private Action deactivate;

        public GameFeatureActivationReceipt(Guid ownerId, Action deactivateAction)
        {
            OwnerId = ownerId;
            deactivate = deactivateAction ?? throw new ArgumentNullException(nameof(deactivateAction));
        }

        public Guid OwnerId { get; }
        public bool IsDisposed => deactivate == null;

        public void Dispose()
        {
            Action action = deactivate;
            deactivate = null;
            action?.Invoke();
        }
    }
}
