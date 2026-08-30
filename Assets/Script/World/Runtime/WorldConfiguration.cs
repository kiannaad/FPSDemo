using System.Collections.Generic;
using UnityEngine;

namespace CGame
{
    public abstract class WorldConfiguration : ScriptableObject
    {
        public abstract IReadOnlyList<WorldSubSystem> CreateWorldSubSystems();

        public virtual IReadOnlyList<PlayerSubSystem> CreatePlayerSubSystems() =>
            System.Array.Empty<PlayerSubSystem>();

        public virtual GameMode CreateGameMode(World world, Player player) => null;

        public virtual LevelRuntime CreateLevelRuntime() => null;

        public virtual GameState CreateGameState(World world) => null;
    }
}
