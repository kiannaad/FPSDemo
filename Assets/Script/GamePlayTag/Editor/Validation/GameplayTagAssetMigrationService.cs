using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CGame.GameplayTags.Editor
{
    public sealed class GameplayTagAssetMigrationResult
    {
        public GameplayTagAssetMigrationResult(
            IEnumerable<string> savedAssets,
            IEnumerable<string> errors,
            GameplayTagAssetScanReport rescan)
        {
            SavedAssets = (savedAssets ?? Array.Empty<string>()).ToArray();
            Errors = (errors ?? Array.Empty<string>()).ToArray();
            Rescan = rescan;
        }

        public IReadOnlyList<string> SavedAssets { get; }
        public IReadOnlyList<string> Errors { get; }
        public GameplayTagAssetScanReport Rescan { get; }
        public bool Succeeded => Errors.Count == 0 && (Rescan == null || !Rescan.HasBlockingIssues);
    }

    public static class GameplayTagAssetMigrationService
    {
        public static GameplayTagAssetMigrationResult FixThis(
            GameplayTagConfig config,
            GameplayTagAssetReference reference)
        {
            return Fix(config, reference == null ? Array.Empty<GameplayTagAssetReference>() : new[] { reference });
        }

        public static GameplayTagAssetMigrationResult FixSelected(
            GameplayTagConfig config,
            IEnumerable<GameplayTagAssetReference> references)
        {
            return Fix(config, references);
        }

        public static GameplayTagAssetMigrationResult FixAll(GameplayTagConfig config, GameplayTagAssetScanReport report)
        {
            return Fix(config, report?.References);
        }

        private static GameplayTagAssetMigrationResult Fix(
            GameplayTagConfig config,
            IEnumerable<GameplayTagAssetReference> references)
        {
            GameplayTagAssetReference[] requested = (references ?? Array.Empty<GameplayTagAssetReference>())
                .Where(reference => reference != null &&
                                    reference.Status == GameplayTagReferenceStatus.NeedsMigration &&
                                    !string.IsNullOrEmpty(reference.ResolvedName))
                .ToArray();
            var savedAssets = new List<string>();
            var errors = new List<string>();

            foreach (IGrouping<string, GameplayTagAssetReference> group in requested.GroupBy(
                         reference => reference.AssetPath,
                         StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    if (group.Key.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                    {
                        FixPrefab(group.Key, group.ToArray());
                    }
                    else
                    {
                        FixScriptableObjects(group.Key, group.ToArray());
                    }

                    savedAssets.Add(group.Key);
                }
                catch (Exception exception)
                {
                    errors.Add($"{group.Key}: {exception.Message}");
                }
            }

            AssetDatabase.SaveAssets();
            foreach (string path in savedAssets)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }

            GameplayTagAssetScanReport rescan = GameplayTagAssetScanner.ScanAssetPaths(
                config,
                requested.Select(reference => reference.AssetPath));
            return new GameplayTagAssetMigrationResult(savedAssets, errors, rescan);
        }

        private static void FixScriptableObjects(string assetPath, IReadOnlyCollection<GameplayTagAssetReference> references)
        {
            ScriptableObject[] targets = AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<ScriptableObject>().ToArray();
            var pending = new List<PendingWrite>();
            var serializedObjects = new Dictionary<UnityEngine.Object, SerializedObject>();
            foreach (GameplayTagAssetReference reference in references)
            {
                ScriptableObject target = targets.FirstOrDefault(candidate =>
                    candidate.name == reference.ObjectPath &&
                    candidate.GetType().AssemblyQualifiedName == reference.TargetTypeName);
                pending.Add(PrepareWrite(target, reference, serializedObjects));
            }

            ApplyWrites(pending);
        }

        private static void FixPrefab(string assetPath, IReadOnlyCollection<GameplayTagAssetReference> references)
        {
            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(assetPath);
                var pending = new List<PendingWrite>();
                var serializedObjects = new Dictionary<UnityEngine.Object, SerializedObject>();
                foreach (GameplayTagAssetReference reference in references)
                {
                    Transform targetTransform = ResolveTransform(root.transform, reference.ObjectPath);
                    Type componentType = Type.GetType(reference.TargetTypeName, false);
                    Component target = componentType == null || targetTransform == null
                        ? null
                        : targetTransform.GetComponents(componentType).ElementAtOrDefault(reference.ComponentIndex);
                    pending.Add(PrepareWrite(target, reference, serializedObjects));
                }

                ApplyWrites(pending);
                PrefabUtility.SaveAsPrefabAsset(root, assetPath);
            }
            finally
            {
                if (root != null)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        private static PendingWrite PrepareWrite(
            UnityEngine.Object target,
            GameplayTagAssetReference reference,
            IDictionary<UnityEngine.Object, SerializedObject> serializedObjects)
        {
            if (target == null)
            {
                throw new InvalidOperationException($"Target '{reference.ObjectPath}' no longer exists.");
            }

            if (!serializedObjects.TryGetValue(target, out SerializedObject serializedObject))
            {
                serializedObject = new SerializedObject(target);
                serializedObjects.Add(target, serializedObject);
            }
            SerializedProperty property = serializedObject.FindProperty(reference.PropertyPath);
            if (property == null || property.propertyType != SerializedPropertyType.String)
            {
                throw new InvalidOperationException($"Property '{reference.PropertyPath}' no longer exists.");
            }

            if (!string.Equals(property.stringValue, reference.RawName, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Property '{reference.PropertyPath}' changed after preview; rescan before writing.");
            }

            return new PendingWrite(target, serializedObject, property, reference.ResolvedName, reference.PropertyPath);
        }

        private static void ApplyWrites(IEnumerable<PendingWrite> writes)
        {
            foreach (IGrouping<UnityEngine.Object, PendingWrite> group in writes.GroupBy(write => write.Target))
            {
                PendingWrite[] targetWrites = group.ToArray();
                foreach (PendingWrite write in targetWrites)
                {
                    write.Property.stringValue = write.ResolvedName;
                }

                targetWrites[0].SerializedObject.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(group.Key);

                const string containerMarker = ".serializedTags.Array.data[";
                foreach (string containerPath in targetWrites
                             .Select(write => write.PropertyPath)
                             .Select(path => new { Path = path, MarkerIndex = path.IndexOf(containerMarker, StringComparison.Ordinal) })
                             .Where(item => item.MarkerIndex >= 0)
                             .Select(item => item.Path.Substring(0, item.MarkerIndex))
                             .Distinct(StringComparer.Ordinal))
                {
                    GameplayTagSerializedPropertyWriter.RefreshContainerCaches(
                        new[] { group.Key },
                        containerPath);
                }
            }
        }

        private static Transform ResolveTransform(Transform root, string objectPath)
        {
            Transform current = root;
            if (string.IsNullOrEmpty(objectPath))
            {
                return current;
            }

            foreach (string indexText in objectPath.Split('/'))
            {
                if (!int.TryParse(indexText, out int index) || index < 0 || index >= current.childCount)
                {
                    return null;
                }

                current = current.GetChild(index);
            }

            return current;
        }

        private sealed class PendingWrite
        {
            public PendingWrite(
                UnityEngine.Object target,
                SerializedObject serializedObject,
                SerializedProperty property,
                string resolvedName,
                string propertyPath)
            {
                Target = target;
                SerializedObject = serializedObject;
                Property = property;
                ResolvedName = resolvedName;
                PropertyPath = propertyPath;
            }

            public UnityEngine.Object Target { get; }
            public SerializedObject SerializedObject { get; }
            public SerializedProperty Property { get; }
            public string ResolvedName { get; }
            public string PropertyPath { get; }
        }
    }
}
