using System;
using CGame.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CGame.Tests.Gameplay
{
    public sealed class LevelDefinitionScannerTests
    {
        private const string ScenePath = "Assets/LevelDefinitionScannerTests.unity";
        private Scene scene;
        private LevelDefinition definition;

        [SetUp]
        public void SetUp()
        {
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
            definition = ScriptableObject.CreateInstance<LevelDefinition>();
        }

        [TearDown]
        public void TearDown()
        {
            if (definition != null) UnityEngine.Object.DestroyImmediate(definition);
            Undo.ClearAll();
            AssetDatabase.DeleteAsset(ScenePath);
        }

        [Test]
        public void Scan_CreatesMissingRootsAndProducesCurrentSnapshot()
        {
            LevelDefinitionScanner.ScanAndApply(scene, definition);
            Assert.That(GameObject.Find(LevelDefinitionScanner.PlayerContainerName), Is.Not.Null);
            Assert.That(GameObject.Find(LevelDefinitionScanner.EnemyContainerName), Is.Not.Null);
            Assert.That(LevelDefinitionScanner.IsSnapshotCurrent(scene, definition), Is.True);
        }

        [Test]
        public void Scan_DeletesIllegalChildrenAndNormalizesNumericOrder()
        {
            Transform players = CreateRoot(LevelDefinitionScanner.PlayerContainerName);
            CreateChild(players, "PlayerPoint9", new Vector3(9, 0, 0));
            CreateChild(players, "IllegalHelper", Vector3.zero).gameObject.AddComponent<BoxCollider>();
            CreateChild(players, "PlayerPoint2", new Vector3(2, 0, 0));
            CreateRoot(LevelDefinitionScanner.EnemyContainerName);

            LevelDefinitionScanner.ScanAndApply(scene, definition);

            Assert.That(players.childCount, Is.EqualTo(2));
            Assert.That(players.GetChild(0).name, Is.EqualTo("PlayerPoint1"));
            Assert.That(players.GetChild(0).position.x, Is.EqualTo(2));
            Assert.That(players.GetChild(1).name, Is.EqualTo("PlayerPoint2"));
            Assert.That(players.GetChild(0).GetComponent<LevelSpawnPointMarker>(), Is.Not.Null);
            Assert.That(LevelDefinitionScanner.IsSnapshotCurrent(scene, definition), Is.True);
        }

        [Test]
        public void Scan_DuplicateRootFailsWithoutCreatingOtherRoot()
        {
            CreateRoot(LevelDefinitionScanner.PlayerContainerName);
            CreateRoot(LevelDefinitionScanner.PlayerContainerName);
            Assert.Throws<InvalidOperationException>(() => LevelDefinitionScanner.ScanAndApply(scene, definition));
            Assert.That(GameObject.Find(LevelDefinitionScanner.EnemyContainerName), Is.Null);
        }

        [Test]
        public void Snapshot_TransformChangeBecomesStale()
        {
            Transform players = CreateRoot(LevelDefinitionScanner.PlayerContainerName);
            Transform point = CreateChild(players, "PlayerPoint1", Vector3.zero);
            CreateRoot(LevelDefinitionScanner.EnemyContainerName);
            LevelDefinitionScanner.ScanAndApply(scene, definition);
            point.position = Vector3.right;
            Assert.That(LevelDefinitionScanner.IsSnapshotCurrent(scene, definition), Is.False);
        }

        [Test]
        public void Scan_IsIdempotentAndSingleUndoRestoresPreviousStructure()
        {
            Transform players = CreateRoot(LevelDefinitionScanner.PlayerContainerName);
            CreateChild(players, "PlayerPoint3", Vector3.zero);
            CreateRoot(LevelDefinitionScanner.EnemyContainerName);
            LevelDefinitionScanner.ScanAndApply(scene, definition);
            Assert.That(players.childCount, Is.EqualTo(1));
            Assert.That(players.GetChild(0).name, Is.EqualTo("PlayerPoint1"));
            Assert.That(LevelDefinitionScanner.IsSnapshotCurrent(scene, definition), Is.True);
            Undo.PerformUndo();
            Assert.That(players.GetChild(0).name, Is.EqualTo("PlayerPoint3"));
            Assert.That(players.GetChild(0).GetComponent<LevelSpawnPointMarker>(), Is.Null);
        }

        [Test]
        public void Scan_PreservesOtherComponentsAndRejectsMarkerConflict()
        {
            Transform players = CreateRoot(LevelDefinitionScanner.PlayerContainerName);
            Transform point = CreateChild(players, "PlayerPoint1", Vector3.zero);
            BoxCollider collider = point.gameObject.AddComponent<BoxCollider>();
            LevelSpawnPointMarker marker = point.gameObject.AddComponent<LevelSpawnPointMarker>();
            marker.Configure(SpawnPointKind.Enemy, "EnemyPoint1");
            CreateRoot(LevelDefinitionScanner.EnemyContainerName);
            Assert.Throws<InvalidOperationException>(() => LevelDefinitionScanner.ScanAndApply(scene, definition));
            Assert.That(point.GetComponent<BoxCollider>(), Is.SameAs(collider));
        }

        [Test]
        public void AddPoint_PerformsFullScanAndSelectsNewPoint()
        {
            LevelDefinitionScanner.AddPointAndScan(scene, definition, SpawnPointKind.Enemy);
            GameObject point = GameObject.Find("EnemyPoint1");
            Assert.That(point, Is.Not.Null);
            Assert.That(Selection.activeGameObject, Is.SameAs(point));
            Assert.That(LevelDefinitionScanner.IsSnapshotCurrent(scene, definition), Is.True);
        }

        private Transform CreateRoot(string name)
        {
            var root = new GameObject(name);
            SceneManager.MoveGameObjectToScene(root, scene);
            return root.transform;
        }

        private static Transform CreateChild(Transform parent, string name, Vector3 position)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent);
            child.transform.position = position;
            return child.transform;
        }
    }
}
