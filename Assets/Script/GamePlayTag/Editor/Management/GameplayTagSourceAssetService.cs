using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CGame.GameplayTags.Editor
{
    public static class GameplayTagSourceAssetService
    {
        public static GameplayTagSource CreateAndRegister(GameplayTagConfig config, string sourceName, string folder, out string error)
        {
            if (config == null || string.IsNullOrWhiteSpace(sourceName))
            {
                error = "Config and a non-empty source name are required.";
                return null;
            }

            if (config.Sources.Any(source => source != null && source.SourceName.Equals(sourceName, StringComparison.OrdinalIgnoreCase)))
            {
                error = $"Source name '{sourceName}' is already registered.";
                return null;
            }

            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{sourceName}.asset");
            var sourceAsset = ScriptableObject.CreateInstance<GameplayTagSource>();
            sourceAsset.SetDefinition(sourceName.Trim(), Array.Empty<GameplayTagSourceNode>());
            AssetDatabase.CreateAsset(sourceAsset, assetPath);
            if (!Register(config, sourceAsset, out error))
            {
                AssetDatabase.DeleteAsset(assetPath);
                return null;
            }

            return sourceAsset;
        }

        public static bool Register(GameplayTagConfig config, GameplayTagSource source, out string error)
        {
            if (config == null || source == null)
            {
                error = "Config and source are required.";
                return false;
            }

            if (config.Sources.Any(item => item == source || item != null && item.SourceName.Equals(source.SourceName, StringComparison.OrdinalIgnoreCase)))
            {
                error = $"Source '{source.SourceName}' is already registered.";
                return false;
            }

            config.SetDefinition(config.Sources.Concat(new[] { source }), config.Redirects);
            SaveConfig(config);
            error = string.Empty;
            return true;
        }

        public static bool Remove(GameplayTagConfig config, GameplayTagSource source, out string error)
        {
            if (config == null || source == null || !config.Sources.Contains(source))
            {
                error = "The selected source is not registered in the Config prefab.";
                return false;
            }

            if (TryFindReference(config, source, out GameplayTagAssetReference reference, out error))
            {
                error = $"Source removal is blocked by {reference.AssetPath}:{reference.PropertyPath}='{reference.RawName}'.";
                return false;
            }

            if (!string.IsNullOrEmpty(error))
            {
                return false;
            }

            config.SetDefinition(config.Sources.Where(item => item != source), config.Redirects);
            SaveConfig(config);
            error = string.Empty;
            return true;
        }

        public static bool DeleteAsset(GameplayTagConfig config, GameplayTagSource source, out string error)
        {
            if (source == null)
            {
                error = "A source asset must be selected.";
                return false;
            }

            error = string.Empty;

            if (config != null && config.Sources.Contains(source))
            {
                error = "Remove the source from Config before deleting its asset.";
                return false;
            }

            if (config != null && TryFindReference(config, source, out GameplayTagAssetReference reference, out error))
            {
                error = $"Source asset deletion is blocked by {reference.AssetPath}:{reference.PropertyPath}='{reference.RawName}'.";
                return false;
            }

            if (!string.IsNullOrEmpty(error))
            {
                return false;
            }

            string path = AssetDatabase.GetAssetPath(source);
            bool deleted = !string.IsNullOrEmpty(path) && AssetDatabase.DeleteAsset(path);
            error = deleted ? string.Empty : $"Could not delete source asset '{path}'.";
            return deleted;
        }

        private static bool TryFindReference(
            GameplayTagConfig config,
            GameplayTagSource source,
            out GameplayTagAssetReference reference,
            out string error)
        {
            GameplayTagAssetScanReport report = GameplayTagAssetScanner.ScanProject(config);
            if (report.ConfigurationErrors.Count > 0)
            {
                reference = null;
                error = string.Join("\n", report.ConfigurationErrors);
                return false;
            }

            var names = new System.Collections.Generic.HashSet<string>(
                GameplayTagRenameService.CollectExplicitNames(source),
                StringComparer.OrdinalIgnoreCase);
            reference = report.References.FirstOrDefault(item => names.Contains(item.RawName) || names.Contains(item.ResolvedName));
            error = string.Empty;
            return reference != null;
        }

        private static void SaveConfig(GameplayTagConfig config)
        {
            EditorUtility.SetDirty(config);
            if (PrefabUtility.IsPartOfPrefabAsset(config.gameObject))
            {
                PrefabUtility.SavePrefabAsset(config.gameObject);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
