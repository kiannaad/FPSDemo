using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CGame.GameplayTags.Editor
{
    public static class GameplayTagAssetScanner
    {
        private const string TagNameField = "tagName";

        public static GameplayTagAssetScanReport ScanProject(GameplayTagConfig config)
        {
            string[] scriptablePaths = AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(IsProductionAssetPath)
                .ToArray();
            string[] prefabPaths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(IsProductionAssetPath)
                .ToArray();
            return ScanAssetPaths(config, scriptablePaths.Concat(prefabPaths));
        }

        public static GameplayTagAssetScanReport ScanAssetPaths(GameplayTagConfig config, IEnumerable<string> assetPaths)
        {
            if (config == null)
            {
                return new GameplayTagAssetScanReport(Array.Empty<GameplayTagAssetReference>(), new[] { "GameplayTagConfig is required." });
            }

            GameplayTagRegistryBuildResult buildResult = new GameplayTagRegistryBuilder().Build(config.Sources, config.Redirects);
            if (!buildResult.Succeeded)
            {
                return new GameplayTagAssetScanReport(
                    Array.Empty<GameplayTagAssetReference>(),
                    buildResult.Errors.Select(error => error.ToString()));
            }

            var references = new List<GameplayTagAssetReference>();
            foreach (string assetPath in (assetPaths ?? Array.Empty<string>())
                         .Where(path => !string.IsNullOrWhiteSpace(path))
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                if (assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                {
                    ScanPrefab(assetPath, buildResult.Snapshot, references);
                }
                else
                {
                    ScanScriptableObjects(assetPath, buildResult.Snapshot, references);
                }
            }

            return new GameplayTagAssetScanReport(references);
        }

        public static IReadOnlyList<GameplayTagAssetReference> FindReferencesToNames(
            GameplayTagConfig config,
            IEnumerable<string> names)
        {
            var requested = new HashSet<string>(names ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            if (requested.Count == 0)
            {
                return Array.Empty<GameplayTagAssetReference>();
            }

            return ScanProject(config).References
                .Where(reference => requested.Contains(reference.RawName) || requested.Contains(reference.ResolvedName))
                .ToArray();
        }

        private static void ScanScriptableObjects(
            string assetPath,
            GameplayTagRegistrySnapshot snapshot,
            ICollection<GameplayTagAssetReference> references)
        {
            foreach (ScriptableObject target in AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<ScriptableObject>())
            {
                ScanSerializedObject(
                    target,
                    assetPath,
                    GameplayTagAssetTargetKind.ScriptableObject,
                    target.name,
                    target.GetType().AssemblyQualifiedName,
                    0,
                    snapshot,
                    references);
            }
        }

        private static void ScanPrefab(
            string assetPath,
            GameplayTagRegistrySnapshot snapshot,
            ICollection<GameplayTagAssetReference> references)
        {
            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(assetPath);
                foreach (Component component in root.GetComponentsInChildren<Component>(true).Where(component => component != null))
                {
                    Type type = component.GetType();
                    Component[] sameType = component.gameObject.GetComponents(type);
                    int componentIndex = Array.IndexOf(sameType, component);
                    ScanSerializedObject(
                        component,
                        assetPath,
                        GameplayTagAssetTargetKind.PrefabComponent,
                        GetTransformPath(root.transform, component.transform),
                        type.AssemblyQualifiedName,
                        componentIndex,
                        snapshot,
                        references);
                }
            }
            finally
            {
                if (root != null)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        private static void ScanSerializedObject(
            UnityEngine.Object target,
            string assetPath,
            GameplayTagAssetTargetKind targetKind,
            string objectPath,
            string targetTypeName,
            int componentIndex,
            GameplayTagRegistrySnapshot snapshot,
            ICollection<GameplayTagAssetReference> references)
        {
            var serializedObject = new SerializedObject(target);
            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            while (iterator.Next(enterChildren))
            {
                enterChildren = true;
                if (iterator.propertyType != SerializedPropertyType.String || iterator.name != TagNameField)
                {
                    continue;
                }

                string suffix = $".{TagNameField}";
                string parentPath = iterator.propertyPath.EndsWith(suffix, StringComparison.Ordinal)
                    ? iterator.propertyPath.Substring(0, iterator.propertyPath.Length - suffix.Length)
                    : string.Empty;
                if (!(GameplayTagSerializedPathUtility.Resolve(target, parentPath) is GameplayTag))
                {
                    continue;
                }

                string rawName = iterator.stringValue ?? string.Empty;
                if (string.IsNullOrEmpty(rawName))
                {
                    continue;
                }

                GameplayTagReferenceStatus status;
                string resolvedName;
                if (!snapshot.TryResolveName(rawName, out resolvedName))
                {
                    status = GameplayTagReferenceStatus.Unregistered;
                    resolvedName = string.Empty;
                }
                else
                {
                    status = string.Equals(rawName, resolvedName, StringComparison.Ordinal)
                        ? GameplayTagReferenceStatus.Registered
                        : GameplayTagReferenceStatus.NeedsMigration;
                }

                references.Add(new GameplayTagAssetReference(
                    assetPath,
                    targetKind,
                    objectPath,
                    targetTypeName,
                    componentIndex,
                    iterator.propertyPath,
                    rawName,
                    resolvedName,
                    status));
            }
        }

        private static string GetTransformPath(Transform root, Transform target)
        {
            if (target == root)
            {
                return string.Empty;
            }

            var indices = new Stack<int>();
            Transform current = target;
            while (current != null && current != root)
            {
                indices.Push(current.GetSiblingIndex());
                current = current.parent;
            }

            return string.Join("/", indices);
        }

        private static bool IsProductionAssetPath(string path)
        {
            return !string.IsNullOrEmpty(path) &&
                   !path.StartsWith("Assets/Tests/", StringComparison.OrdinalIgnoreCase) &&
                   !path.StartsWith("Assets/ThirdParty/", StringComparison.OrdinalIgnoreCase) &&
                   !path.StartsWith("Assets/Plugins/", StringComparison.OrdinalIgnoreCase);
        }
    }
}
