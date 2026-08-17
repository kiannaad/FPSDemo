namespace CGame
{
    public interface IWeaponRecoilReceiver
    {
        void ApplyingWeaponRecoil(WeaponRecoilRequest request);
        void ClearingWeaponRecoil();
    }
}
