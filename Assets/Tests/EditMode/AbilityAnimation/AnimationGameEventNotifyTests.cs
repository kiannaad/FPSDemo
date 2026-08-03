using System;
using System.Linq;
using CGame.GameplayTags;
using NUnit.Framework;
using UnityEngine;

namespace CGame.Ability.Animation.Tests
{
    public sealed class AnimationGameEventNotifyTests
    {
        private GameplayTagSource tagSource;

        [TearDown]
        public void TearDown()
        {
            GameplayTagManager.Instance.Shutdown();
            if (tagSource != null)
            {
                UnityEngine.Object.DestroyImmediate(tagSource);
                tagSource = null;
            }
        }

        [Test]
        public void SharedNotify_RoutesOnlyToTheCurrentAscOfEachCallbackPawn()
        {
            InitializeTags(Node("Event", false, Node("Weapon", false, Node("Reload", true))));
            GameplayTag eventTag = GameplayTagManager.Instance.RequestTag("Event.Weapon.Reload");
            var firstState = new global::CGame.PlayerState(new AbilitySet(), new object());
            var secondState = new global::CGame.PlayerState(new AbilitySet(), new object());
            var firstPawn = new Pawn();
            var replacementPawn = new Pawn();
            var secondPawn = new Pawn();
            var unboundPawn = new Pawn();
            firstState.SetAvatar(firstPawn);
            secondState.SetAvatar(secondPawn);
            int firstCount = 0;
            int secondCount = 0;
            AbilityGameEventPayload lastFirstPayload = null;
            AbilityGameEventRegistration firstRegistration = firstState.AbilitySystem.RegisterGameEvent(
                eventTag,
                AbilityGameEventMatchPolicy.Exact,
                payload =>
                {
                    firstCount++;
                    lastFirstPayload = payload;
                });
            AbilityGameEventRegistration secondRegistration = secondState.AbilitySystem.RegisterGameEvent(
                eventTag,
                AbilityGameEventMatchPolicy.Exact,
                payload => secondCount++);
            var notify = new AnimationGameEventNotify { EventTag = eventTag };

            try
            {
                notify.OnNotify(firstPawn);
                notify.OnNotify(secondPawn);
                Assert.That(firstCount, Is.EqualTo(1));
                Assert.That(secondCount, Is.EqualTo(1));
                Assert.That(lastFirstPayload.TargetAbilitySystem, Is.SameAs(firstState.AbilitySystem));
                Assert.That(lastFirstPayload.Avatar, Is.SameAs(firstPawn));

                firstState.SetAvatar(replacementPawn);
                notify.OnNotify(firstPawn);
                notify.OnNotify(replacementPawn);
                notify.OnNotify(unboundPawn);
                notify.OnNotify(null);

                Assert.That(firstCount, Is.EqualTo(2));
                Assert.That(secondCount, Is.EqualTo(1));
                Assert.That(lastFirstPayload.Avatar, Is.SameAs(replacementPawn));
                Assert.That(firstState.AbilitySystem.AbilityCount, Is.Zero);
                Assert.That(secondState.AbilitySystem.AbilityCount, Is.Zero);
            }
            finally
            {
                firstRegistration.Dispose();
                secondRegistration.Dispose();
                firstState.Dispose();
                secondState.Dispose();
            }
        }

        private void InitializeTags(params GameplayTagSourceNode[] roots)
        {
            tagSource = ScriptableObject.CreateInstance<GameplayTagSource>();
            tagSource.SetDefinition("AbilityAnimationTests", roots);
            GameplayTagRegistryBuildResult result = GameplayTagManager.Instance.Initialize(new[] { tagSource });
            Assert.That(result.Succeeded, Is.True, string.Join(Environment.NewLine, result.Errors.Select(error => error.ToString())));
        }

        private static GameplayTagSourceNode Node(string segmentName, bool isExplicit, params GameplayTagSourceNode[] children)
        {
            return new GameplayTagSourceNode(segmentName, isExplicit, children: children);
        }
    }
}
