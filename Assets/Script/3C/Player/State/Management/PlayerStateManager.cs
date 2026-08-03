using System;
using CGame.Ability;

namespace CGame
{
    public sealed class PlayerStateManager
    {
        private PlayerState playerState;

        public bool IsInitialized => playerState != null && !playerState.IsDisposed;
        public PlayerState PlayerState => IsInitialized
            ? playerState
            : throw new InvalidOperationException("PlayerStateManager is not initialized.");

        public PlayerState Initialize(AbilitySet baseAbilitySet, object baseGrantSource)
        {
            if (IsInitialized)
            {
                throw new InvalidOperationException("PlayerStateManager is already initialized.");
            }

            playerState = new PlayerState(baseAbilitySet, baseGrantSource);
            return playerState;
        }

        public PlayerStateAvatarBinding BindAvatar(Pawn pawn)
        {
            if (pawn == null)
            {
                throw new ArgumentNullException(nameof(pawn));
            }

            return new PlayerStateAvatarBinding(PlayerState, pawn);
        }

        public void Shutdown()
        {
            playerState?.Dispose();
            playerState = null;
        }
    }
}
