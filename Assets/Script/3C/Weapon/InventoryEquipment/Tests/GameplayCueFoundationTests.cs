using System;
using System.Collections.Generic;
using CGame.Ability;
using CGame.Ability.Cues;
using CGame.Ability.Effects;
using CGame.GameplayTags;
using NUnit.Framework;
using UnityEngine;

namespace CGame.InventoryEquipment.Tests
{
    public sealed class GameplayCueFoundationTests
    {
        private GameplayCueManager manager;
        private GameplayTag fireTag;
        private GameplayTag impactTag;

        [SetUp]
        public void SetUp()
        {
            GameplayTagManager.Instance.Shutdown();
            var source = ScriptableObject.CreateInstance<GameplayTagSource>();
            source.SetDefinition(
                "GameplayCueTests",
                new[]
                {
                    new GameplayTagSourceNode("GameplayCue", false, children: new[]
                    {
                        new GameplayTagSourceNode("Weapon", false, children: new[]
                        {
                            new GameplayTagSourceNode("Fire", true),
                            new GameplayTagSourceNode("Impact", true)
                        })
                    })
                });
            Assert.That(GameplayTagManager.Instance.Initialize(new[] { source }).Succeeded, Is.True);
            fireTag = GameplayTagManager.Instance.RequestTag("GameplayCue.Weapon.Fire");
            impactTag = GameplayTagManager.Instance.RequestTag("GameplayCue.Weapon.Impact");
            manager = new GameplayCueManager();
            GameplayCueRouter.Register(manager);
        }

        [TearDown]
        public void TearDown()
        {
            GameplayCueRouter.Unregister(manager);
            GameplayTagManager.Instance.Shutdown();
        }

        [Test]
        public void Execute_ResolvesConcreteThenParent_AndDeduplicatesNotify()
        {
            var events = new List<string>();
            Action<GameplayCueEventType, GameplayCueParameters, GameplayCueHandle> duplicate = (eventType, _, _) => events.Add($"duplicate:{eventType}");
            manager.RegisterRoute(GameplayTagManager.Instance.RequestTag("GameplayCue"), duplicate);
            manager.RegisterRoute(fireTag, duplicate);
            manager.RegisterRoute(fireTag, (eventType, _, _) => events.Add($"specific:{eventType}"));

            var avatar = new object();
            var abilitySystem = new AbilitySystemComponent(avatar);
            abilitySystem.ExecuteGameplayCue(fireTag, new GameplayCueParameters(new GameplayEffectContext(new object(), new object(), new object())));

            Assert.That(events, Is.EqualTo(new[]
            {
                "duplicate:Executed",
                "specific:Executed"
            }));
        }

        [Test]
        public void DurationCue_UsesInitialSnapshotForAvatarChangeAndExactRemove()
        {
            GameplayCueParameters removedParameters = default;
            int removedCount = 0;
            manager.RegisterRoute(fireTag, (eventType, parameters, _) =>
            {
                if (eventType != GameplayCueEventType.Removed)
                {
                    return;
                }

                removedCount++;
                removedParameters = parameters;
            });
            var avatar = new object();
            var abilitySystem = new AbilitySystemComponent(avatar);
            var parameters = new GameplayCueParameters(
                new GameplayEffectContext(new object(), new object(), new object()),
                location: Vector3.zero,
                hasLocation: true);

            GameplayCueHandle handle = abilitySystem.AddGameplayCue(fireTag, parameters);
            Assert.That(handle.IsValid, Is.True);
            Assert.That(abilitySystem.SetAvatar(new object()), Is.True);

            Assert.That(removedCount, Is.EqualTo(1));
            Assert.That(removedParameters.Target, Is.SameAs(avatar));
            Assert.That(removedParameters.HasLocation, Is.True);
            Assert.That(removedParameters.Location, Is.EqualTo(Vector3.zero));
            Assert.That(abilitySystem.RemoveGameplayCue(handle), Is.False);
        }

        [Test]
        public void MissingRoute_InvalidHandleAndFailingNotify_AreIsolated()
        {
            var logs = new List<string>();
            manager.Log = logs.Add;
            manager.RegisterRoute(fireTag, (_, _, _) => throw new InvalidOperationException("expected"));
            int successfulDispatches = 0;
            manager.RegisterRoute(fireTag, (_, _, _) => successfulDispatches++);

            Assert.DoesNotThrow(() => manager.Execute(impactTag, default));
            Assert.DoesNotThrow(() => manager.Execute(fireTag, default));
            Assert.That(manager.Remove(default), Is.False);

            Assert.That(successfulDispatches, Is.EqualTo(1));
            Assert.That(logs.Count, Is.GreaterThanOrEqualTo(3));
        }
    }
}
