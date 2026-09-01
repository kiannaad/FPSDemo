using System;
using System.Collections.Generic;
using CGame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CGame.Editor
{
    public static class LevelDefinitionScanner
    {
        public const string PlayerContainerName = "PlayerSpawnPoint";
        public const string EnemyContainerName = "EnemySpawnPoint";

        [MenuItem("CGame/Level/Scan Active Scene Spawn Points")]
        public static void ScanActiveScene()
        {
            Scene scene = EditorSceneManager.GetActiveScene();
            string[] guids = AssetDatabase.FindAssets("t:LevelDefinition");
            var matches = new List<LevelDefinition>();
            foreach (string guid in guids)
            {
                LevelDefinition candidate = AssetDatabase.LoadAssetAtPath<LevelDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (candidate != null && (string.IsNullOrEmpty(candidate.ScenePath) || candidate.ScenePath == scene.path))
                    matches.Add(candidate);
            }
            if (matches.Count != 1)
                throw new InvalidOperationException($"Active Scene requires exactly one matching LevelDefinition, found {matches.Count}.");
            ScanAndApply(scene, matches[0]);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[LevelDefinition] Scene={scene.path} SnapshotCurrent={IsSnapshotCurrent(scene, matches[0])}");
        }

        public static void ScanAndApply(Scene scene, LevelDefinition definition)
        {
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(scene.path))
                throw new InvalidOperationException("Active Scene must be loaded and saved before scanning.");
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Scan Level Spawn Points");
            try
            {
                Transform players = GetOrCreateUniqueRoot(scene, PlayerContainerName);
                Transform enemies = GetOrCreateUniqueRoot(scene, EnemyContainerName);
                var snapshots = new List<SpawnPointSnapshot>();
                Normalize(players, SpawnPointKind.Player, "PlayerPoint", snapshots);
                Normalize(enemies, SpawnPointKind.Enemy, "EnemyPoint", snapshots);
                Undo.RecordObject(definition, "Update Level Spawn Snapshot");
                definition.SetSnapshot(scene.path, snapshots.ToArray());
                EditorUtility.SetDirty(definition);
                EditorSceneManager.MarkSceneDirty(scene);
            }
            finally { Undo.CollapseUndoOperations(group); }
        }

        public static void AddPointAndScan(Scene scene, LevelDefinition definition, SpawnPointKind kind)
        {
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(scene.path))
                throw new InvalidOperationException("Active Scene must be loaded and saved before adding a point.");
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName($"Add {kind} Spawn Point");
            try
            {
                string containerName = kind == SpawnPointKind.Player ? PlayerContainerName : EnemyContainerName;
                string prefix = kind == SpawnPointKind.Player ? "PlayerPoint" : "EnemyPoint";
                Transform container = GetOrCreateUniqueRoot(scene, containerName);
                var point = new GameObject($"{prefix} {container.childCount + 1}");
                Undo.RegisterCreatedObjectUndo(point, "Add Spawn Point");
                point.transform.SetParent(container);
                ScanAndApply(scene, definition);
                Selection.activeGameObject = point;
            }
            finally { Undo.CollapseUndoOperations(group); }
        }

        public static bool IsSnapshotCurrent(Scene scene, LevelDefinition definition)
        {
            if (!scene.IsValid() || definition == null || scene.path != definition.ScenePath) return false;
            var current = new List<SpawnPointSnapshot>();
            Transform players = FindUniqueRoot(scene, PlayerContainerName);
            Transform enemies = FindUniqueRoot(scene, EnemyContainerName);
            if (players == null || enemies == null) return false;
            if (!Collect(players, SpawnPointKind.Player, current) || !Collect(enemies, SpawnPointKind.Enemy, current)) return false;
            if (current.Count != definition.SpawnPoints.Count) return false;
            for (int index = 0; index < current.Count; index++)
            {
                SpawnPointSnapshot left = current[index];
                SpawnPointSnapshot right = definition.SpawnPoints[index];
                if (left.PointId != right.PointId || left.Kind != right.Kind || left.SiblingIndex != right.SiblingIndex ||
                    left.Position != right.Position || Quaternion.Angle(left.Rotation, right.Rotation) > 0.001f) return false;
            }
            return true;
        }

        private static Transform GetOrCreateUniqueRoot(Scene scene, string name)
        {
            GameObject[] matches = Array.FindAll(scene.GetRootGameObjects(), root => root.name == name);
            if (matches.Length > 1) throw new InvalidOperationException($"Scene has duplicate roots named {name}.");
            if (matches.Length == 1) return matches[0].transform;
            var root = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(root, $"Create {name}");
            SceneManager.MoveGameObjectToScene(root, scene);
            return root.transform;
        }

        private static Transform FindUniqueRoot(Scene scene, string name)
        {
            GameObject[] matches = Array.FindAll(scene.GetRootGameObjects(), root => root.name == name);
            return matches.Length == 1 ? matches[0].transform : null;
        }

        private static void Normalize(Transform parent, SpawnPointKind kind, string prefix, List<SpawnPointSnapshot> output)
        {
            var children = new List<Transform>();
            for (int index = 0; index < parent.childCount; index++)
            {
                Transform child = parent.GetChild(index);
                if (!TryExtractNumber(child.name, prefix, out _)) { Undo.DestroyObjectImmediate(child.gameObject); index--; continue; }
                children.Add(child);
            }
            children.Sort((left, right) => ExtractNumber(left.name, prefix).CompareTo(ExtractNumber(right.name, prefix)));
            for (int index = 0; index < children.Count; index++)
            {
                Transform child = children[index];
                LevelSpawnPointMarker[] markers = child.GetComponents<LevelSpawnPointMarker>();
                if (markers.Length > 1) throw new InvalidOperationException($"{child.name} has duplicate spawn markers.");
                if (markers.Length == 1 && markers[0].Kind != kind && !string.IsNullOrEmpty(markers[0].PointId))
                    throw new InvalidOperationException($"{child.name} marker kind conflicts with its container.");
                string id = $"{prefix} {index + 1}";
                Undo.RecordObject(child.gameObject, "Normalize Spawn Point");
                child.name = id;
                child.SetSiblingIndex(index);
                LevelSpawnPointMarker marker = markers.Length == 0 ? Undo.AddComponent<LevelSpawnPointMarker>(child.gameObject) : markers[0];
                Undo.RecordObject(marker, "Configure Spawn Point Marker");
                marker.Configure(kind, id);
                output.Add(new SpawnPointSnapshot(id, kind, index, child.position, child.rotation));
            }
        }

        private static bool Collect(Transform parent, SpawnPointKind kind, List<SpawnPointSnapshot> output)
        {
            for (int index = 0; index < parent.childCount; index++)
            {
                Transform child = parent.GetChild(index);
                LevelSpawnPointMarker marker = child.GetComponent<LevelSpawnPointMarker>();
                if (marker == null || marker.Kind != kind || marker.PointId != child.name) return false;
                output.Add(new SpawnPointSnapshot(marker.PointId, kind, index, child.position, child.rotation));
            }
            return true;
        }

        private static bool TryExtractNumber(string name, string prefix, out int value)
        {
            value = 0;
            string requiredPrefix = prefix + " ";
            return name.StartsWith(requiredPrefix, StringComparison.Ordinal) &&
                   int.TryParse(name.Substring(requiredPrefix.Length), out value) && value > 0;
        }
        private static int ExtractNumber(string name, string prefix) { TryExtractNumber(name, prefix, out int value); return value; }
    }
}
