using NUnit.Framework;
using UnityEngine;
using System.IO;

namespace CGame.Network.Tests
{
    public sealed class CoverBootstrapConfigurationTests
    {
        [Test]
        public void SampleSceneConfiguration_SupportsSustainedOwnerCombat()
        {
            var configuration = UnityEditor.AssetDatabase.LoadAssetAtPath<WorldConfiguration>(
                "Assets/Settings/Gameplay/SampleScene/DefaultConfig/Gameplay/Bootstrap/GameBootstrap.asset")
                as IDedicatedServerBootstrapConfiguration;
            Assert.That(configuration, Is.Not.Null);
            foreach (var definition in configuration.EnemyArchetypeCombatCatalog.Definitions)
            {
                Assert.That(definition.PatrolRoute.LevelId, Is.EqualTo("SampleScene"));
                Assert.That(definition.PatrolRoute.WorldPoints.Count, Is.GreaterThanOrEqualTo(4),
                    "Mainline patrols need a circuit, not a short two-point shuffle.");
                float circuitLength = 0f;
                var points = definition.PatrolRoute.WorldPoints;
                for (int index = 0; index < points.Count; index++)
                    circuitLength += Vector3.Distance(points[index], points[(index + 1) % points.Count]);
                Assert.That(circuitLength, Is.GreaterThanOrEqualTo(20f));
                Assert.That(definition.FireDefinition.MagazineCapacity * definition.FireDefinition.Damage,
                    Is.GreaterThanOrEqualTo(100), "A surviving enemy must be able to reach the owner death terminal.");
                Assert.That(definition.FireDefinition.EngagementRange, Is.GreaterThanOrEqualTo(6f));
            }
        }

        [Test]
        public void LabConfiguration_PersistsCompleteCombatAndCoverCatalogs()
        {
            var configuration = UnityEditor.AssetDatabase.LoadAssetAtPath<WorldConfiguration>(
                "Assets/Settings/Gameplay/EnemyCombatPresentationLab/LabBootstrap.asset")
                as IDedicatedServerBootstrapConfiguration;
            Assert.That(configuration, Is.Not.Null);
            Assert.DoesNotThrow(() => configuration.EnemyArchetypeCombatCatalog.Validate());
            Assert.That(configuration.CoverPointCatalog.Definitions.Count, Is.EqualTo(3));
            foreach (var definition in configuration.EnemyArchetypeCombatCatalog.Definitions)
            {
                Assert.That(definition.PatrolRoute.LevelId, Is.EqualTo("EnemyCombatPresentationLab"));
                Assert.That(definition.FireDefinition.MagazineCapacity, Is.GreaterThanOrEqualTo(20),
                    "The presentation lab must sustain combat instead of stopping after three shots.");
                Assert.That(definition.FireDefinition.EngagementRange, Is.GreaterThanOrEqualTo(6f));
            }
            foreach (var cover in configuration.CoverPointCatalog.Definitions)
                Assert.That(cover.LevelId, Is.EqualTo("EnemyCombatPresentationLab"));
        }

        [Test]
        public void DedicatedBootstrap_UsesTheLoadedLevelsConfiguration()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            var root = new GameObject("LevelConfiguration");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            var bootstrapRoot = new GameObject("DedicatedBootstrap");
            try
            {
                var instance = root.AddComponent<GameInstance>();
                var expected = UnityEditor.AssetDatabase.LoadAssetAtPath<WorldConfiguration>(
                    "Assets/Settings/Gameplay/EnemyCombatPresentationLab/LabBootstrap.asset");
                Assert.That(expected, Is.Not.Null);
                var serialized = new UnityEditor.SerializedObject(instance);
                serialized.FindProperty("worldConfiguration").objectReferenceValue = expected;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var bootstrap = bootstrapRoot.AddComponent<DedicatedServerBootstrap>();
                Assert.That(bootstrap.ResolveLevelConfiguration(scene), Is.SameAs(expected));
            }
            finally
            {
                Object.DestroyImmediate(bootstrapRoot);
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [Test]
        public void DedicatedBootstrap_ExposesCoverPointCatalogToRuntime()
        {
            string gameBootstrapPath = Path.Combine(Application.dataPath, "Script/GameMode/Runtime/GameBootstrap.cs");
            string runtimePath = Path.Combine(Application.dataPath, "Script/Network/Runtime/DedicatedServer/DedicatedServerRuntime.cs");

            Assert.That(File.ReadAllText(gameBootstrapPath), Does.Contain("CoverPointCatalog CoverPointCatalog"));
            Assert.That(File.ReadAllText(runtimePath), Does.Contain("ValidateCoverPointCatalog(dedicatedBootstrap.CoverPointCatalog)"));
        }
    }
}
