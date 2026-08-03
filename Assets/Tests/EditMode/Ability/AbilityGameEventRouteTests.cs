using System;
using System.Collections.Generic;
using System.Linq;
using CGame.GameplayTags;
using NUnit.Framework;
using UnityEngine;

namespace CGame.Ability.Tests
{
    public sealed class AbilityGameEventRouteTests
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
        public void HandleGameEvent_MatchesExactAndChildrenInRegistrationOrderAndIsolatesExceptions()
        {
            InitializeTags(Node("Event", false, Node("Weapon", true, Node("Reload", true))));
            GameplayTag weaponEvent = GameplayTagManager.Instance.RequestTag("Event.Weapon");
            GameplayTag reloadEvent = GameplayTagManager.Instance.RequestTag("Event.Weapon.Reload");
            var avatar = new object();
            var component = new AbilitySystemComponent(avatar);
            var trace = new List<string>();
            component.RegisterGameEvent(
                weaponEvent,
                AbilityGameEventMatchPolicy.IncludeChildTags,
                payload => trace.Add("parent"));
            component.RegisterGameEvent(
                reloadEvent,
                AbilityGameEventMatchPolicy.Exact,
                payload =>
                {
                    trace.Add("throw");
                    throw new InvalidOperationException("listener failure");
                });
            component.RegisterGameEvent(
                reloadEvent,
                AbilityGameEventMatchPolicy.Exact,
                payload => trace.Add("last"));
            component.RegisterGameEvent(
                weaponEvent,
                AbilityGameEventMatchPolicy.Exact,
                payload => trace.Add("wrong-exact"));
            var payload = new AbilityGameEventPayload(component, avatar, null, default);

            AbilityGameEventDispatchResult result = component.HandleGameEvent(reloadEvent, payload);

            Assert.That(trace, Is.EqualTo(new[] { "parent", "throw", "last" }));
            Assert.That(result.MatchedCount, Is.EqualTo(3));
            Assert.That(result.InvokedCount, Is.EqualTo(3));
            Assert.That(result.Exceptions.Count, Is.EqualTo(1));
            Assert.That(component.AbilityCount, Is.Zero, "GameEvent must not activate an inactive Ability.");
        }

        [Test]
        public void HandleGameEvent_ModificationsAreDeterministicAndOldAvatarOrWrongTargetIsIgnored()
        {
            InitializeTags(Node("Event", false, Node("Weapon", false, Node("Reload", true))));
            GameplayTag reloadEvent = GameplayTagManager.Instance.RequestTag("Event.Weapon.Reload");
            var oldAvatar = new object();
            var newAvatar = new object();
            var component = new AbilitySystemComponent(oldAvatar);
            var otherComponent = new AbilitySystemComponent(new object());
            var trace = new List<string>();
            AbilityGameEventRegistration second = null;
            AbilityGameEventRegistration late = null;
            AbilityGameEventRegistration first = component.RegisterGameEvent(
                reloadEvent,
                AbilityGameEventMatchPolicy.Exact,
                payload =>
                {
                    trace.Add("first");
                    second?.Dispose();
                    if (late == null)
                    {
                        late = component.RegisterGameEvent(
                            reloadEvent,
                            AbilityGameEventMatchPolicy.Exact,
                            value => trace.Add("late"));
                    }
                });
            second = component.RegisterGameEvent(
                reloadEvent,
                AbilityGameEventMatchPolicy.Exact,
                payload => trace.Add("second"));

            AbilityGameEventDispatchResult wrongTarget = component.HandleGameEvent(
                reloadEvent,
                new AbilityGameEventPayload(otherComponent, oldAvatar, null, default));
            Assert.That(wrongTarget.InvokedCount, Is.Zero);
            Assert.That(trace, Is.Empty);

            AbilityGameEventDispatchResult firstDispatch = component.HandleGameEvent(
                reloadEvent,
                new AbilityGameEventPayload(component, oldAvatar, null, default));
            Assert.That(trace, Is.EqualTo(new[] { "first" }));
            Assert.That(firstDispatch.InvokedCount, Is.EqualTo(1));
            Assert.That(second.IsActive, Is.False);
            Assert.That(late.IsActive, Is.True);

            component.SetAvatar(newAvatar);
            trace.Clear();
            AbilityGameEventDispatchResult staleAvatar = component.HandleGameEvent(
                reloadEvent,
                new AbilityGameEventPayload(component, oldAvatar, null, default));
            Assert.That(staleAvatar.InvokedCount, Is.Zero);
            Assert.That(trace, Is.Empty);

            component.HandleGameEvent(
                reloadEvent,
                new AbilityGameEventPayload(component, newAvatar, null, default));
            Assert.That(trace, Is.EqualTo(new[] { "first", "late" }));

            first.Dispose();
            first.Dispose();
            late.Dispose();
            trace.Clear();
            Assert.That(
                component.HandleGameEvent(
                    reloadEvent,
                    new AbilityGameEventPayload(component, newAvatar, null, default)).InvokedCount,
                Is.Zero);
            Assert.That(trace, Is.Empty);
        }

        private void InitializeTags(params GameplayTagSourceNode[] roots)
        {
            GameplayTagSource source = ScriptableObject.CreateInstance<GameplayTagSource>();
            source.SetDefinition("AbilityEventTests", roots);
            sourcesToDestroy.Add(source);
            GameplayTagRegistryBuildResult result = GameplayTagManager.Instance.Initialize(new[] { source });
            Assert.That(result.Succeeded, Is.True, string.Join(Environment.NewLine, result.Errors.Select(error => error.ToString())));
        }

        private static GameplayTagSourceNode Node(string segmentName, bool isExplicit, params GameplayTagSourceNode[] children)
        {
            return new GameplayTagSourceNode(segmentName, isExplicit, children: children);
        }
    }
}
