using System.Collections.Generic;

namespace CGame.GameplayTags
{
    public sealed class GameplayTagRegistryBuildResult
    {
        internal GameplayTagRegistryBuildResult(GameplayTagRegistrySnapshot snapshot, IReadOnlyList<GameplayTagRegistryError> errors)
        {
            Snapshot = snapshot;
            Errors = errors;
        }

        public bool Succeeded => Snapshot != null && Errors.Count == 0;
        public GameplayTagRegistrySnapshot Snapshot { get; }
        public IReadOnlyList<GameplayTagRegistryError> Errors { get; }
    }
}
