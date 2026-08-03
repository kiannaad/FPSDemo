using System;
using System.Collections.Generic;
using System.Linq;
using CGame.GameplayTags;
using NUnit.Framework;
using UnityEngine;

namespace CGame.Ability.Tests
{
    public sealed class AbilityLocalActivationTests
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
        public void ExactTagActivation_ActivatesCommitsOnceAndCleansUpOnCancel()
        {
            InitializeTags(
                Node("Ability", false, Node("Test", false, Node("Trace", true))),
                Node("State", false, Node("Ability", false, Node("Active", true))));
            GameplayTag abilityTag = GameplayTagManager.Instance.RequestTag("Ability.Test.Trace");
            GameplayTag activeTag = GameplayTagManager.Instance.RequestTag("State.Ability.Active");
            var component = new AbilitySystemComponent(new object());
            var definition = new TraceAbilityDefinition(abilityTag, activeTag);

            AbilitySpecHandle specHandle = component.GiveAbility(definition, new object());
            AbilityActivationResult activation = component.TryActivateAbilityByTag(abilityTag);

            Assert.That(activation.Succeeded, Is.True);
            Assert.That(component.TryGetSpec(specHandle, out AbilitySpec spec), Is.True);
            var instance = (TraceAbilityInstance)spec.PrimaryInstance;
            Assert.That(instance.State, Is.EqualTo(AbilityInstanceState.Active));
            Assert.That(instance.StateObservedDuringActivate, Is.EqualTo(AbilityInstanceState.Active));
            Assert.That(spec.ActiveInstanceCount, Is.EqualTo(1));
            Assert.That(component.HasOwnedTagExact(activeTag), Is.True);
            Assert.That(instance.TryCommit(), Is.True);
            Assert.That(instance.TryCommit(), Is.False);
            Assert.That(instance.CommitCount, Is.EqualTo(1));

            Assert.That(component.CancelAbility(specHandle, AbilityEndReason.Cancelled), Is.True);
            Assert.That(instance.State, Is.EqualTo(AbilityInstanceState.Inactive));
            Assert.That(instance.LastEndReason, Is.EqualTo(AbilityEndReason.Cancelled));
            Assert.That(instance.StateObservedDuringEnd, Is.EqualTo(AbilityInstanceState.Ending));
            Assert.That(spec.ActiveInstanceCount, Is.Zero);
            Assert.That(component.HasOwnedTagExact(activeTag), Is.False);
            Assert.That(component.CancelAbility(specHandle, AbilityEndReason.Cancelled), Is.False);
            Assert.That(instance.EndAbility(AbilityEndReason.Completed), Is.False);
        }

        [Test]
        public void ExactTagActivation_RejectsZeroAndAmbiguousMatchesWithStructuredFailures()
        {
            InitializeTags(Node("Ability", false, Node("Test", false, Node("Trace", true), Node("Other", true))));
            GameplayTag traceTag = GameplayTagManager.Instance.RequestTag("Ability.Test.Trace");
            GameplayTag otherTag = GameplayTagManager.Instance.RequestTag("Ability.Test.Other");
            var component = new AbilitySystemComponent(new object());

            AbilityActivationResult missing = component.TryActivateAbilityByTag(otherTag);

            Assert.That(missing.Succeeded, Is.False);
            Assert.That(missing.FailureTags, Does.Contain(AbilityFailureTags.NotFound));

            component.GiveAbility(new TraceAbilityDefinition(traceTag), new object());
            component.GiveAbility(new TraceAbilityDefinition(traceTag), new object());
            AbilityActivationResult ambiguous = component.TryActivateAbilityByTag(traceTag);

            Assert.That(ambiguous.Succeeded, Is.False);
            Assert.That(ambiguous.FailureTags, Does.Contain(AbilityFailureTags.Ambiguous));
        }

        [Test]
        public void CanActivate_ReportsRequiredAndBlockedOwnedTagFailures()
        {
            InitializeTags(
                Node("Ability", false, Node("Test", false, Node("Trace", true))),
                Node("State", false, Node("Ready", true), Node("Blocked", true)));
            GameplayTag abilityTag = GameplayTagManager.Instance.RequestTag("Ability.Test.Trace");
            GameplayTag readyTag = GameplayTagManager.Instance.RequestTag("State.Ready");
            GameplayTag blockedTag = GameplayTagManager.Instance.RequestTag("State.Blocked");
            var component = new AbilitySystemComponent(new object());
            component.GiveAbility(
                new TraceAbilityDefinition(abilityTag, requiredTags: new[] { readyTag }, blockedTags: new[] { blockedTag }),
                new object());

            AbilityActivationResult missingRequired = component.TryActivateAbilityByTag(abilityTag);
            Assert.That(missingRequired.FailureTags, Does.Contain(AbilityFailureTags.RequiredTagMissing));

            component.AddOwnedTag(readyTag);
            GameplayTagGrantHandle blockedGrant = component.AddOwnedTag(blockedTag);
            AbilityActivationResult blocked = component.TryActivateAbilityByTag(abilityTag);
            Assert.That(blocked.FailureTags, Does.Contain(AbilityFailureTags.BlockedTagPresent));

            component.RemoveOwnedTag(blockedGrant);
            Assert.That(component.TryActivateAbilityByTag(abilityTag).Succeeded, Is.True);
        }

        [Test]
        public void ActivationGate_RejectsInvalidContextNonLocalExecutionAndDuplicateActivation()
        {
            InitializeTags(Node("Ability", false, Node("Test", false, Node("Trace", true))));
            GameplayTag abilityTag = GameplayTagManager.Instance.RequestTag("Ability.Test.Trace");
            var gate = new TraceExecutionGate { CanExecute = false };

            var missingAvatar = new AbilitySystemComponent(null);
            missingAvatar.GiveAbility(new TraceAbilityDefinition(abilityTag), new object());
            Assert.That(
                missingAvatar.TryActivateAbilityByTag(abilityTag).FailureTags,
                Does.Contain(AbilityFailureTags.InvalidAvatar));

            var missingSource = new AbilitySystemComponent(new object());
            missingSource.GiveAbility(new TraceAbilityDefinition(abilityTag), null);
            Assert.That(
                missingSource.TryActivateAbilityByTag(abilityTag).FailureTags,
                Does.Contain(AbilityFailureTags.InvalidSource));

            var component = new AbilitySystemComponent(new object(), gate);
            component.GiveAbility(new TraceAbilityDefinition(abilityTag), new object());
            Assert.That(
                component.TryActivateAbilityByTag(abilityTag).FailureTags,
                Does.Contain(AbilityFailureTags.NotLocal));

            gate.CanExecute = true;
            Assert.That(component.TryActivateAbilityByTag(abilityTag).Succeeded, Is.True);
            Assert.That(
                component.TryActivateAbilityByTag(abilityTag).FailureTags,
                Does.Contain(AbilityFailureTags.AlreadyActive));
        }

        [Test]
        public void ExactTagActivation_RejectsEmptyAndNonLeafAbilityTags()
        {
            InitializeTags(Node("Ability", false, Node("Test", true, Node("Trace", true))));
            GameplayTag parentTag = GameplayTagManager.Instance.RequestTag("Ability.Test");
            var component = new AbilitySystemComponent(new object());
            component.GiveAbility(new TraceAbilityDefinition(parentTag), new object());

            Assert.That(
                component.TryActivateAbilityByTag(GameplayTag.Empty).FailureTags,
                Does.Contain(AbilityFailureTags.InvalidAbilityTag));
            Assert.That(
                component.TryActivateAbilityByTag(parentTag).FailureTags,
                Does.Contain(AbilityFailureTags.InvalidAbilityTag));
        }

        [Test]
        public void OwnedTagGrants_CountIndependentSourcesAndRemoveOnlyAtZero()
        {
            InitializeTags(Node("State", false, Node("Shared", true)));
            GameplayTag sharedTag = GameplayTagManager.Instance.RequestTag("State.Shared");
            var component = new AbilitySystemComponent(new object());

            GameplayTagGrantHandle first = component.AddOwnedTag(sharedTag);
            GameplayTagGrantHandle second = component.AddOwnedTag(sharedTag);

            Assert.That(component.GetOwnedTagCount(sharedTag), Is.EqualTo(2));
            Assert.That(component.RemoveOwnedTag(first), Is.True);
            Assert.That(component.GetOwnedTagCount(sharedTag), Is.EqualTo(1));
            Assert.That(component.HasOwnedTagExact(sharedTag), Is.True);
            Assert.That(component.RemoveOwnedTag(first), Is.False);
            Assert.That(component.GetOwnedTagCount(sharedTag), Is.EqualTo(1));
            Assert.That(component.RemoveOwnedTag(second), Is.True);
            Assert.That(component.GetOwnedTagCount(sharedTag), Is.Zero);
            Assert.That(component.HasOwnedTagExact(sharedTag), Is.False);
        }

        private void InitializeTags(params GameplayTagSourceNode[] roots)
        {
            GameplayTagSource source = ScriptableObject.CreateInstance<GameplayTagSource>();
            source.SetDefinition("AbilityTests", roots);
            sourcesToDestroy.Add(source);
            GameplayTagRegistryBuildResult result = GameplayTagManager.Instance.Initialize(new[] { source });
            Assert.That(result.Succeeded, Is.True, string.Join(Environment.NewLine, result.Errors.Select(error => error.ToString())));
        }

        private static GameplayTagSourceNode Node(string segmentName, bool isExplicit, params GameplayTagSourceNode[] children)
        {
            return new GameplayTagSourceNode(segmentName, isExplicit, children: children);
        }

        private sealed class TraceAbilityDefinition : AbilityDefinition
        {
            public TraceAbilityDefinition(
                GameplayTag abilityTag,
                GameplayTag activeTag = default,
                IEnumerable<GameplayTag> requiredTags = null,
                IEnumerable<GameplayTag> blockedTags = null)
                : base(
                    abilityTag,
                    activationOwnedTags: activeTag.IsEmpty ? null : new[] { activeTag },
                    requiredOwnedTags: requiredTags,
                    blockedOwnedTags: blockedTags)
            {
            }

            protected override AbilityInstance CreateInstance()
            {
                return new TraceAbilityInstance();
            }
        }

        private sealed class TraceAbilityInstance : AbilityInstance
        {
            public int CommitCount { get; private set; }
            public AbilityInstanceState StateObservedDuringActivate { get; private set; }
            public AbilityInstanceState StateObservedDuringEnd { get; private set; }

            protected override void OnActivate()
            {
                StateObservedDuringActivate = State;
            }

            protected override void OnCommit()
            {
                CommitCount++;
            }

            protected override void OnEnd(AbilityEndReason reason)
            {
                StateObservedDuringEnd = State;
            }
        }

        private sealed class TraceExecutionGate : IAbilityExecutionGate
        {
            public bool CanExecute { get; set; }

            public bool CanExecuteLocally(AbilitySystemComponent abilitySystem)
            {
                return CanExecute;
            }
        }
    }
}
