using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CGame.GameplayTags.Editor;
using NUnit.Framework;
using UnityEngine;

namespace CGame.GameplayTags.Editor.Tests
{
    public sealed class GameplayTagSourceEditorTests
    {
        private readonly List<GameplayTagSource> sources = new List<GameplayTagSource>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameplayTagSource source in sources)
            {
                Object.DestroyImmediate(source);
            }

            sources.Clear();
        }

        [Test]
        public void AddTagPath_CreatesImplicitAncestorsAndExplicitLeaf()
        {
            GameplayTagSource source = Source("Core");

            bool added = GameplayTagSourceMutationService.AddTagPath(source, "Combat.Weapon.Fire", "Primary fire", out string error);

            Assert.That(added, Is.True, error);
            GameplayTagSourceNode combat = source.Roots.Single();
            GameplayTagSourceNode weapon = combat.Children.Single();
            GameplayTagSourceNode fire = weapon.Children.Single();
            Assert.That(combat.IsExplicitTag, Is.False);
            Assert.That(weapon.IsExplicitTag, Is.False);
            Assert.That(fire.IsExplicitTag, Is.True);
            Assert.That(fire.DevComment, Is.EqualTo("Primary fire"));
        }

        [Test]
        public void AddTagPath_RejectsCaseInsensitiveDuplicateWithoutMutation()
        {
            GameplayTagSource source = Source("Core");
            GameplayTagSourceMutationService.AddTagPath(source, "Combat.Fire", null, out _);

            bool added = GameplayTagSourceMutationService.AddTagPath(source, "combat.fire", null, out string error);

            Assert.That(added, Is.False);
            Assert.That(error, Does.Contain("already exists"));
            Assert.That(source.Roots.Single().Children.Count, Is.EqualTo(1));
        }

        [Test]
        public void DeleteSubtree_RemovesOnlySelectedSourceBranch()
        {
            GameplayTagSource source = Source("Core");
            GameplayTagSourceMutationService.AddTagPath(source, "Combat.Fire", null, out _);
            GameplayTagSourceMutationService.AddTagPath(source, "Combat.Reload", null, out _);

            bool deleted = GameplayTagSourceMutationService.DeleteSubtree(source, "Combat.Fire", out string error);

            Assert.That(deleted, Is.True, error);
            Assert.That(source.Roots.Single().Children.Select(node => node.SegmentName), Is.EqualTo(new[] { "Reload" }));
        }

        [Test]
        public void Search_LeafMatchIncludesAncestorsButNotUnrelatedSibling()
        {
            GameplayTagRegistrySnapshot snapshot = Snapshot();

            HashSet<string> visible = GameplayTagTreeSearch.CollectVisible(snapshot, "Fire");

            Assert.That(visible, Does.Contain("Combat"));
            Assert.That(visible, Does.Contain("Combat.Weapon"));
            Assert.That(visible, Does.Contain("Combat.Weapon.Fire"));
            Assert.That(visible, Does.Not.Contain("Combat.Weapon.Reload"));
        }

        [Test]
        public void Search_MiddleNodeMatchIncludesCompleteSubtree()
        {
            GameplayTagRegistrySnapshot snapshot = Snapshot();

            HashSet<string> visible = GameplayTagTreeSearch.CollectVisible(snapshot, "Weapon");

            Assert.That(visible, Does.Contain("Combat.Weapon.Fire"));
            Assert.That(visible, Does.Contain("Combat.Weapon.Reload"));
        }

        [Test]
        public void ConfigLocator_FindsTheSingleAuthoritativeConfigPrefab()
        {
            bool found = GameplayTagConfigLocator.TryLoadUnique(out GameplayTagConfig config, out string error);

            Assert.That(found, Is.True, error);
            Assert.That(config.name, Is.EqualTo("Config"));
            Assert.That(config.Sources.Count, Is.EqualTo(1));
        }

        [Test]
        public void RefreshData_AfterConfigRecovery_ClearsStaleConfigError()
        {
            GameplayTagManagerWindow window = ScriptableObject.CreateInstance<GameplayTagManagerWindow>();
            try
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                FieldInfo configError = typeof(GameplayTagManagerWindow).GetField("configError", flags);
                FieldInfo operationMessage = typeof(GameplayTagManagerWindow).GetField("operationMessage", flags);
                MethodInfo refreshData = typeof(GameplayTagManagerWindow).GetMethod("RefreshData", flags);
                Assert.That(configError, Is.Not.Null);
                Assert.That(operationMessage, Is.Not.Null);
                Assert.That(refreshData, Is.Not.Null);

                configError.SetValue(window, "stale config error");
                operationMessage.SetValue(window, "Expected one GameplayTagConfig prefab but found 2.");
                refreshData.Invoke(window, null);

                Assert.That(configError.GetValue(window), Is.EqualTo(string.Empty));
                Assert.That(operationMessage.GetValue(window), Is.EqualTo(string.Empty));
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }

        private GameplayTagRegistrySnapshot Snapshot()
        {
            GameplayTagSource source = Source("Core");
            source.SetDefinition(
                "Core",
                new[]
                {
                    new GameplayTagSourceNode(
                        "Combat",
                        children: new[]
                        {
                            new GameplayTagSourceNode(
                                "Weapon",
                                children: new[]
                                {
                                    new GameplayTagSourceNode("Fire", true),
                                    new GameplayTagSourceNode("Reload", true)
                                })
                        })
                });
            GameplayTagRegistryBuildResult result = new GameplayTagRegistryBuilder().Build(new[] { source });
            Assert.That(result.Succeeded, Is.True);
            return result.Snapshot;
        }

        private GameplayTagSource Source(string name)
        {
            GameplayTagSource source = ScriptableObject.CreateInstance<GameplayTagSource>();
            source.SetDefinition(name, System.Array.Empty<GameplayTagSourceNode>());
            sources.Add(source);
            return source;
        }
    }
}
