using System;
using CGame.Ability;

namespace CGame
{
    public sealed class EnemyPlayerState : IDisposable
    {
        public EnemyPlayerState(EnemyPlayerStateDefinition definition)
        {
            Definition = definition ?? throw new System.ArgumentNullException(nameof(definition));
            AbilitySystem = new AbilitySystemComponent(this, null, null);
            try
            {
                Definition.AbilitySystemInitialization?.Initialize(AbilitySystem);
            }
            catch
            {
                AbilitySystem.Dispose();
                throw;
            }
        }

        public EnemyPlayerStateDefinition Definition { get; }
        public AbilitySystemComponent AbilitySystem { get; }
        public Pawn Avatar { get; private set; }
        public bool IsDisposed { get; private set; }

        public bool SetAvatar(Pawn avatar)
        {
            if (IsDisposed) throw new ObjectDisposedException(nameof(EnemyPlayerState));
            if (ReferenceEquals(Avatar, avatar)) return false;
            AbilitySystem.SetAvatar(avatar);
            Avatar?.ClearingAbilitySystem(AbilitySystem);
            Avatar = avatar;
            Avatar?.BindingAbilitySystem(AbilitySystem);
            return true;
        }

        public void Dispose()
        {
            if (IsDisposed) return;
            SetAvatar(null);
            AbilitySystem.Dispose();
            IsDisposed = true;
        }
    }
}
