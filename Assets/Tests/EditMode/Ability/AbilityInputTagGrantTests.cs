using System;
using System.Collections.Generic;
using System.Linq;
using CGame.GameplayTags;
using NUnit.Framework;
using UnityEngine;

namespace CGame.Ability.Tests
{
    public sealed class AbilityInputTagGrantTests
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
        public void GiveAbilitySet_DuplicateInputTagRollsBackEverySpec()
        {
            InitializeTags(
                Node("Ability", false, Node("Test", false, Node("First", true), Node("Second", true))),
                Node("InputTag", false, Node("Weapon", false, Node("Fire", true))));
            GameplayTag first = GameplayTagManager.Instance.RequestTag("Ability.Test.First");
            GameplayTag second = GameplayTagManager.Instance.RequestTag("Ability.Test.Second");
            GameplayTag fire = GameplayTagManager.Instance.RequestTag("InputTag.Weapon.Fire");
            var component = new AbilitySystemComponent(new object());
            var set = new AbilitySet(new[]
            {
                new AbilityGrantDefinition(new TestAbilityDefinition(first), fire),
                new AbilityGrantDefinition(new TestAbilityDefinition(second), fire)
            });

            Assert.Throws<InvalidOperationException>(() => component.GiveAbilitySet(set, new object()));
            Assert.That(component.AbilityCount, Is.Zero);
        }

        [Test]
        public void KnownHandleEventAndOnceActivation_RespectTheirIndependentContracts()
        {
            InitializeTags(
                Node("Ability", false, Node("Test", false, Node("Direct", true), Node("Event", true), Node("Once", true))),
                Node("Event", false, Node("Test", false, Node("Allowed", true), Node("Rejected", true))));
            GameplayTag direct = GameplayTagManager.Instance.RequestTag("Ability.Test.Direct");
            GameplayTag eventAbility = GameplayTagManager.Instance.RequestTag("Ability.Test.Event");
            GameplayTag once = GameplayTagManager.Instance.RequestTag("Ability.Test.Once");
            GameplayTag allowed = GameplayTagManager.Instance.RequestTag("Event.Test.Allowed");
            GameplayTag rejected = GameplayTagManager.Instance.RequestTag("Event.Test.Rejected");
            var component = new AbilitySystemComponent(new object());
            AbilitySpecHandle directHandle = component.GiveAbility(new TestAbilityDefinition(direct), new object());
            AbilitySpecHandle eventHandle = component.GiveAbility(new TestAbilityDefinition(eventAbility, triggerEventTags: new[] { allowed }), new object());

            Assert.That(component.TryActivateAbility(directHandle).Succeeded, Is.True);
            Assert.That(component.TriggerAbilityFromGameplayEvent(
                eventHandle,
                rejected,
                new AbilityGameEventPayload(component, component.Avatar, new object(), default)).FailureTags,
                Does.Contain(AbilityFailureTags.InvalidEventTag));
            Assert.That(component.TriggerAbilityFromGameplayEvent(
                eventHandle,
                allowed,
                new AbilityGameEventPayload(component, component.Avatar, new object(), default)).Succeeded,
                Is.True);

            Assert.That(component.GiveAbilityAndActivateOnce(new CompletingAbilityDefinition(once), new object()).Succeeded, Is.True);
            Assert.That(component.AbilityCount, Is.EqualTo(2));
        }

        [Test]
        public void RemoveAbility_ClearsItsInputPressedState()
        {
            InitializeTags(
                Node("Ability", false, Node("Test", false, Node("Remove", true))),
                Node("InputTag", false, Node("Weapon", false, Node("Fire", true))));
            GameplayTag ability = GameplayTagManager.Instance.RequestTag("Ability.Test.Remove");
            GameplayTag fire = GameplayTagManager.Instance.RequestTag("InputTag.Weapon.Fire");
            var component = new AbilitySystemComponent(new object());
            AbilitySpecHandle handle = component.GiveAbility(new AbilityGrantDefinition(new TestAbilityDefinition(ability), fire), new object());

            component.AbilityInputTagPressed(fire);
            Assert.That(component.TryGetSpec(handle, out AbilitySpec beforeRemove) && beforeRemove.InputPressed, Is.True);

            Assert.That(component.RemoveAbility(handle), Is.True);
            Assert.That(component.TryGetSpec(handle, out _), Is.False);
        }

        [Test]
        public void ProcessAbilityInput_ActivatesOnceAndForwardsPressReleaseToRunningInstance()
        {
            InitializeTags(
                Node("Ability", false, Node("Test", false, Node("Input", true))),
                Node("InputTag", false, Node("Weapon", false, Node("Fire", true))));
            GameplayTag ability = GameplayTagManager.Instance.RequestTag("Ability.Test.Input");
            GameplayTag fire = GameplayTagManager.Instance.RequestTag("InputTag.Weapon.Fire");
            var component = new AbilitySystemComponent(new object());
            var definition = new InputAbilityDefinition(ability);
            component.GiveAbility(new AbilityGrantDefinition(definition, fire), new object());

            component.AbilityInputTagPressed(fire);
            component.ProcessAbilityInput();
            component.ProcessAbilityInput();
            component.AbilityInputTagPressed(fire);
            component.ProcessAbilityInput();
            component.AbilityInputTagReleased(fire);
            component.ProcessAbilityInput();

            Assert.That(definition.Instance.ActivateCount, Is.EqualTo(1));
            Assert.That(definition.Instance.PressedCount, Is.EqualTo(1));
            Assert.That(definition.Instance.ReleasedCount, Is.EqualTo(1));
        }

        [Test]
        public void ReplacingGrant_AdmitsTheNewInputTagBeforeRevokingTheOldReceipt()
        {
            InitializeTags(
                Node("Ability", false, Node("Test", false, Node("Old", true), Node("New", true))),
                Node("InputTag", false, Node("Weapon", false, Node("Fire", true))));
            GameplayTag oldAbility = GameplayTagManager.Instance.RequestTag("Ability.Test.Old");
            GameplayTag newAbility = GameplayTagManager.Instance.RequestTag("Ability.Test.New");
            GameplayTag fire = GameplayTagManager.Instance.RequestTag("InputTag.Weapon.Fire");
            var component = new AbilitySystemComponent(new object());
            var oldSet = new AbilitySet(new[] { new AbilityGrantDefinition(new TestAbilityDefinition(oldAbility), fire) });
            AbilityGrantReceipt oldReceipt = component.GiveAbilitySet(oldSet, new object());
            var newSet = new AbilitySet(new[] { new AbilityGrantDefinition(new TestAbilityDefinition(newAbility), fire) });

            AbilityGrantReceipt newReceipt = component.GiveAbilitySet(newSet, new object(), new[] { oldReceipt });
            Assert.That(component.AbilityCount, Is.EqualTo(2));
            Assert.That(oldReceipt.Revoke(), Is.True);
            Assert.That(component.AbilityCount, Is.EqualTo(1));
            Assert.That(component.TryActivateAbility(newReceipt.SpecHandles[0]).Succeeded, Is.True);
        }

        private void InitializeTags(params GameplayTagSourceNode[] roots)
        {
            GameplayTagSource source = ScriptableObject.CreateInstance<GameplayTagSource>();
            source.SetDefinition("AbilityInputTagGrantTests", roots);
            sourcesToDestroy.Add(source);
            GameplayTagRegistryBuildResult result = GameplayTagManager.Instance.Initialize(new[] { source });
            Assert.That(result.Succeeded, Is.True, string.Join(Environment.NewLine, result.Errors.Select(error => error.ToString())));
        }

        private static GameplayTagSourceNode Node(string segmentName, bool isExplicit, params GameplayTagSourceNode[] children)
        {
            return new GameplayTagSourceNode(segmentName, isExplicit, children: children);
        }

        private sealed class TestAbilityDefinition : AbilityDefinition
        {
            public TestAbilityDefinition(GameplayTag abilityTag, IEnumerable<GameplayTag> triggerEventTags = null)
                : base(abilityTag, triggerEventTags: triggerEventTags)
            {
            }

            protected override AbilityInstance CreateInstance()
            {
                return new TestAbilityInstance();
            }
        }

        private sealed class CompletingAbilityDefinition : AbilityDefinition
        {
            public CompletingAbilityDefinition(GameplayTag abilityTag)
                : base(abilityTag)
            {
            }

            protected override AbilityInstance CreateInstance()
            {
                return new CompletingAbilityInstance();
            }
        }

        private sealed class TestAbilityInstance : AbilityInstance
        {
        }

        private sealed class CompletingAbilityInstance : AbilityInstance
        {
            protected override void OnActivate()
            {
                EndAbility(AbilityEndReason.Completed);
            }
        }

        private sealed class InputAbilityDefinition : AbilityDefinition
        {
            public InputAbilityDefinition(GameplayTag abilityTag) : base(abilityTag) { }
            public InputAbilityInstance Instance { get; private set; }
            protected override AbilityInstance CreateInstance() => Instance = new InputAbilityInstance();
        }

        private sealed class InputAbilityInstance : AbilityInstance
        {
            public int ActivateCount { get; private set; }
            public int PressedCount { get; private set; }
            public int ReleasedCount { get; private set; }
            protected override void OnActivate() => ActivateCount++;
            protected override void OnInputPressed() => PressedCount++;
            protected override void OnInputReleased() => ReleasedCount++;
        }
    }
}
