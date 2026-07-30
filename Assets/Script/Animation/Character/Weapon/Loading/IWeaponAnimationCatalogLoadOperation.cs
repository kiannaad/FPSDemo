using System;

namespace CGame.Animation
{
    public interface IWeaponAnimationCatalogLoadOperation : IDisposable
    {
        bool IsCompleted { get; }
        bool IsSuccessful { get; }
        WeaponAnimationCatalog Asset { get; }
        string Error { get; }
    }
}
