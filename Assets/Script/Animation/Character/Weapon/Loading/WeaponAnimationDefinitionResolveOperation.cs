using System;

namespace CGame.Animation
{
    public sealed class WeaponAnimationDefinitionResolveOperation :
        IWeaponAnimationDefinitionResolveOperation
    {
        public bool IsCompleted { get; private set; }
        public bool IsDisposed { get; private set; }
        public WeaponAnimationDefinitionResolveResult Result { get; private set; }

        public static WeaponAnimationDefinitionResolveOperation Completed(
            WeaponAnimationDefinitionResolveResult result)
        {
            var operation = new WeaponAnimationDefinitionResolveOperation();
            operation.Complete(result);
            return operation;
        }

        public void Complete(WeaponAnimationDefinitionResolveResult result)
        {
            if (IsDisposed)
            {
                result.Lease?.Dispose();
                return;
            }

            if (IsCompleted)
            {
                throw new InvalidOperationException(
                    "A weapon definition resolve operation can only complete once.");
            }

            Result = result;
            IsCompleted = true;
        }

        public void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }

            IsDisposed = true;
            if (IsCompleted)
            {
                Result.Lease?.Dispose();
            }
        }
    }
}
