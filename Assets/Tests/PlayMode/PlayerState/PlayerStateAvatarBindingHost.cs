using UnityEngine;

namespace CGame.PlayerStatePlayModeTests
{
    public sealed class PlayerStateAvatarBindingHost : MonoBehaviour
    {
        private PlayerStateAvatarBinding binding;

        public void Initialize(PlayerStateManager manager, Pawn pawn)
        {
            binding = manager.BindAvatar(pawn);
        }

        private void OnDestroy()
        {
            binding?.Dispose();
            binding = null;
        }
    }
}
