using System;
using CGame.Animation;

namespace CGame
{
    public interface IEquipmentDefinitionLease : IDisposable
    {
        WeaponAnimationDefinition Definition { get; }
        WeaponId WeaponId { get; }
        bool IsValid { get; }
        bool IsDisposed { get; }
    }
}
