using System;

namespace CGame.Animation
{
    public interface IWeaponAnimationDefinitionLoadOperation : IDisposable
    {
        bool IsCompleted { get; }
        bool IsSuccessful { get; }
        WeaponAnimationDefinition Asset { get; }
        string Error { get; }
    }
}
