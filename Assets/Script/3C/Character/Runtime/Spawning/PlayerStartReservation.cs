using System;

namespace CGame
{
    public sealed class PlayerStartReservation : IDisposable
    {
        private PlayerStartRegistry registry;

        internal PlayerStartReservation(PlayerStartRegistry registry, PlayerStartInfo start)
        {
            this.registry = registry;
            Start = start;
        }

        public PlayerStartInfo Start { get; }

        public bool IsActive => registry != null;

        public void Dispose()
        {
            PlayerStartRegistry current = registry;
            registry = null;
            current?.Release(Start.Id);
        }
    }
}
