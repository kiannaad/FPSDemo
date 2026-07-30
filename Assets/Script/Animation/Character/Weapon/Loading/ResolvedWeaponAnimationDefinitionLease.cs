using System;

namespace CGame.Animation
{
    public sealed class ResolvedWeaponAnimationDefinitionLease : IDisposable
    {
        private Action release;

        public ResolvedWeaponAnimationDefinitionLease(
            WeaponAnimationDefinition definition,
            Action release = null)
        {
            Definition =
                definition
                ?? throw new ArgumentNullException(nameof(definition));
            this.release = release;
        }

        public WeaponAnimationDefinition Definition { get; }
        public bool IsReleased { get; private set; }

        public void Dispose()
        {
            if (IsReleased)
            {
                return;
            }

            IsReleased = true;
            Action releaseAction = release;
            release = null;
            releaseAction?.Invoke();
        }
    }
}
