using System;

namespace CGame
{
    public sealed class PlayerStateAvatarBinding : IDisposable
    {
        private PlayerState playerState;
        private Pawn pawn;

        internal PlayerStateAvatarBinding(PlayerState playerState, Pawn pawn)
        {
            this.playerState = playerState ?? throw new ArgumentNullException(nameof(playerState));
            this.pawn = pawn ?? throw new ArgumentNullException(nameof(pawn));
            playerState.SetAvatar(pawn);
        }

        public bool IsActive => playerState != null && pawn != null && ReferenceEquals(playerState.Avatar, pawn);

        public void Dispose()
        {
            PlayerState currentState = playerState;
            Pawn currentPawn = pawn;
            playerState = null;
            pawn = null;
            if (currentState != null && !currentState.IsDisposed)
            {
                currentState.ClearAvatar(currentPawn);
            }
        }
    }
}
