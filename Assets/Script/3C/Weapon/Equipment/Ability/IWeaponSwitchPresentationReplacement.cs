using System;

namespace CGame
{
    public interface IWeaponSwitchPresentationReplacement : IDisposable
    {
        bool IsValid { get; }
        void Commit();
    }
}
