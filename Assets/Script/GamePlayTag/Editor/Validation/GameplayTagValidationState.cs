using System;
using System.Collections.Generic;
using System.Linq;

namespace CGame.GameplayTags.Editor
{
    public static class GameplayTagValidationState
    {
        private static readonly Dictionary<string, GameplayTagAssetReference[]> IssuesByAsset =
            new Dictionary<string, GameplayTagAssetReference[]>(StringComparer.OrdinalIgnoreCase);

        public static IReadOnlyList<GameplayTagAssetReference> CurrentIssues => IssuesByAsset.Values.SelectMany(items => items).ToArray();

        public static void UpdatePaths(IEnumerable<string> importedOrMovedPaths, IEnumerable<string> deletedOrMovedFromPaths)
        {
            foreach (string path in deletedOrMovedFromPaths ?? Array.Empty<string>())
            {
                IssuesByAsset.Remove(path);
            }

            string[] candidates = (importedOrMovedPaths ?? Array.Empty<string>())
                .Where(path => path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) ||
                               path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                .Where(path => !path.StartsWith("Assets/Tests/", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (candidates.Length == 0 || !GameplayTagConfigLocator.TryLoadUnique(out GameplayTagConfig config, out _))
            {
                return;
            }

            GameplayTagAssetScanReport report = GameplayTagAssetScanner.ScanAssetPaths(config, candidates);
            foreach (string path in candidates)
            {
                GameplayTagAssetReference[] issues = report.References
                    .Where(reference => reference.AssetPath.Equals(path, StringComparison.OrdinalIgnoreCase) &&
                                        reference.Status != GameplayTagReferenceStatus.Registered)
                    .ToArray();
                if (issues.Length == 0)
                {
                    IssuesByAsset.Remove(path);
                }
                else
                {
                    IssuesByAsset[path] = issues;
                }
            }
        }

        public static void ReplaceWith(GameplayTagAssetScanReport report)
        {
            IssuesByAsset.Clear();
            foreach (IGrouping<string, GameplayTagAssetReference> group in (report?.References ?? Array.Empty<GameplayTagAssetReference>())
                         .Where(reference => reference.Status != GameplayTagReferenceStatus.Registered)
                         .GroupBy(reference => reference.AssetPath, StringComparer.OrdinalIgnoreCase))
            {
                IssuesByAsset[group.Key] = group.ToArray();
            }
        }
    }
}
