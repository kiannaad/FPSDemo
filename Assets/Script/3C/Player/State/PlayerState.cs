using System;
using CGame.Ability;

namespace CGame
{
    public sealed class PlayerState : IDisposable
    {
        private Pawn avatar;

        public PlayerState(AbilitySet baseAbilitySet, object baseGrantSource)
        {
            if (baseAbilitySet == null)
            {
                throw new ArgumentNullException(nameof(baseAbilitySet));
            }

            AbilitySystem = new AbilitySystemComponent(null);
            BaseGrantReceipt = AbilitySystem.GiveAbilitySet(baseAbilitySet, baseGrantSource);
        }

        public AbilitySystemComponent AbilitySystem { get; }
        public AbilityGrantReceipt BaseGrantReceipt { get; }
        public Pawn Avatar => avatar;
        public bool IsDisposed { get; private set; }

        public bool SetAvatar(Pawn nextAvatar)
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(PlayerState));
            }

            if (ReferenceEquals(avatar, nextAvatar))
            {
                return false;
            }

            if (nextAvatar != null &&
                nextAvatar.AbilitySystem != null &&
                !ReferenceEquals(nextAvatar.AbilitySystem, AbilitySystem))
            {
                throw new InvalidOperationException("The target Pawn is already bound to another PlayerState.");
            }

            Pawn previousAvatar = avatar;
            AbilitySystem.SetAvatar(nextAvatar);
            previousAvatar?.ClearingAbilitySystem(AbilitySystem);
            avatar = nextAvatar;
            nextAvatar?.BindingAbilitySystem(AbilitySystem);
            return true;
        }

        public bool ClearAvatar(Pawn expectedAvatar)
        {
            return ReferenceEquals(avatar, expectedAvatar) && SetAvatar(null);
        }

        public void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }

            SetAvatar(null);
            BaseGrantReceipt.Revoke();
            AbilitySystem.Dispose();
            IsDisposed = true;
        }
    }
}
