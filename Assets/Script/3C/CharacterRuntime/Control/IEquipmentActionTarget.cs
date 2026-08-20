namespace CGame
{
    public interface IEquipmentActionTarget
    {
        bool Fire();

        void UpdateFireInput(bool fireHeld, float deltaTime);

        bool Melee();

        bool CanAcceptDirectSlotSelection(int slotIndex);
    }
}
