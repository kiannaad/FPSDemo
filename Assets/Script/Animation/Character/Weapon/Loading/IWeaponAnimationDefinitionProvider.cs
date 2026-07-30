using System;

namespace CGame.Animation
{
    public interface IWeaponAnimationDefinitionProvider : IDisposable
    {
        IWeaponAnimationDefinitionResolveOperation BeginResolve(
            WeaponId weaponId);
    }
}
