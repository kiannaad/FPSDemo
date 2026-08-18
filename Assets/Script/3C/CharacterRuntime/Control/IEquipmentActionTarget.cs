namespace CGame
{
    public interface IEquipmentActionTarget
    {
        bool Fire();

        void UpdateFireInput(bool fireHeld, float deltaTime);

        int Reload();

        bool Melee();
    }
}
