namespace CGame.Animation
{
    public interface IWeaponAnimationDefinitionLocationResolver
    {
        bool TryResolveLocation(
            WeaponId weaponId,
            out string location);
    }
}
