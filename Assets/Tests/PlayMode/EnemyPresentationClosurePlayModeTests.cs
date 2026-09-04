using System.Collections;
using System.Linq;
using CGame.Network;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CGame.GameplayCue.PlayModeTests
{
    public sealed class EnemyPresentationClosurePlayModeTests
    {
        [UnityTest]
        public IEnumerator TPSBundleVariants_InstantiateAndAcceptPresentationParameters()
        {
#if UNITY_EDITOR
            EnemyPresentationCatalog catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyPresentationCatalog>(
                "Assets/Settings/Gameplay/Enemy/EnemyPresentationCatalog.asset");
            Assert.That(catalog, Is.Not.Null);
            AsyncOperation load = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/EnemyPresentationValidation.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            while (!load.isDone) yield return null;
            yield return null;

            EnemyPresentation[] presentations = Object.FindObjectsOfType<EnemyPresentation>();
            Assert.That(presentations, Has.Length.EqualTo(catalog.Archetypes.Count));
            foreach (EnemyPresentation presentation in presentations)
            {
                Assert.That(presentation, Is.Not.Null);
                Assert.That(presentation.Animator.applyRootMotion, Is.False);
                Assert.DoesNotThrow(() =>
                {
                    presentation.ApplyMovement(1f, Vector2.right);
                    presentation.PlayFire();
                    presentation.PlayHit();
                });
                presentation.ApplyRemoteAnimationState(
                    new RemoteEnemyAnimationState(1f, Vector2.right, true, Quaternion.identity),
                    Time.deltaTime);
                Assert.That(presentation.HasPlayableGraph, Is.True);
            }
#else
            Assert.Ignore("This test resolves the catalog through the Editor AssetDatabase.");
            yield return null;
#endif
        }

        [UnityTest]
        public IEnumerator RifleSpawnAndSnapshot_CreatePlayableRemotePresentation()
        {
#if UNITY_EDITOR
            EnemyPresentationCatalog catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyPresentationCatalog>(
                "Assets/Settings/Gameplay/Enemy/EnemyPresentationCatalog.asset");
            Assert.That(catalog, Is.Not.Null);

            using var registry = new EnemyPresentationRegistry(catalog);
            Assert.That(registry.Spawn(new EnemySpawnedEvent
            {
                EnemyId = 55,
                ArchetypeId = "Enemy.Rifle",
                Position = QuantizedVector3WireMessage.FromValue(new QuantizedVector3(0, 0, 0)),
                Rotation = QuantizedQuaternionWireMessage.FromValue(new QuantizedQuaternion(0, 0, 0, short.MaxValue)),
                AuthorityServerTick = 1,
                Health = 100
            }), Is.True);
            Assert.That(registry.ApplySnapshot(new EnemySnapshotEvent
            {
                EnemyId = 55,
                Position = QuantizedVector3WireMessage.FromValue(new QuantizedVector3(1000, 0, 0)),
                Rotation = QuantizedQuaternionWireMessage.FromValue(
                    QuantizedQuaternion.FromQuaternion(Quaternion.Euler(0f, 90f, 0f))),
                PlanarVelocity = QuantizedVector3WireMessage.FromValue(
                    QuantizedVector3.FromMeters(Vector3.right)),
                IsGrounded = true,
                BrainState = EnemyBrainState.PeekFire,
                CoverPointId = "Cover.B",
                AuthorityServerTick = 2,
                Health = 100
            }), Is.True);

            registry.Tick(0.1f, Time.realtimeSinceStartup);
            yield return null;

            EnemyPresentation rifle = Object.FindObjectsOfType<EnemyPresentation>()
                .Single(candidate => candidate.gameObject.name.StartsWith("NetworkEnemyRifle"));
            Assert.That(rifle.RemoteAnimationState.IsMoving, Is.True);
            Assert.That(rifle.RemoteAnimationState.IsGrounded, Is.True);
            Assert.That(rifle.RemoteAnimationState.BrainState, Is.EqualTo(EnemyBrainState.PeekFire));
            Assert.That(rifle.HasPlayableGraph, Is.True);
            Assert.That(rifle.Animator.applyRootMotion, Is.False);
#else
            Assert.Ignore("This test resolves the catalog through the Editor AssetDatabase.");
            yield return null;
#endif
        }
    }
}
