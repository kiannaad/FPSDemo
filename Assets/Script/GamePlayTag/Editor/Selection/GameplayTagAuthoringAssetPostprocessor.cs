using System;
using System.Linq;
using UnityEditor;

namespace CGame.GameplayTags.Editor
{
    public sealed class GameplayTagAuthoringAssetPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            bool registryInputChanged = importedAssets
                .Concat(deletedAssets)
                .Concat(movedAssets)
                .Concat(movedFromAssetPaths)
                .Any(path => path.StartsWith("Assets/Data/GamePlayTag/", StringComparison.OrdinalIgnoreCase) ||
                             path.Equals("Assets/Prefab/Config/Config.prefab", StringComparison.OrdinalIgnoreCase));
            if (registryInputChanged)
            {
                GameplayTagPickerModel.InvalidateAuthoritativeConfigCache();
            }

            GameplayTagValidationState.UpdatePaths(
                importedAssets.Concat(movedAssets),
                deletedAssets.Concat(movedFromAssetPaths));
        }
    }
}
