using System;
using System.Collections;
using System.Linq;
using CGame.Ability;
using CGame.GameplayTags;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CGame.PlayerStatePlayModeTests
{
    public sealed class PlayerStateSpawnReleasePlayModeTests
    {
        [UnityTest]
        public IEnumerator SpawnReplacementAndRelease_PreservesStateUntilManagerShutdown()
        {
            GameplayTagSource tagSource = ScriptableObject.CreateInstance<GameplayTagSource>();
            tagSource.SetDefinition(
                "PlayerStatePlayModeTests",
                new[] { Node("Ability", false, Node("Test", false, Node("Spawned", true))) });
            GameplayTagRegistryBuildResult tagResult = GameplayTagManager.Instance.Initialize(new[] { tagSource });
            Assert.That(tagResult.Succeeded, Is.True, string.Join(Environment.NewLine, tagResult.Errors.Select(error => error.ToString())));
            GameplayTag abilityTag = GameplayTagManager.Instance.RequestTag("Ability.Test.Spawned");
            var manager = new PlayerStateManager();
            global::CGame.PlayerState state = manager.Initialize(
                new AbilitySet(new[] { new SpawnedAbilityDefinition(abilityTag) }),
                new object());
            var firstPawn = new Pawn();
            var secondPawn = new Pawn();
            var firstRoot = new GameObject("FirstPlayerRuntime");
            var secondRoot = new GameObject("SecondPlayerRuntime");

            try
            {
                firstRoot.AddComponent<PlayerStateAvatarBindingHost>().Initialize(manager, firstPawn);
                Assert.That(state.AbilitySystem.TryActivateAbilityByTag(abilityTag).Succeeded, Is.True);
                AbilitySpecHandle specHandle = state.BaseGrantReceipt.SpecHandles[0];
                Assert.That(state.AbilitySystem.TryGetSpec(specHandle, out AbilitySpec spec), Is.True);

                secondRoot.AddComponent<PlayerStateAvatarBindingHost>().Initialize(manager, secondPawn);
                Assert.That(spec.PrimaryInstance.LastEndReason, Is.EqualTo(AbilityEndReason.AvatarChanged));

                UnityEngine.Object.Destroy(firstRoot);
                yield return null;
                Assert.That(state.Avatar, Is.SameAs(secondPawn));
                Assert.That(secondPawn.AbilitySystem, Is.SameAs(state.AbilitySystem));

                UnityEngine.Object.Destroy(secondRoot);
                yield return null;
                Assert.That(state.Avatar, Is.Null);
                Assert.That(state.AbilitySystem.AbilityCount, Is.EqualTo(1));

                manager.Shutdown();
                Assert.That(state.IsDisposed, Is.True);
                Assert.That(state.AbilitySystem.AbilityCount, Is.Zero);
            }
            finally
            {
                if (firstRoot != null)
                {
                    UnityEngine.Object.Destroy(firstRoot);
                }

                if (secondRoot != null)
                {
                    UnityEngine.Object.Destroy(secondRoot);
                }

                manager.Shutdown();
                GameplayTagManager.Instance.Shutdown();
                UnityEngine.Object.Destroy(tagSource);
            }
        }

        private static GameplayTagSourceNode Node(string segmentName, bool isExplicit, params GameplayTagSourceNode[] children)
        {
            return new GameplayTagSourceNode(segmentName, isExplicit, children: children);
        }

        private sealed class SpawnedAbilityDefinition : AbilityDefinition
        {
            public SpawnedAbilityDefinition(GameplayTag abilityTag)
                : base(abilityTag)
            {
            }

            protected override AbilityInstance CreateInstance()
            {
                return new SpawnedAbilityInstance();
            }
        }

        private sealed class SpawnedAbilityInstance : AbilityInstance
        {
        }
    }
}
