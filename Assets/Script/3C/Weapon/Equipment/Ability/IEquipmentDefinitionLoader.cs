namespace CGame
{
    public interface IEquipmentDefinitionLoader
    {
        IEquipmentDefinitionLoadOperation BeginLoad(WeaponId weaponId);
    }
}
