using CGame.Ability;
using UnityEngine;

namespace CGame
{
    [System.Obsolete("Use PawnDefinition. Retained temporarily so existing asset GUIDs remain valid during migration.")]
    public sealed class PawnData : PawnDefinition
    {
        public static PawnData CreateRuntime(params AbilitySet[] baseAbilitySets)
        {
            return CreateRuntime(null, baseAbilitySets);
        }

        public static PawnData CreateRuntime(GameObject pawnPrefab, params AbilitySet[] baseAbilitySets)
        {
            PawnData data = CreateInstance<PawnData>();
            data.ConfigureRuntime(pawnPrefab, baseAbilitySets);
            return data;
        }
    }
}
