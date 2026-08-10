using UnityEngine;

namespace CGame
{
    public abstract class GameModeDefinition : ScriptableObject
    {
        public abstract PawnData ResolvePawnData(GameStartRequest request);

        public virtual InitialInventorySet ResolveInitialInventorySet(GameStartRequest request)
        {
            return null;
        }

        public abstract GameMode CreateRuntime(GameModeCreationContext context, PawnData resolvedPawnData);
    }
}
