using System;
using System.Collections.Generic;
using CGame.Ability;

namespace CGame
{
    public sealed class PlayerState : IDisposable
    {
        private Pawn avatar;
        private readonly List<AbilityGrantReceipt> baseGrantReceipts = new List<AbilityGrantReceipt>();

        public PlayerState(AbilitySet baseAbilitySet, object baseGrantSource)
            : this(new[] { baseAbilitySet }, baseGrantSource)
        {
        }

        public PlayerState(IEnumerable<AbilitySet> baseAbilitySets, object baseGrantSource)
        {
            if (baseAbilitySets == null)
            {
                throw new ArgumentNullException(nameof(baseAbilitySets));
            }

            AbilitySystem = new AbilitySystemComponent(null);
            try
            {
                foreach (AbilitySet abilitySet in baseAbilitySets)
                {
                    if (abilitySet == null)
                    {
                        throw new ArgumentException("Base AbilitySets cannot contain null.", nameof(baseAbilitySets));
                    }

                    baseGrantReceipts.Add(AbilitySystem.GiveAbilitySet(abilitySet, baseGrantSource));
                }
            }
            catch
            {
                for (int index = baseGrantReceipts.Count - 1; index >= 0; index--)
                {
                    baseGrantReceipts[index].Revoke();
                }

                AbilitySystem.Dispose();
                throw;
            }
        }

        public AbilitySystemComponent AbilitySystem { get; }
        public IReadOnlyList<AbilityGrantReceipt> BaseGrantReceipts => baseGrantReceipts;
        public AbilityGrantReceipt BaseGrantReceipt => baseGrantReceipts.Count > 0
            ? baseGrantReceipts[0]
            : null;
        public Pawn Avatar => avatar;
        public Pawn CurrentPawn => avatar;
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
            for (int index = baseGrantReceipts.Count - 1; index >= 0; index--)
            {
                baseGrantReceipts[index].Revoke();
            }

            AbilitySystem.Dispose();
            IsDisposed = true;
        }
    }
}
