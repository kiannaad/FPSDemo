using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace CGame.GameplayTags.Tests
{
    public sealed class GameplayTagRuntimeTests
    {
        private readonly List<GameplayTagSource> sourcesToDestroy = new List<GameplayTagSource>();

        [TearDown]
        public void TearDown()
        {
            GameplayTagManager.Instance.Shutdown();
            foreach (GameplayTagSource source in sourcesToDestroy)
            {
                UnityEngine.Object.DestroyImmediate(source);
            }

            sourcesToDestroy.Clear();
        }

        [Test]
        public void SerializedTag_ValidatesAsciiSegmentsAndUsesCaseInsensitiveIdentity()
        {
            Assert.That(GameplayTag.TryCreateSerialized(" Combat.Fire_2 ", out GameplayTag first), Is.True);
            Assert.That(GameplayTag.TryCreateSerialized("combat.fire_2", out GameplayTag second), Is.True);
            Assert.That(first.Name, Is.EqualTo("Combat.Fire_2"));
            Assert.That(first, Is.EqualTo(second));
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));

            foreach (string invalid in new[] { null, "", ".Combat", "Combat.", "Combat..Fire", "Combat Fire", "Combat-Fire", "战斗.Fire" })
            {
                Assert.That(GameplayTag.TryCreateSerialized(invalid, out _), Is.False, invalid);
            }
        }

        [Test]
        public void Manager_RequiresInitializationAndReturnsRegisteredCanonicalName()
        {
            Assert.Throws<InvalidOperationException>(() => GameplayTagManager.Instance.RequestTag("Combat.Fire"));
            Initialize(Source("Core", Node("Combat", false, Node("Fire", true))));

            Assert.That(GameplayTagManager.Instance.TryRequestTag("combat.fire", out GameplayTag tag), Is.True);
            Assert.That(tag.Name, Is.EqualTo("Combat.Fire"));
            Assert.That(GameplayTagManager.Instance.TryRequestTag("Combat.Missing", out _), Is.False);
        }

        [Test]
        public void Registry_DistinguishesImplicitAndExplicitTags()
        {
            Initialize(Source("Core", Node("Combat", false, Node("Fire", true))));

            GameplayTag implicitTag = GameplayTagManager.Instance.RequestTag("Combat");
            GameplayTag explicitTag = GameplayTagManager.Instance.RequestTag("Combat.Fire");
            Assert.That(GameplayTagManager.Instance.IsRegistered(implicitTag), Is.True);
            Assert.That(GameplayTagManager.Instance.IsExplicitTag(implicitTag), Is.False);
            Assert.That(GameplayTagManager.Instance.IsExplicitTag(explicitTag), Is.True);
            Assert.That(GameplayTagManager.Instance.GetExplicitTags(), Is.EqualTo(new[] { explicitTag }));
        }

        [Test]
        public void Registry_MergesSourcesAndSortsSiblingsDeterministically()
        {
            GameplayTagSource second = Source("Weapons", Node("Combat", false, Node("Reload", true)));
            GameplayTagSource first = Source("Abilities", Node("Utility", true), Node("Combat", false, Node("Aim", true)));
            Initialize(second, first);

            GameplayTag combat = GameplayTagManager.Instance.RequestTag("Combat");
            Assert.That(
                GameplayTagManager.Instance.GetDirectChildren(combat).Select(tag => tag.Name),
                Is.EqualTo(new[] { "Combat.Aim", "Combat.Reload" }));
            Assert.That(
                GameplayTagManager.Instance.GetExplicitTags().Select(tag => tag.Name),
                Is.EqualTo(new[] { "Combat.Aim", "Combat.Reload", "Utility" }));
        }

        [Test]
        public void Registry_RejectsDuplicateExplicitOwnershipAcrossSources()
        {
            GameplayTagRegistryBuildResult result = new GameplayTagRegistryBuilder().Build(new[]
            {
                Source("One", Node("Combat", false, Node("Fire", true))),
                Source("Two", Node("Combat", false, Node("Fire", true)))
            });

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Errors.Select(error => error.Code), Does.Contain("DuplicateExplicitTag"));
        }

        [Test]
        public void Registry_RejectsMalformedSourceDefinitions()
        {
            GameplayTagSource duplicateName = Source("Core", Node("Utility", true));
            GameplayTagSource invalidNode = Source("Core", Node("Invalid Segment", true));
            GameplayTagRegistryBuildResult result = new GameplayTagRegistryBuilder().Build(
                new GameplayTagSource[] { duplicateName, invalidNode, null });

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Errors.Select(error => error.Code), Does.Contain("DuplicateSource"));
            Assert.That(result.Errors.Select(error => error.Code), Does.Contain("NullSource"));
            Assert.That(result.Errors.Select(error => error.Code), Does.Contain("InvalidSegment"));
        }

        [Test]
        public void Manager_FailedRebuildPreservesPreviousSnapshot()
        {
            Initialize(Source("Core", Node("Combat", false, Node("Fire", true))));
            GameplayTagSource duplicateOne = Source("One", Node("Combat", false, Node("Reload", true)));
            GameplayTagSource duplicateTwo = Source("Two", Node("Combat", false, Node("Reload", true)));

            GameplayTagRegistryBuildResult result = GameplayTagManager.Instance.Rebuild(new[] { duplicateOne, duplicateTwo });

            Assert.That(result.Succeeded, Is.False);
            Assert.That(GameplayTagManager.Instance.TryRequestTag("Combat.Fire", out _), Is.True);
            Assert.That(GameplayTagManager.Instance.TryRequestTag("Combat.Reload", out _), Is.False);
        }

        [Test]
        public void Manager_TraversesParentsAncestorsAndDescendants()
        {
            Initialize(Source("Core", Node("Combat", false, Node("Weapon", false, Node("Fire", true), Node("Reload", true)))));
            GameplayTag fire = GameplayTagManager.Instance.RequestTag("Combat.Weapon.Fire");
            GameplayTag combat = GameplayTagManager.Instance.RequestTag("Combat");

            Assert.That(GameplayTagManager.Instance.GetParent(fire).Name, Is.EqualTo("Combat.Weapon"));
            Assert.That(
                GameplayTagManager.Instance.GetAncestors(fire).Select(tag => tag.Name),
                Is.EqualTo(new[] { "Combat.Weapon", "Combat" }));
            Assert.That(
                GameplayTagManager.Instance.GetDescendants(combat).Select(tag => tag.Name),
                Is.EqualTo(new[] { "Combat.Weapon", "Combat.Weapon.Fire", "Combat.Weapon.Reload" }));
        }

        [Test]
        public void Redirect_ResolvesChainsToCanonicalExplicitTag()
        {
            GameplayTagSource source = Source("Core", Node("Combat", false, Node("Fire", true)));
            GameplayTagRegistryBuildResult result = GameplayTagManager.Instance.Initialize(
                new[] { source },
                new[]
                {
                    new GameplayTagRedirect("Old.Fire", "Legacy.Fire"),
                    new GameplayTagRedirect("Legacy.Fire", "Combat.Fire")
                });

            Assert.That(result.Succeeded, Is.True, JoinErrors(result));
            Assert.That(GameplayTagManager.Instance.TryResolveTag("old.fire", out GameplayTag resolved), Is.True);
            Assert.That(resolved.Name, Is.EqualTo("Combat.Fire"));
            Assert.That(GameplayTagManager.Instance.TryRequestTag("Old.Fire", out _), Is.False);
        }

        [Test]
        public void Redirect_RejectsCyclesAndMissingTargets()
        {
            GameplayTagSource source = Source("Core", Node("Combat", false, Node("Fire", true)));
            GameplayTagRegistryBuildResult result = new GameplayTagRegistryBuilder().Build(
                new[] { source },
                new[]
                {
                    new GameplayTagRedirect("Old.One", "Old.Two"),
                    new GameplayTagRedirect("Old.Two", "Old.One"),
                    new GameplayTagRedirect("Missing.Old", "Missing.Target")
                });

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Errors.Select(error => error.Code), Does.Contain("RedirectCycle"));
            Assert.That(result.Errors.Select(error => error.Code), Does.Contain("RedirectTargetMissing"));
        }

        [Test]
        public void Container_EnforcesExplicitOwnershipAndHierarchyMatching()
        {
            Initialize(Source("Core", Node("Combat", false, Node("Weapon", false, Node("Fire", true), Node("Reload", true)))));
            GameplayTag combat = GameplayTagManager.Instance.RequestTag("Combat");
            GameplayTag weapon = GameplayTagManager.Instance.RequestTag("Combat.Weapon");
            GameplayTag fire = GameplayTagManager.Instance.RequestTag("Combat.Weapon.Fire");
            GameplayTag reload = GameplayTagManager.Instance.RequestTag("Combat.Weapon.Reload");
            var container = new GameplayTagContainer();

            Assert.That(container.AddTag(combat), Is.False);
            Assert.That(container.AddTag(fire), Is.True);
            Assert.That(container.AddTag(fire), Is.False);
            Assert.That(container.HasTagExact(fire), Is.True);
            Assert.That(container.HasTag(weapon), Is.True);
            Assert.That(container.HasAny(new[] { reload, combat }), Is.True);
            Assert.That(container.HasAll(new[] { combat, weapon }), Is.True);
            Assert.That(container.HasAll(new[] { fire, reload }, exact: true), Is.False);
            Assert.That(container.RemoveTag(weapon), Is.False);
            Assert.That(container.RemoveTag(fire), Is.True);
        }

        [Test]
        public void Container_DeserializationRebuildsUniqueCacheAndCopyIsIndependent()
        {
            Initialize(Source("Core", Node("Combat", false, Node("Fire", true))));
            GameplayTag fire = GameplayTagManager.Instance.RequestTag("Combat.Fire");
            var original = new GameplayTagContainer();
            Assert.That(original.AddTag(fire), Is.True);

            GameplayTagContainer copy = original.Copy();
            Assert.That(copy.Count, Is.EqualTo(1));
            Assert.That(copy.RemoveTag(fire), Is.True);
            Assert.That(original.HasTagExact(fire), Is.True);

            GameplayTagContainer restored = JsonUtility.FromJson<GameplayTagContainer>(
                "{\"serializedTags\":[{\"tagName\":\"Combat.Fire\"},{\"tagName\":\"combat.fire\"},{\"tagName\":\"Ghost.Missing\"},{\"tagName\":\"\"}]}");
            restored.OnAfterDeserialize();
            Assert.That(restored.Count, Is.EqualTo(2));
            Assert.That(restored.Tags.Select(tag => tag.Name), Is.EqualTo(new[] { "Combat.Fire", "Ghost.Missing" }));
        }

        private GameplayTagSource Source(string sourceName, params GameplayTagSourceNode[] roots)
        {
            GameplayTagSource source = ScriptableObject.CreateInstance<GameplayTagSource>();
            source.SetDefinition(sourceName, roots);
            sourcesToDestroy.Add(source);
            return source;
        }

        private static GameplayTagSourceNode Node(string segmentName, bool isExplicit, params GameplayTagSourceNode[] children)
        {
            return new GameplayTagSourceNode(segmentName, isExplicit, children: children);
        }

        private static void Initialize(params GameplayTagSource[] sources)
        {
            GameplayTagRegistryBuildResult result = GameplayTagManager.Instance.Initialize(sources);
            Assert.That(result.Succeeded, Is.True, JoinErrors(result));
        }

        private static string JoinErrors(GameplayTagRegistryBuildResult result)
        {
            return string.Join(Environment.NewLine, result.Errors.Select(error => error.ToString()));
        }
    }
}
