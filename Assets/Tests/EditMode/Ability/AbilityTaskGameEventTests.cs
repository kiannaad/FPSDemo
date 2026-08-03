using System;
using System.Collections.Generic;
using System.Linq;
using CGame.GameplayTags;
using NUnit.Framework;
using UnityEngine;

namespace CGame.Ability.Tests
{
    public sealed class AbilityTaskGameEventTests
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
        public void WaitGameEvent_OneShotCompletesBeforeCallbackCommitsAndEndsAbility()
        {
            InitializeTags(out GameplayTag abilityTag, out GameplayTag eventTag, out _);
            object avatar = new object();
            object source = new object();
            var component = new AbilitySystemComponent(avatar);
            AbilitySpecHandle specHandle = component.GiveAbility(
                new WaitingAbilityDefinition(abilityTag, eventTag, completeOnEvent: true),
                source);

            AbilityActivationResult activation = component.TryActivateAbilityByTag(abilityTag);
            WaitingAbilityInstance instance = GetInstance(component, specHandle);
            WaitGameEventTask task = instance.WaitTask;
            AbilityGameEventDispatchResult dispatch = component.HandleGameEvent(
                eventTag,
                new AbilityGameEventPayload(component, avatar, source, activation.ActivationHandle));

            Assert.That(dispatch.InvokedCount, Is.EqualTo(1));
            Assert.That(instance.EventCount, Is.EqualTo(1));
            Assert.That(instance.CommitCount, Is.EqualTo(1));
            Assert.That(instance.State, Is.EqualTo(AbilityInstanceState.Inactive));
            Assert.That(instance.LastEndReason, Is.EqualTo(AbilityEndReason.Completed));
            Assert.That(task.State, Is.EqualTo(AbilityTaskState.Completed));
            Assert.That(task.IsListening, Is.False);
            Assert.That(instance.ActiveTaskCount, Is.Zero);
        }

        [Test]
        public void AbilityEnd_CancelsWaitAndOldActivationCannotAdvanceTheNextActivation()
        {
            InitializeTags(out GameplayTag abilityTag, out GameplayTag eventTag, out _);
            object avatar = new object();
            object source = new object();
            var component = new AbilitySystemComponent(avatar);
            AbilitySpecHandle specHandle = component.GiveAbility(
                new WaitingAbilityDefinition(abilityTag, eventTag),
                source);

            AbilityActivationResult firstActivation = component.TryActivateAbilityByTag(abilityTag);
            WaitingAbilityInstance instance = GetInstance(component, specHandle);
            WaitGameEventTask firstTask = instance.WaitTask;
            Assert.That(component.CancelAbility(specHandle, AbilityEndReason.Cancelled), Is.True);

            Assert.That(firstTask.State, Is.EqualTo(AbilityTaskState.Cancelled));
            Assert.That(firstTask.IsListening, Is.False);
            Assert.That(instance.ActiveTaskCount, Is.Zero);

            AbilityActivationResult secondActivation = component.TryActivateAbilityByTag(abilityTag);
            WaitGameEventTask secondTask = instance.WaitTask;
            component.HandleGameEvent(
                eventTag,
                new AbilityGameEventPayload(component, avatar, source, firstActivation.ActivationHandle));

            Assert.That(instance.EventCount, Is.Zero);
            Assert.That(secondTask.State, Is.EqualTo(AbilityTaskState.Active));

            component.HandleGameEvent(
                eventTag,
                new AbilityGameEventPayload(component, avatar, source, secondActivation.ActivationHandle));

            Assert.That(instance.EventCount, Is.EqualTo(1));
            Assert.That(secondTask.State, Is.EqualTo(AbilityTaskState.Completed));
        }

        [Test]
        public void WaitGameEvent_IgnoresWrongSourceAndAvatarChangeCancelsRegistration()
        {
            InitializeTags(out GameplayTag abilityTag, out GameplayTag eventTag, out GameplayTag childEventTag);
            object avatar = new object();
            object source = new object();
            var component = new AbilitySystemComponent(avatar);
            AbilitySpecHandle specHandle = component.GiveAbility(
                new WaitingAbilityDefinition(abilityTag, eventTag),
                source);
            AbilityActivationResult activation = component.TryActivateAbilityByTag(abilityTag);
            WaitingAbilityInstance instance = GetInstance(component, specHandle);
            WaitGameEventTask task = instance.WaitTask;

            component.HandleGameEvent(
                childEventTag,
                new AbilityGameEventPayload(component, avatar, source, activation.ActivationHandle));
            component.HandleGameEvent(
                eventTag,
                new AbilityGameEventPayload(component, avatar, new object(), activation.ActivationHandle));
            Assert.That(instance.EventCount, Is.Zero);
            Assert.That(task.State, Is.EqualTo(AbilityTaskState.Active));

            component.SetAvatar(new object());

            Assert.That(instance.LastEndReason, Is.EqualTo(AbilityEndReason.AvatarChanged));
            Assert.That(task.State, Is.EqualTo(AbilityTaskState.Cancelled));
            Assert.That(task.IsListening, Is.False);
        }

        [Test]
        public void AbilityTask_CreatedTaskCanHaveOnlyOneActiveOwner()
        {
            InitializeTags(out GameplayTag abilityTag, out GameplayTag eventTag, out _);
            object avatar = new object();
            var component = new AbilitySystemComponent(avatar);
            var sharedTask = new WaitGameEventTask(
                eventTag,
                AbilityGameEventMatchPolicy.Exact,
                onlyTriggerOnce: false,
                payload => { });
            Assert.That(sharedTask.State, Is.EqualTo(AbilityTaskState.Created));
            Assert.That(sharedTask.Owner, Is.Null);

            var secondComponent = new AbilitySystemComponent(new object());
            AbilitySpecHandle firstHandle = component.GiveAbility(
                new ExternalTaskAbilityDefinition(abilityTag, sharedTask),
                new object());
            AbilitySpecHandle secondHandle = secondComponent.GiveAbility(
                new ExternalTaskAbilityDefinition(abilityTag, sharedTask),
                new object());
            Assert.That(component.TryGetSpec(firstHandle, out AbilitySpec firstSpec), Is.True);
            Assert.That(secondComponent.TryGetSpec(secondHandle, out AbilitySpec secondSpec), Is.True);
            var first = (ExternalTaskAbilityInstance)firstSpec.PrimaryInstance;
            var second = (ExternalTaskAbilityInstance)secondSpec.PrimaryInstance;
            Assert.That(component.TryActivateAbilityByTag(abilityTag).Succeeded, Is.True);
            Assert.That(secondComponent.TryActivateAbilityByTag(abilityTag).Succeeded, Is.True);

            first.StartExternalTask();
            Assert.That(sharedTask.State, Is.EqualTo(AbilityTaskState.Active));
            Assert.That(sharedTask.Owner, Is.SameAs(first));
            Assert.Throws<InvalidOperationException>(() => second.StartExternalTask());
            Assert.That(second.ActiveTaskCount, Is.Zero);

            first.EndAbility(AbilityEndReason.Cancelled);
            second.EndAbility(AbilityEndReason.Cancelled);
            Assert.That(sharedTask.State, Is.EqualTo(AbilityTaskState.Cancelled));
        }

        [Test]
        public void AbilityFailedAndAscDispose_CancelEveryActiveWait()
        {
            InitializeTags(out GameplayTag abilityTag, out GameplayTag eventTag, out _);
            object avatar = new object();
            object source = new object();
            var component = new AbilitySystemComponent(avatar);
            AbilitySpecHandle specHandle = component.GiveAbility(
                new WaitingAbilityDefinition(abilityTag, eventTag, onlyTriggerOnce: false),
                source);

            component.TryActivateAbilityByTag(abilityTag);
            WaitingAbilityInstance instance = GetInstance(component, specHandle);
            WaitGameEventTask failedTask = instance.WaitTask;
            component.CancelAbility(specHandle, AbilityEndReason.Failed);

            Assert.That(instance.LastEndReason, Is.EqualTo(AbilityEndReason.Failed));
            Assert.That(failedTask.State, Is.EqualTo(AbilityTaskState.Cancelled));
            Assert.That(failedTask.IsListening, Is.False);

            component.TryActivateAbilityByTag(abilityTag);
            WaitGameEventTask disposeTask = instance.WaitTask;
            component.Dispose();

            Assert.That(component.IsDisposed, Is.True);
            Assert.That(disposeTask.State, Is.EqualTo(AbilityTaskState.Cancelled));
            Assert.That(disposeTask.IsListening, Is.False);
            Assert.That(instance.ActiveTaskCount, Is.Zero);
        }

        [Test]
        public void WaitGameEvent_ContinuousIncludeChildrenGuardsReentrantDispatchUntilOwnerEnds()
        {
            InitializeTags(out GameplayTag abilityTag, out GameplayTag eventParent, out GameplayTag eventChild);
            object avatar = new object();
            object source = new object();
            var component = new AbilitySystemComponent(avatar);
            AbilitySpecHandle specHandle = component.GiveAbility(
                new WaitingAbilityDefinition(
                    abilityTag,
                    eventParent,
                    onlyTriggerOnce: false,
                    matchPolicy: AbilityGameEventMatchPolicy.IncludeChildTags,
                    sendReentrantEvent: true),
                source);
            AbilityActivationResult activation = component.TryActivateAbilityByTag(abilityTag);
            WaitingAbilityInstance instance = GetInstance(component, specHandle);
            var payload = new AbilityGameEventPayload(component, avatar, source, activation.ActivationHandle);

            component.HandleGameEvent(eventChild, payload);
            Assert.That(instance.EventCount, Is.EqualTo(1));
            Assert.That(instance.WaitTask.State, Is.EqualTo(AbilityTaskState.Active));

            component.HandleGameEvent(eventChild, payload);
            Assert.That(instance.EventCount, Is.EqualTo(2));

            component.RemoveAbility(specHandle, AbilityEndReason.SourceRemoved);
            Assert.That(instance.WaitTask.State, Is.EqualTo(AbilityTaskState.Cancelled));
            Assert.That(instance.WaitTask.IsListening, Is.False);
        }

        private void InitializeTags(
            out GameplayTag abilityTag,
            out GameplayTag eventParent,
            out GameplayTag eventChild)
        {
            GameplayTagSource source = ScriptableObject.CreateInstance<GameplayTagSource>();
            source.SetDefinition(
                "AbilityTaskTests",
                new[]
                {
                    Node("Ability", false, Node("Test", false, Node("Wait", true))),
                    Node("Event", false, Node("Weapon", true, Node("Reload", true)))
                });
            sourcesToDestroy.Add(source);
            GameplayTagRegistryBuildResult result = GameplayTagManager.Instance.Initialize(new[] { source });
            Assert.That(result.Succeeded, Is.True, string.Join(Environment.NewLine, result.Errors.Select(error => error.ToString())));
            abilityTag = GameplayTagManager.Instance.RequestTag("Ability.Test.Wait");
            eventParent = GameplayTagManager.Instance.RequestTag("Event.Weapon");
            eventChild = GameplayTagManager.Instance.RequestTag("Event.Weapon.Reload");
        }

        private static GameplayTagSourceNode Node(string segmentName, bool isExplicit, params GameplayTagSourceNode[] children)
        {
            return new GameplayTagSourceNode(segmentName, isExplicit, children: children);
        }

        private static WaitingAbilityInstance GetInstance(
            AbilitySystemComponent component,
            AbilitySpecHandle specHandle)
        {
            Assert.That(component.TryGetSpec(specHandle, out AbilitySpec spec), Is.True);
            return (WaitingAbilityInstance)spec.PrimaryInstance;
        }

        private sealed class WaitingAbilityDefinition : AbilityDefinition
        {
            private readonly GameplayTag eventTag;
            private readonly bool onlyTriggerOnce;
            private readonly AbilityGameEventMatchPolicy matchPolicy;
            private readonly bool completeOnEvent;
            private readonly bool sendReentrantEvent;

            public WaitingAbilityDefinition(
                GameplayTag abilityTag,
                GameplayTag eventTag,
                bool onlyTriggerOnce = true,
                AbilityGameEventMatchPolicy matchPolicy = AbilityGameEventMatchPolicy.Exact,
                bool completeOnEvent = false,
                bool sendReentrantEvent = false)
                : base(abilityTag)
            {
                this.eventTag = eventTag;
                this.onlyTriggerOnce = onlyTriggerOnce;
                this.matchPolicy = matchPolicy;
                this.completeOnEvent = completeOnEvent;
                this.sendReentrantEvent = sendReentrantEvent;
            }

            protected override AbilityInstance CreateInstance()
            {
                return new WaitingAbilityInstance(
                    eventTag,
                    onlyTriggerOnce,
                    matchPolicy,
                    completeOnEvent,
                    sendReentrantEvent);
            }
        }

        private sealed class WaitingAbilityInstance : AbilityInstance
        {
            private readonly GameplayTag eventTag;
            private readonly bool onlyTriggerOnce;
            private readonly AbilityGameEventMatchPolicy matchPolicy;
            private readonly bool completeOnEvent;
            private readonly bool sendReentrantEvent;

            public WaitingAbilityInstance(
                GameplayTag eventTag,
                bool onlyTriggerOnce,
                AbilityGameEventMatchPolicy matchPolicy,
                bool completeOnEvent,
                bool sendReentrantEvent)
            {
                this.eventTag = eventTag;
                this.onlyTriggerOnce = onlyTriggerOnce;
                this.matchPolicy = matchPolicy;
                this.completeOnEvent = completeOnEvent;
                this.sendReentrantEvent = sendReentrantEvent;
            }

            public WaitGameEventTask WaitTask { get; private set; }
            public int EventCount { get; private set; }
            public int CommitCount { get; private set; }

            protected override void OnActivate()
            {
                WaitTask = StartTask(new WaitGameEventTask(eventTag, matchPolicy, onlyTriggerOnce, OnEvent));
            }

            protected override void OnCommit()
            {
                CommitCount++;
            }

            private void OnEvent(AbilityGameEventPayload payload)
            {
                EventCount++;
                if (sendReentrantEvent && EventCount == 1)
                {
                    payload.TargetAbilitySystem.HandleGameEvent(eventTag, payload);
                }

                if (completeOnEvent)
                {
                    TryCommit();
                    EndAbility(AbilityEndReason.Completed);
                }
            }
        }

        private sealed class ExternalTaskAbilityDefinition : AbilityDefinition
        {
            private readonly WaitGameEventTask task;

            public ExternalTaskAbilityDefinition(GameplayTag abilityTag, WaitGameEventTask task)
                : base(abilityTag)
            {
                this.task = task;
            }

            protected override AbilityInstance CreateInstance()
            {
                return new ExternalTaskAbilityInstance(task);
            }
        }

        private sealed class ExternalTaskAbilityInstance : AbilityInstance
        {
            private readonly WaitGameEventTask task;

            public ExternalTaskAbilityInstance(WaitGameEventTask task)
            {
                this.task = task;
            }

            public void StartExternalTask()
            {
                StartTask(task);
            }

        }
    }
}
