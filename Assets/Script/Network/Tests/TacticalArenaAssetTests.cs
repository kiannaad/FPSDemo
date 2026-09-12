using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CGame.Network.Tests
{
    public sealed class TacticalArenaAssetTests
    {
        [Test]
        public void SampleScene_ContainsOriginalArenaWithoutDemoBehaviours()
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = SceneManager.GetSceneByPath("Assets/Scenes/SampleScene.unity");
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Additive);
            try
            {
                GameObject arena = scene.GetRootGameObjects().SingleOrDefault(value => value.name == "TacticalArenaEnvironment");
                Assert.That(arena, Is.Not.Null, "SampleScene must contain the replicated TPS arena, not the old block sandbox.");
                Assert.That(arena.transform.Find("tree"), Is.Not.Null);
                Assert.That(arena.transform.Find("front"), Is.Not.Null);
                Assert.That(arena.transform.Find("covers"), Is.Not.Null);
                Assert.That(arena.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(365));
                Assert.That(arena.GetComponentsInChildren<Collider>(true).Length, Is.EqualTo(208));
                Assert.That(arena.GetComponentsInChildren<MonoBehaviour>(true), Is.Empty);
                var configuration = UnityEditor.AssetDatabase.LoadAssetAtPath<WorldConfiguration>(
                    "Assets/Settings/Gameplay/SampleScene/DefaultConfig/Gameplay/Bootstrap/GameBootstrap.asset")
                    as IDedicatedServerBootstrapConfiguration;
                Assert.That(configuration, Is.Not.Null);
                SceneManager.SetActiveScene(scene);
                var navigationSettings = new UnityEditor.SerializedObject(UnityEditor.AI.NavMeshBuilder.navMeshSettingsObject);
                var sceneNavigation = navigationSettings.FindProperty("m_NavMeshData").objectReferenceValue;
                Assert.That(configuration.LevelDefinition.SceneNavigationData, Is.Not.Null,
                    "Dedicated must explicitly use the arena's scene-owned baked navigation.");
                Assert.That(configuration.LevelDefinition.SceneNavigationData, Is.SameAs(sceneNavigation),
                    "The level contract must reference the exact NavMesh loaded with SampleScene.");
                Assert.That(configuration.CoverPointCatalog.Definitions.Count, Is.GreaterThanOrEqualTo(12));
                var navigation = new UnityEnemyNavPathQuery();
                Assert.DoesNotThrow(() => configuration.CoverPointCatalog.ValidateForLevel("SampleScene", navigation));
                foreach (var definition in configuration.EnemyArchetypeCombatCatalog.Definitions)
                {
                    var points = definition.PatrolRoute.WorldPoints;
                    for (int index = 0; index < points.Count; index++)
                        Assert.That(navigation.TryCalculateCompletePath(points[index], points[(index + 1) % points.Count], out _),
                            Is.True, definition.ArchetypeId + " must have a reachable patrol circuit.");
                }
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, true);
                if (previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }
    }
}
