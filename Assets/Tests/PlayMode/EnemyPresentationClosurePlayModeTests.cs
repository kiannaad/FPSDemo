using System.Collections;
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
            }
#else
            Assert.Ignore("This test resolves the catalog through the Editor AssetDatabase.");
            yield return null;
#endif
        }
    }
}
