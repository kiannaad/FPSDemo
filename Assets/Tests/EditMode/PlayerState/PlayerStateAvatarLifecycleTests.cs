using System;
using System.Collections.Generic;
using System.Linq;
using CGame.Ability;
using CGame.GameplayTags;
using NUnit.Framework;
using UnityEngine;

namespace CGame.PlayerStateTests
{
    public sealed class PlayerStateAvatarLifecycleTests
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
        public void ChangeAvatar_PreservesSpecsEndsOldInstanceMovesPawnLinkAndDisposeClearsEverything()
        {
            InitializeTags(Node("Ability", false, Node("Test", false, Node("AvatarBound", true))));
            GameplayTag abilityTag = GameplayTagManager.Instance.RequestTag("Ability.Test.AvatarBound");
            var definition = new AvatarBoundAbilityDefinition(abilityTag);
            var baseSet = new AbilitySet(new[] { definition });
            var source = new object();
            var playerState = new global::CGame.PlayerState(baseSet, source);
            var firstPawn = new Pawn();
            var secondPawn = new Pawn();

            Assert.That(playerState.AbilitySystem.AbilityCount, Is.EqualTo(1));
            Assert.That(playerState.SetAvatar(firstPawn), Is.True);
            Assert.That(firstPawn.AbilitySystem, Is.SameAs(playerState.AbilitySystem));
            Assert.That(playerState.AbilitySystem.TryActivateAbilityByTag(abilityTag).Succeeded, Is.True);
            AbilitySpecHandle specHandle = playerState.BaseGrantReceipt.SpecHandles[0];
            Assert.That(playerState.AbilitySystem.TryGetSpec(specHandle, out AbilitySpec spec), Is.True);

            Assert.That(playerState.SetAvatar(secondPawn), Is.True);
            Assert.That(spec.PrimaryInstance.LastEndReason, Is.EqualTo(AbilityEndReason.AvatarChanged));
            Assert.That(playerState.AbilitySystem.TryGetSpec(specHandle, out _), Is.True);
            Assert.That(firstPawn.AbilitySystem, Is.Null);
            Assert.That(secondPawn.AbilitySystem, Is.SameAs(playerState.AbilitySystem));
            Assert.That(playerState.SetAvatar(secondPawn), Is.False);

            playerState.Dispose();

            Assert.That(playerState.IsDisposed, Is.True);
            Assert.That(playerState.BaseGrantReceipt.IsActive, Is.False);
            Assert.That(playerState.AbilitySystem.AbilityCount, Is.Zero);
            Assert.That(playerState.AbilitySystem.Avatar, Is.Null);
            Assert.That(secondPawn.AbilitySystem, Is.Null);
        }

        [Test]
        public void PlayerStateManager_OldBindingReleaseCannotClearNewAvatarAndShutdownOwnsState()
        {
            InitializeTags(Node("Ability", false, Node("Test", false, Node("Base", true))));
            GameplayTag abilityTag = GameplayTagManager.Instance.RequestTag("Ability.Test.Base");
            var baseSet = new AbilitySet(new[] { new AvatarBoundAbilityDefinition(abilityTag) });
            var manager = new PlayerStateManager();
            global::CGame.PlayerState state = manager.Initialize(baseSet, new object());
            var firstPawn = new Pawn();
            var secondPawn = new Pawn();

            Assert.Throws<InvalidOperationException>(() => manager.Initialize(baseSet, new object()));
            PlayerStateAvatarBinding firstBinding = manager.BindAvatar(firstPawn);
            PlayerStateAvatarBinding secondBinding = manager.BindAvatar(secondPawn);

            firstBinding.Dispose();
            Assert.That(state.Avatar, Is.SameAs(secondPawn));
            Assert.That(secondPawn.AbilitySystem, Is.SameAs(state.AbilitySystem));

            secondBinding.Dispose();
            Assert.That(state.Avatar, Is.Null);
            Assert.That(state.AbilitySystem.AbilityCount, Is.EqualTo(1));

            manager.Shutdown();
            Assert.That(state.IsDisposed, Is.True);
            Assert.That(manager.IsInitialized, Is.False);
            manager.Shutdown();
        }

        [Test]
        public void SetAvatar_TargetOwnedByAnotherStateRejectsWithoutChangingEitherBinding()
        {
            var firstState = new global::CGame.PlayerState(new AbilitySet(), new object());
            var secondState = new global::CGame.PlayerState(new AbilitySet(), new object());
            var firstPawn = new Pawn();
            var secondPawn = new Pawn();

            try
            {
                firstState.SetAvatar(firstPawn);
                secondState.SetAvatar(secondPawn);

                Assert.Throws<InvalidOperationException>(() => firstState.SetAvatar(secondPawn));
                Assert.That(firstState.Avatar, Is.SameAs(firstPawn));
                Assert.That(secondState.Avatar, Is.SameAs(secondPawn));
                Assert.That(firstPawn.AbilitySystem, Is.SameAs(firstState.AbilitySystem));
                Assert.That(secondPawn.AbilitySystem, Is.SameAs(secondState.AbilitySystem));
            }
            finally
            {
                firstState.Dispose();
                secondState.Dispose();
            }
        }

        private void InitializeTags(params GameplayTagSourceNode[] roots)
        {
            GameplayTagSource source = ScriptableObject.CreateInstance<GameplayTagSource>();
            source.SetDefinition("PlayerStateTests", roots);
            sourcesToDestroy.Add(source);
            GameplayTagRegistryBuildResult result = GameplayTagManager.Instance.Initialize(new[] { source });
            Assert.That(result.Succeeded, Is.True, string.Join(Environment.NewLine, result.Errors.Select(error => error.ToString())));
        }

        private static GameplayTagSourceNode Node(string segmentName, bool isExplicit, params GameplayTagSourceNode[] children)
        {
            return new GameplayTagSourceNode(segmentName, isExplicit, children: children);
        }

        private sealed class AvatarBoundAbilityDefinition : AbilityDefinition
        {
            public AvatarBoundAbilityDefinition(GameplayTag abilityTag)
                : base(abilityTag)
            {
            }

            protected override AbilityInstance CreateInstance()
            {
                return new AvatarBoundAbilityInstance();
            }
        }

        private sealed class AvatarBoundAbilityInstance : AbilityInstance
        {
        }
    }
}
