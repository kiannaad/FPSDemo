namespace CGame
{
    public sealed class DefaultPlayerControllerComponentFactory : IPlayerControllerComponentFactory
    {
        public IInventoryComponent CreateInventory() => new InventoryComponent();

        public IQuickBarComponent CreateQuickBar(IInventoryComponent inventory) =>
            new QuickBarComponent(inventory);

        public IPlayerCameraComponent CreatePlayerCamera() => new PlayerCameraComponent();
    }
}
