using System;
using System.Collections.Generic;
using System.Linq;
using CGame.GameplayTags;
using NUnit.Framework;
using UnityEngine;

namespace CGame.Ability.Tests
{
    public sealed class AbilitySetGrantTests
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
        public void GiveAbilitySet_IsIdempotentPerSourceAndReceiptRevokesSpecsTagsAndActiveInstance()
        {
            InitializeTags(
                Node("Ability", false, Node("Test", false, Node("Granted", true))),
                Node("State", false, Node("Granted", true)));
            GameplayTag abilityTag = GameplayTagManager.Instance.RequestTag("Ability.Test.Granted");
            GameplayTag grantedTag = GameplayTagManager.Instance.RequestTag("State.Granted");
            var component = new AbilitySystemComponent(new object());
            var source = new object();
            var definition = new GrantedAbilityDefinition(abilityTag);
            var set = new AbilitySet(new[] { definition }, new[] { grantedTag });

            AbilityGrantReceipt first = component.GiveAbilitySet(set, source);
            AbilityGrantReceipt duplicate = component.GiveAbilitySet(set, source);

            Assert.That(duplicate, Is.SameAs(first));
            Assert.That(component.AbilityCount, Is.EqualTo(1));
            Assert.That(component.GetOwnedTagCount(grantedTag), Is.EqualTo(1));
            Assert.That(component.TryActivateAbilityByTag(abilityTag).Succeeded, Is.True);
            Assert.That(component.TryGetSpec(first.SpecHandles[0], out AbilitySpec spec), Is.True);

            Assert.That(first.Revoke(), Is.True);
            Assert.That(first.IsActive, Is.False);
            Assert.That(spec.PrimaryInstance.LastEndReason, Is.EqualTo(AbilityEndReason.SourceRemoved));
            Assert.That(component.AbilityCount, Is.Zero);
            Assert.That(component.GetOwnedTagCount(grantedTag), Is.Zero);
            Assert.That(first.Revoke(), Is.False);
        }

        private void InitializeTags(params GameplayTagSourceNode[] roots)
        {
            GameplayTagSource source = ScriptableObject.CreateInstance<GameplayTagSource>();
            source.SetDefinition("AbilitySetTests", roots);
            sourcesToDestroy.Add(source);
            GameplayTagRegistryBuildResult result = GameplayTagManager.Instance.Initialize(new[] { source });
            Assert.That(result.Succeeded, Is.True, string.Join(Environment.NewLine, result.Errors.Select(error => error.ToString())));
        }

        private static GameplayTagSourceNode Node(string segmentName, bool isExplicit, params GameplayTagSourceNode[] children)
        {
            return new GameplayTagSourceNode(segmentName, isExplicit, children: children);
        }

        private sealed class GrantedAbilityDefinition : AbilityDefinition
        {
            public GrantedAbilityDefinition(GameplayTag abilityTag)
                : base(abilityTag)
            {
            }

            protected override AbilityInstance CreateInstance()
            {
                return new GrantedAbilityInstance();
            }
        }

        private sealed class GrantedAbilityInstance : AbilityInstance
        {
        }
    }
}
