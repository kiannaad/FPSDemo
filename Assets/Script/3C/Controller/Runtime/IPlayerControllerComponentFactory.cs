namespace CGame
{
    public interface IPlayerControllerComponentFactory
    {
        IInventoryComponent CreateInventory();

        IQuickBarComponent CreateQuickBar(IInventoryComponent inventory);

        IPlayerCameraComponent CreatePlayerCamera();
    }
}
