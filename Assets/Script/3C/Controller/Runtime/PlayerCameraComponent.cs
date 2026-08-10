namespace CGame
{
    public sealed class PlayerCameraComponent : IPlayerCameraComponent, IPlayerCameraRuntime
    {
        public bool IsDisposed { get; private set; }

        public Pawn BoundPawn { get; private set; }

        public UnityEngine.Vector3 Position { get; private set; }

        public UnityEngine.Quaternion Rotation { get; private set; } = UnityEngine.Quaternion.identity;

        public PawnBindingReceipt BindPawn(Pawn pawn)
        {
            if (IsDisposed)
            {
                throw new System.ObjectDisposedException(nameof(PlayerCameraComponent));
            }

            if (pawn == null)
            {
                throw new System.ArgumentNullException(nameof(pawn));
            }

            if (BoundPawn != null)
            {
                throw new System.InvalidOperationException("PlayerCamera is already bound to a Pawn.");
            }

            BoundPawn = pawn;
            return new PawnBindingReceipt(() =>
            {
                if (ReferenceEquals(BoundPawn, pawn))
                {
                    BoundPawn = null;
                }
            });
        }

        public void Dispose()
        {
            BoundPawn = null;
            IsDisposed = true;
        }

        public void UpdatePresentation(Pawn pawn, UnityEngine.Quaternion controlRotation)
        {
            if (IsDisposed || pawn == null || !ReferenceEquals(BoundPawn, pawn) || pawn.Host == null)
            {
                return;
            }

            Position = pawn.Host.transform.position + UnityEngine.Vector3.up * 1.6f;
            Rotation = controlRotation;
        }
    }
}
