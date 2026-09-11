using System.Collections;
using System.Linq;
using CGame.Ability;
using CGame.Ability.Cues;
using CGame.Ability.Effects;
using CGame.GameplayTags;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CGame.GameplayCue.PlayModeTests
{
    public sealed class GameplayCueConfigurationPlayModeTests
    {
        [UnityTest]
        public IEnumerator EnemyCuePrefabs_FollowAllMuzzlesAndExpireWithoutMovingTheirOwners()
        {
            var fire = UnityEditor.AssetDatabase.LoadAssetAtPath<AttachedPrefabCueNotifyDefinition>(
                "Assets/Settings/Gameplay/Cues/EnemyFireCueNotify.asset");
            var hit = UnityEditor.AssetDatabase.LoadAssetAtPath<AttachedPrefabCueNotifyDefinition>(
                "Assets/Settings/Gameplay/Cues/EnemyImpactCueNotify.asset");
            Assert.That(fire, Is.Not.Null);
            Assert.That(hit, Is.Not.Null);
            foreach (string variant in new[] { "Pistol", "Rifle", "Ak" })
            {
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    $"Assets/Art/Characters/Enemies/TPSBundle/Prefabs/NetworkEnemy{variant}.prefab");
                var root = Object.Instantiate(prefab, new Vector3(2f, 0f, 3f), Quaternion.Euler(0f, 40f, 0f));
                try
                {
                    Vector3 position = root.transform.position;
                    Quaternion rotation = root.transform.rotation;
                    var parameters = new GameplayCueParameters(default, root);
                    var muzzle = root.GetComponentsInChildren<Transform>().Single(value => value.name == "muzzle");
                    fire.HandleCue(GameplayCueEventType.OnActive, parameters, default);
                    Assert.That(muzzle.Find("flash(Clone)"), Is.Null, "Only confirmed one-shot cues spawn effects.");
                    fire.HandleCue(GameplayCueEventType.Executed, parameters, default);
                    var flash = muzzle.Find("flash(Clone)");
                    Assert.That(flash, Is.Not.Null, variant);
                    Assert.That(flash.localPosition, Is.EqualTo(Vector3.zero));
                    Assert.That(Quaternion.Angle(flash.localRotation, Quaternion.Euler(0f, 0f, -90f)), Is.LessThan(.01f));
                    hit.HandleCue(GameplayCueEventType.Executed, parameters, default);
                    var impact = root.transform.Find("hitEffect(Clone)");
                    Assert.That(impact, Is.Not.Null);
                    Assert.That(impact.GetComponent<ParticleSystem>().isPlaying, Is.True);
                    yield return new WaitForSeconds(.2f);
                    Assert.That(flash == null, Is.True, "Muzzle flash must not remain frozen on the gun.");
                    yield return new WaitForSeconds(.4f);
                    Assert.That(impact == null, Is.True, "Impact must release its transient instance.");
                    Assert.That(root.transform.position, Is.EqualTo(position));
                    Assert.That(root.transform.rotation, Is.EqualTo(rotation));
                }
                finally { Object.DestroyImmediate(root); }
            }
        }

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
