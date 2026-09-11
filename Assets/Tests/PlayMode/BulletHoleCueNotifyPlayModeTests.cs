using System.Collections;
using CGame.Ability.Cues;
using CGame.Ability.Effects;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CGame.GameplayCue.PlayModeTests
{
    public sealed class BulletHoleCueNotifyPlayModeTests
    {
        [TestCase(0, 1)]
        [TestCase(101, 0)]
        public void AuthorityImpact_OnlyEnvironmentHitsRouteToSurfaceDecals(long hitEnemyId, int expectedCount)
        {
            var source = ScriptableObject.CreateInstance<CGame.GameplayTags.GameplayTagSource>();
            source.SetDefinition("ImpactRouting", new[]
            {
                new CGame.GameplayTags.GameplayTagSourceNode("GameplayCue", false, children: new[]
                {
                    new CGame.GameplayTags.GameplayTagSourceNode("Weapon", false, children: new[]
                    {
                        new CGame.GameplayTags.GameplayTagSourceNode("Impact", true)
                    })
                })
            });
            var previousRouter = GameplayCueRouter.Current;
            var router = new RecordingImpactRouter();
            try
            {
                CGame.GameplayTags.GameplayTagManager.Instance.Initialize(new[] { source });
                GameplayCueRouter.Register(router);
                var committed = new CGame.Network.FireCommitted
                {
                    HasImpact = true, ImpactId = 1, HitEnemyId = hitEnemyId,
                    ImpactPositionY = 1.3f, ImpactNormalZ = -1f
                };
                var wire = CGame.Network.NetworkMessageSerializer.Serialize(committed);
                committed = CGame.Network.NetworkMessageSerializer.Deserialize<CGame.Network.FireCommitted>(wire);
                Assert.That(committed.HitEnemyId, Is.EqualTo(hitEnemyId));
                typeof(NetworkGameMode).GetMethod("ExecuteAuthorityImpactCue",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(null, new object[] { null, committed, "RoutingContract" });
                Assert.That(router.ExecuteCount, Is.EqualTo(expectedCount),
                    "Enemy Hit/Death actions must not leave a static wall decal at the old body position.");
            }
            finally
            {
                GameplayCueRouter.Register(previousRouter);
                CGame.GameplayTags.GameplayTagManager.Instance.Shutdown();
                Object.DestroyImmediate(source);
            }
        }

        private sealed class RecordingImpactRouter : IGameplayCueRouter
        {
            public int ExecuteCount { get; private set; }
            public void Execute(CGame.GameplayTags.GameplayTag cueTag, GameplayCueParameters parameters) => ExecuteCount++;
            public GameplayCueHandle Add(CGame.GameplayTags.GameplayTag cueTag, GameplayCueParameters parameters) => default;
            public bool Remove(GameplayCueHandle handle) => false;
            public void RemoveForTarget(object target) { }
        }

        [UnityTest]
        public IEnumerator ExecutedImpact_CreatesSurfaceOffsetBulletHole_ThenFadesAndCleansUp()
        {
            BulletHoleCueNotifyDefinition notify = ScriptableObject.CreateInstance<BulletHoleCueNotifyDefinition>();
            notify.Configure(null, 0.1f, 0.05f, 0.2f, 0.01f, 1);
            var parameters = new GameplayCueParameters(
                new GameplayEffectContext(new object(), new object(), new object()),
                location: new Vector3(1f, 2f, 3f),
                hasLocation: true,
                normal: Vector3.forward,
                hasNormal: true);

            notify.HandleCue(GameplayCueEventType.Executed, parameters, default);
            yield return null;

            GameObject bulletHole = GameObject.Find("GameplayCueBulletHole");
            Assert.That(bulletHole, Is.Not.Null);
            Assert.That(Vector3.Distance(bulletHole.transform.position, new Vector3(1f, 2f, 3.01f)), Is.LessThan(0.0001f));
            Assert.That(Vector3.Dot(-bulletHole.transform.forward, Vector3.forward), Is.GreaterThan(0.9999f));
            Assert.That(Vector3.Dot(bulletHole.transform.up, Vector3.up), Is.GreaterThan(0.9999f));
            Assert.That(notify.ActiveBulletHoleCount, Is.EqualTo(1));

            yield return new WaitForSeconds(0.15f);
            Assert.That(GameObject.Find("GameplayCueBulletHole"), Is.Null);
            Assert.That(notify.ActiveBulletHoleCount, Is.EqualTo(0));
            Object.DestroyImmediate(notify);
        }

        [UnityTest]
        public IEnumerator ExecutedImpact_TransparentTexture_DoesNotDrawOpaqueBackground()
        {
            var transparentTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            transparentTexture.SetPixels(new[]
            {
                new Color(0f, 0f, 0f, 0f),
                new Color(0f, 0f, 0f, 0f),
                new Color(0f, 0f, 0f, 0f),
                new Color(0f, 0f, 0f, 0f)
            });
            transparentTexture.Apply();

            BulletHoleCueNotifyDefinition notify = ScriptableObject.CreateInstance<BulletHoleCueNotifyDefinition>();
            notify.Configure(transparentTexture, 1f, 0.1f, 1f, 0.002f, 1);
            var parameters = new GameplayCueParameters(
                new GameplayEffectContext(new object(), new object(), new object()),
                location: new Vector3(0f, 0f, 2f),
                hasLocation: true,
                normal: Vector3.back,
                hasNormal: true);

            var cameraObject = new GameObject("BulletHoleTransparencyTestCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.green;
            camera.orthographic = true;
            camera.orthographicSize = 1f;
            var renderTexture = new RenderTexture(32, 32, 16, RenderTextureFormat.ARGB32);
            camera.targetTexture = renderTexture;

            notify.HandleCue(GameplayCueEventType.Executed, parameters, default);
            yield return null;
            camera.Render();

            RenderTexture previousActive = RenderTexture.active;
            RenderTexture.active = renderTexture;
            var captured = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            captured.ReadPixels(new Rect(0f, 0f, 32f, 32f), 0, 0);
            captured.Apply();
            Color centerPixel = captured.GetPixel(16, 16);
            RenderTexture.active = previousActive;

            Assert.That(centerPixel.g, Is.GreaterThan(0.8f));
            Assert.That(centerPixel.r, Is.LessThan(0.2f));
            Assert.That(centerPixel.b, Is.LessThan(0.2f));

            GameObject bulletHole = GameObject.Find("GameplayCueBulletHole");
            if (bulletHole != null) Object.DestroyImmediate(bulletHole);
            Object.DestroyImmediate(captured);
            camera.targetTexture = null;
            Object.DestroyImmediate(renderTexture);
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(notify);
            Object.DestroyImmediate(transparentTexture);
        }
    }
}
