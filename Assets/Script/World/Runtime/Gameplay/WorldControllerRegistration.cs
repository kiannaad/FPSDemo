using System;

namespace CGame
{
    public sealed class WorldControllerRegistration : IDisposable
    {
        private World world;
        private IWorldController controller;

        internal WorldControllerRegistration(World world, IWorldController controller)
        {
            this.world = world;
            this.controller = controller;
        }

        public bool IsActive => world != null && controller != null;

        public void Dispose()
        {
            World currentWorld = world;
            IWorldController currentController = controller;
            world = null;
            controller = null;
            currentWorld?.UnregisterController(currentController);
        }
    }
}
