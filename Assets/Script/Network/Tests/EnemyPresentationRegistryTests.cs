using System.IO;
using NUnit.Framework;
using UnityEngine;
using System.Linq;

namespace CGame.Network.Tests
{
    public sealed class EnemyPresentationRegistryTests
    {
        private GameObject prefab;
        private EnemyArchetypeSpec archetype;
        private EnemyPresentationCatalog catalog;

        [SetUp]
        public void SetUp()
        {
            prefab = new GameObject("EnemyPresentationPrefab");
            var presentation = prefab.AddComponent<EnemyPresentation>();
            presentation.Configure(prefab.AddComponent<Animator>());
            archetype = ScriptableObject.CreateInstance<EnemyArchetypeSpec>();
            archetype.Configure("Enemy.Test", prefab);
            catalog = ScriptableObject.CreateInstance<EnemyPresentationCatalog>();
            catalog.Configure(archetype);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(prefab);
            Object.DestroyImmediate(archetype);
            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void SnapshotInterpolation_UsesOneTenthSecondStep_AndFreezesAfterHalfSecond()
        {
            using var registry = new EnemyPresentationRegistry(catalog);
            Assert.That(registry.Spawn(Spawn()), Is.True);
            Assert.That(registry.ApplySnapshot(Snapshot(10_000)), Is.True);

            registry.Tick(0.05f, Time.realtimeSinceStartup);
            EnemyPresentation instance = Object.FindObjectsByType<EnemyPresentation>(UnityEngine.FindObjectsSortMode.None)
                .Single(candidate => candidate.gameObject != prefab);
            Assert.That(instance.transform.position.x, Is.EqualTo(5f).Within(0.01f));

            registry.Tick(0.1f, Time.realtimeSinceStartup + 0.6f);
            Assert.That(instance.transform.position.x, Is.EqualTo(5f).Within(0.01f));
            Assert.That(instance.RemoteAnimationState.IsMoving, Is.False);
        }

        [TestCase("Pistol")]
        [TestCase("Rifle")]
        [TestCase("Ak")]
        public void ProductionPrefab_HasHumanoidVisualAndLocomotionContract(string kind)
        {
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Art/Characters/Enemies/TPSBundle/Prefabs/NetworkEnemy" + kind + ".prefab");
            Assert.That(asset, Is.Not.Null);
            var presentation = asset.GetComponentInChildren<EnemyPresentation>(true);
            Assert.That(presentation, Is.Not.Null);
            Assert.That(presentation.VisualRoot, Is.Not.Null);
            Assert.That(presentation.VisualRoot, Is.Not.EqualTo(presentation.transform));
            var animator = presentation.Animator;
            Assert.That(animator, Is.Not.Null);
            Assert.That(animator.applyRootMotion, Is.False);
            Assert.That(animator.avatar != null && animator.avatar.isValid && animator.avatar.isHuman, Is.True);
            var controller = animator.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
            Assert.That(controller, Is.Not.Null);
            foreach (string name in new[] { "Speed", "MoveX", "MoveY" })
                Assert.That(controller.parameters.Any(p => p.name == name && p.type == AnimatorControllerParameterType.Float), Is.True, name);
            foreach (string name in new[] { "IsInCover", "IsPeeking" })
                Assert.That(controller.parameters.Any(p => p.name == name && p.type == AnimatorControllerParameterType.Bool), Is.True, name);
            var locomotion = controller.layers[0].stateMachine.states.Single(s => s.state.name == "Locomotion").state;
            var tree = locomotion.motion as UnityEditor.Animations.BlendTree;
            Assert.That(tree, Is.Not.Null);
            Assert.That(tree.blendParameter, Is.EqualTo("MoveX"));
            Assert.That(tree.blendParameterY, Is.EqualTo("MoveY"));
            Assert.That(tree.children.Any(c => c.position == Vector2.up && c.motion != null), Is.True);
            Assert.That(tree.children.Any(c => c.position == Vector2.zero && c.motion != null), Is.True);
        }

        [Test]
        public void RemoteEnemyAnimationState_MapsSnapshotVelocityFacingAndGrounding()
        {
            var snapshot = new EnemySnapshotEvent
            {
                EnemyId = 1,
                AuthorityServerTick = 4,
                Rotation = QuantizedQuaternionWireMessage.FromValue(QuantizedQuaternion.FromQuaternion(Quaternion.Euler(0f, 90f, 0f))),
                PlanarVelocity = QuantizedVector3WireMessage.FromValue(QuantizedVector3.FromMeters(new Vector3(2f, 0f, 0f))),
                IsGrounded = true,
                BrainState = EnemyBrainState.CoverHold
            };

            RemoteEnemyAnimationState state = RemoteEnemyAnimationState.FromSnapshot(snapshot);

            Assert.That(state.IsMoving, Is.True);
            Assert.That(state.Speed, Is.EqualTo(2f).Within(0.001f));
            Assert.That(state.MoveDirection.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(state.MoveDirection.y, Is.EqualTo(1f).Within(0.001f));
            Assert.That(state.IsGrounded, Is.True);
            Assert.That(state.BrainState, Is.EqualTo(EnemyBrainState.CoverHold));
            Assert.That(state.IsInCover, Is.True);
            Assert.That(state.IsPeeking, Is.False);
        }

        [TestCase(EnemyBrainState.CoverHold, true, false)]
        [TestCase(EnemyBrainState.PeekFire, true, true)]
        [TestCase(EnemyBrainState.ReturnToCover, true, false)]
        [TestCase(EnemyBrainState.Chase, false, false)]
        public void RemoteEnemyAnimationState_DerivesCoverParameters(
            EnemyBrainState brainState,
            bool expectedInCover,
            bool expectedPeeking)
        {
            var state = new RemoteEnemyAnimationState(0f, Vector2.zero, true, Quaternion.identity, brainState);

            Assert.That(state.IsInCover, Is.EqualTo(expectedInCover));
            Assert.That(state.IsPeeking, Is.EqualTo(expectedPeeking));
        }

        [Test]
        public void RemoteEnemyAnimationState_SubThresholdVelocityHasNoDirection()
        {
            EnemySnapshotEvent snapshot = Snapshot(0);
            snapshot.PlanarVelocity = QuantizedVector3WireMessage.FromValue(
                QuantizedVector3.FromMeters(new Vector3(0.02f, 0f, 0f)));
            RemoteEnemyAnimationState state = RemoteEnemyAnimationState.FromSnapshot(snapshot);
            Assert.That(state.IsMoving, Is.False);
            Assert.That(state.MoveDirection, Is.EqualTo(Vector2.zero));
        }

        [TestCase("Pistol")]
        [TestCase("Rifle")]
        [TestCase("Ak")]
        public void RemotePresentation_MovementReachesTheRenderedControllerPlayable(string kind)
        {
            GameObject asset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Art/Characters/Enemies/TPSBundle/Prefabs/NetworkEnemy" + kind + ".prefab");
            GameObject root = Object.Instantiate(asset);
            try
            {
                EnemyPresentation presentation = root.GetComponentInChildren<EnemyPresentation>();
                presentation.ApplyRemoteAnimationState(
                    new RemoteEnemyAnimationState(2f, Vector2.up, true, Quaternion.identity,
                        EnemyBrainState.PeekFire), 0.1f);
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                object controller = typeof(EnemyPresentation).GetField("playablesController", flags)
                    .GetValue(presentation);
                var source = (UnityEngine.Animations.AnimatorControllerPlayable)controller.GetType()
                    .GetField("animatorControllerSource", flags).GetValue(controller);
                Assert.That(source.GetFloat("Speed"), Is.EqualTo(2f).Within(0.001f));
                Assert.That(source.GetBool("IsInCover"), Is.True);
                Assert.That(source.GetBool("IsPeeking"), Is.False,
                    "Travel to a peek point must not select the stationary full-body peek pose.");
                Assert.That(source.GetFloat("MoveY"), Is.EqualTo(1f).Within(0.001f));
                presentation.ApplyRemoteAnimationState(
                    new RemoteEnemyAnimationState(0f, Vector2.zero, true, Quaternion.identity,
                        EnemyBrainState.PeekFire), 0.1f);
                Assert.That(source.GetBool("IsPeeking"), Is.True);
                Assert.That(source.GetFloat("MoveY"), Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RemotePresentation_SourceUsesSnapshotStateWithoutClientMotorOrNavMesh()
        {
            string sourcePath = Path.Combine(
                Application.dataPath,
                "Script/Network/Runtime/Enemy/EnemyPresentation.cs");
            string source = File.ReadAllText(sourcePath);

            Assert.That(source, Does.Contain("RemoteEnemyAnimationState"));
            Assert.That(source, Does.Contain("applyRootMotion = false"));
            Assert.That(source, Does.Not.Contain("CharacterPhysicsMotor"));
            Assert.That(source, Does.Not.Contain("NavMeshAgent"));
        }

        [Test]
        public void SharedPlayback_DoesNotOverwriteCurveDrivenPlayerParameters()
        {
            GameObject asset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Art/Characters/Enemies/TPSBundle/Prefabs/NetworkEnemyRifle.prefab");
            GameObject root = Object.Instantiate(asset);
            CGame.Animation.CharacterPlayablesController playback = null;
            try
            {
                Animator animator = root.GetComponent<EnemyPresentation>().Animator;
                string guid = UnityEditor.AssetDatabase.FindAssets("FPSAnimator_Generic_Project t:AnimatorController",
                    new[] { "Assets/Art" }).Single();
                animator.runtimeAnimatorController = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                    UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                animator.Rebind();
                animator.Update(0f);
                playback = new CGame.Animation.CharacterPlayablesController(new Pawn(root), animator);
                Assert.That(playback.TryRebuild(), Is.True);
                animator.playableGraph.Evaluate(0.1f);
                var source = (UnityEngine.Animations.AnimatorControllerPlayable)playback.GetType()
                    .GetField("animatorControllerSource", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .GetValue(playback);
                Assert.That(source.IsParameterControlledByCurve("FullBodyWeight"), Is.True);
                playback.Update(0.1f);
                UnityEngine.TestTools.LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                playback?.Dispose();
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ActionPresentation_RecordsOnlyConfirmedFireAndHitActions()
        {
            using var registry = new EnemyPresentationRegistry(catalog);
            Assert.That(registry.Spawn(Spawn()), Is.True);

            Assert.That(registry.ApplyAction(new EnemyActionEvent { EnemyId = 1, ActionSequence = 1, ActionKind = EnemyActionKind.Fire, AuthorityServerTick = 1 }), Is.True);
            EnemyPresentation instance = Object.FindObjectsByType<EnemyPresentation>(UnityEngine.FindObjectsSortMode.None)
                .Single(candidate => candidate.gameObject != prefab);
            Assert.That(instance.LastConfirmedAction, Is.EqualTo(EnemyActionKind.Fire));
            Assert.That(registry.ApplyAction(new EnemyActionEvent { EnemyId = 1, ActionSequence = 2, ActionKind = EnemyActionKind.Hit, AuthorityServerTick = 2 }), Is.True);
            Assert.That(instance.LastConfirmedAction, Is.EqualTo(EnemyActionKind.Hit));
        }

        [Test]
        public void Death_PreservesTerminalPresentationThenReclaimsOnce()
        {
            using var registry = new EnemyPresentationRegistry(catalog);
            registry.Spawn(Spawn());
            var death = new EnemyActionEvent
            {
                EnemyId = 1, ActionSequence = 3, ActionKind = EnemyActionKind.Death,
                AuthorityServerTick = 3
            };
            Assert.That(registry.ApplyAction(death), Is.True);
            Assert.That(registry.Count, Is.EqualTo(1), "Death must be visible before reclamation.");
            Assert.That(registry.ApplyAction(death), Is.False);
            Assert.That(registry.ApplyAction(new EnemyActionEvent
            {
                EnemyId = 1, ActionSequence = 4, ActionKind = EnemyActionKind.Fire
            }), Is.False);
            Assert.That(registry.ApplySnapshot(Snapshot(1000)), Is.False);
            EnemyPresentation instance = Object.FindObjectsByType<EnemyPresentation>(FindObjectsSortMode.None)
                .Single(candidate => candidate.gameObject != prefab);
            Assert.That(instance.LastConfirmedAction, Is.EqualTo(EnemyActionKind.Death));
            registry.Tick(0.1f, Time.realtimeSinceStartup + 10f);
            Assert.That(registry.Count, Is.Zero);
            Assert.That(registry.Spawn(Spawn()), Is.False, "A late spawn cannot resurrect a dead enemy.");
            Assert.DoesNotThrow(() => registry.Tick(0.1f, Time.realtimeSinceStartup + 11f));
        }

        [Test]
        public void Hit_HasPriorityOverFireDuringReaction()
        {
            EnemyPresentation presentation = prefab.GetComponent<EnemyPresentation>();
            presentation.PlayHit();
            presentation.PlayFire();
            Assert.That(presentation.LastConfirmedAction, Is.EqualTo(EnemyActionKind.Hit));
        }

        private static EnemySpawnedEvent Spawn() => new EnemySpawnedEvent
        {
            EnemyId = 1,
            ArchetypeId = "Enemy.Test",
            Position = QuantizedVector3WireMessage.FromValue(new QuantizedVector3(0, 0, 0)),
            Rotation = QuantizedQuaternionWireMessage.FromValue(new QuantizedQuaternion(0, 0, 0, short.MaxValue)),
            AuthorityServerTick = 1,
            Health = 100
        };

        private static EnemySnapshotEvent Snapshot(int xMillimeters) => new EnemySnapshotEvent
        {
            EnemyId = 1,
            AuthorityServerTick = 2,
            Position = QuantizedVector3WireMessage.FromValue(new QuantizedVector3(xMillimeters, 0, 0)),
            Rotation = QuantizedQuaternionWireMessage.FromValue(new QuantizedQuaternion(0, 0, 0, short.MaxValue)),
            PlanarVelocity = QuantizedVector3WireMessage.FromValue(default),
            Health = 100
        };
    }
}
