using System.Collections;
using CGame.Ability;
using CGame.Ability.Cues;
using CGame.GameplayTags;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CGame.GameplayCue.PlayModeTests
{
    public sealed class GameplayCueConfigurationPlayModeTests
    {
        [UnityTest]
        public IEnumerator GameBootstrap_ConfiguresCueSet_AndWorldTeardownReleasesDurationCue()
        {
            GameplayTagManager.Instance.Shutdown();
            GameBootstrap bootstrap = ScriptableObject.CreateInstance<GameBootstrap>();
            GameplayTagSource source = CreateTagSource();
            GameplayTag fireTag = GameplayTagManager.Instance.Initialize(new[] { source }).Snapshot == null
                ? GameplayTag.Empty
                : GameplayTagManager.Instance.RequestTag("GameplayCue.Weapon.Fire");
            GameplayTagManager.Instance.Shutdown();
            Assert.That(fireTag.IsEmpty, Is.False);

            DebugParticleCueNotifyDefinition notify = ScriptableObject.CreateInstance<DebugParticleCueNotifyDefinition>();
            var entry = new GameplayCueSetEntry();
            entry.SetDefinition(fireTag, notify);
            GameplayCueSet cueSet = ScriptableObject.CreateInstance<GameplayCueSet>();
            cueSet.SetDefinition(0, new[] { entry });
            bootstrap.ConfigureGameplayTagSources(source);
            bootstrap.ConfigureGameplayCueSets(cueSet);
            bootstrap.ConfigureResourceInitialization(false);

            World world = World.Create(bootstrap);
            world.InitializeAsync().GetAwaiter().GetResult();
            world.StartPlay();
            var abilitySystem = new AbilitySystemComponent(new object());
            GameplayCueHandle handle = abilitySystem.AddGameplayCue(
                fireTag,
                new GameplayCueParameters(new GameplayEffectContext(new object(), new object(), new object()), location: Vector3.one, hasLocation: true));
            yield return null;

            Assert.That(handle.IsValid, Is.True);
            Assert.That(GameObject.Find("GameplayCueDebugParticle"), Is.Not.Null);

            world.ShutdownAsync().GetAwaiter().GetResult();
            yield return null;
            Assert.That(GameObject.Find("GameplayCueDebugParticle"), Is.Null);
            Object.DestroyImmediate(bootstrap);
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(cueSet);
            Object.DestroyImmediate(notify);
        }

        private static GameplayTagSource CreateTagSource()
        {
            GameplayTagSource source = ScriptableObject.CreateInstance<GameplayTagSource>();
            source.SetDefinition("GameplayCuePlayMode", new[]
            {
                new GameplayTagSourceNode("GameplayCue", false, children: new[]
                {
                    new GameplayTagSourceNode("Weapon", false, children: new[]
                    {
                        new GameplayTagSourceNode("Fire", true)
                    })
                })
            });
            return source;
        }
    }
}
