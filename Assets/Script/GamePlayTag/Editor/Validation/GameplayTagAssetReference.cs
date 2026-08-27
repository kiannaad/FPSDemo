using System;
using System.Collections.Generic;
using System.Linq;

namespace CGame.GameplayTags.Editor
{
    public enum GameplayTagReferenceStatus
    {
        Registered,
        NeedsMigration,
        Unregistered
    }

    public enum GameplayTagAssetTargetKind
    {
        ScriptableObject,
        PrefabComponent
    }

    public sealed class GameplayTagAssetReference
    {
        public GameplayTagAssetReference(
            string assetPath,
            GameplayTagAssetTargetKind targetKind,
            string objectPath,
            string targetTypeName,
            int componentIndex,
            string propertyPath,
            string rawName,
            string resolvedName,
            GameplayTagReferenceStatus status)
        {
            AssetPath = assetPath ?? string.Empty;
            TargetKind = targetKind;
            ObjectPath = objectPath ?? string.Empty;
            TargetTypeName = targetTypeName ?? string.Empty;
            ComponentIndex = componentIndex;
            PropertyPath = propertyPath ?? string.Empty;
            RawName = rawName ?? string.Empty;
            ResolvedName = resolvedName ?? string.Empty;
            Status = status;
        }

        public string AssetPath { get; }
        public GameplayTagAssetTargetKind TargetKind { get; }
        public string ObjectPath { get; }
        public string TargetTypeName { get; }
        public int ComponentIndex { get; }
        public string PropertyPath { get; }
        public string RawName { get; }
        public string ResolvedName { get; }
        public GameplayTagReferenceStatus Status { get; }
    }

    public sealed class GameplayTagAssetScanReport
    {
        public GameplayTagAssetScanReport(
            IEnumerable<GameplayTagAssetReference> references,
            IEnumerable<string> configurationErrors = null)
        {
            References = (references ?? Array.Empty<GameplayTagAssetReference>()).ToArray();
            ConfigurationErrors = (configurationErrors ?? Array.Empty<string>()).ToArray();
        }

        public IReadOnlyList<GameplayTagAssetReference> References { get; }
        public IReadOnlyList<string> ConfigurationErrors { get; }
        public bool HasBlockingIssues => ConfigurationErrors.Count > 0 ||
                                         References.Any(reference => reference.Status != GameplayTagReferenceStatus.Registered);
    }
}
