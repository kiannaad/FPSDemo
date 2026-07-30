using System;

namespace CGame.Animation
{
    public interface IWeaponAnimationDefinitionResolveOperation : IDisposable
    {
        bool IsCompleted { get; }
        WeaponAnimationDefinitionResolveResult Result { get; }
    }
}
