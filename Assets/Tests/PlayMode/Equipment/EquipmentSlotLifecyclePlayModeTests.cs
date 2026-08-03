using System.Collections;
using System.Reflection;
using CGame.Ability;
using CGame.Animation;
using CGame.GameplayTags;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CGame.Weapon.Equipment.PlayMode.Tests
{
    public sealed class EquipmentSlotLifecyclePlayModeTests
    {
        [UnityTest]
        public IEnumerator DestroyingRuntimeHostRevokesEquipmentButKeepsPlayerStateAlive()
        {
            GameplayTagSource tagSource = ScriptableObject.CreateInstance<GameplayTagSource>();
            tagSource.SetDefinition(
                "EquipmentPlayMode",
                new[]
                {
                    new GameplayTagSourceNode(
                        "State",
                        false,
                        children: new[]
                        {
                            new GameplayTagSourceNode(
                                "Equipment",
                                false,
                                children: new[] { new GameplayTagSourceNode("Active", true) })
                        })
                });
            GameplayTagRegistryBuildResult tagResult = GameplayTagManager.Instance.Initialize(new[] { tagSource });
            Assert.That(tagResult.Succeeded, Is.True);
            GameplayTag equipmentTag = GameplayTagManager.Instance.RequestTag("State.Equipment.Active");
            var playerState = new PlayerState(new AbilitySet(), new object());
            var pawn = new Pawn();
            playerState.SetAvatar(pawn);
            var runtime = new WeaponRuntime();
            runtime.Initialize(
                new WeaponId("rifle"),
                new WeaponRuntimeCapabilities(true, true, false));
            WeaponAnimationDefinition definition = CreateDefinition("rifle");
            var lease = new FakeDefinitionLease(definition);
            var slot = new EquipmentSlot(playerState.AbilitySystem, runtime);
            var owner = new GameObject("EquipmentSlotHost");
            owner.AddComponent<EquipmentSlotHost>().Initialize(slot);

            Assert.That(
                slot.TryEquip(
                    lease,
                    new[] { new AbilitySet(ownedTags: new[] { equipmentTag }) },
                    out _),
                Is.EqualTo(EquipmentEquipResult.Equipped));
            Assert.That(playerState.AbilitySystem.HasOwnedTagExact(equipmentTag), Is.True);

            UnityEngine.Object.Destroy(owner);
            yield return null;

            Assert.That(lease.ReleaseCount, Is.EqualTo(1));
            Assert.That(playerState.AbilitySystem.HasOwnedTagExact(equipmentTag), Is.False);
            Assert.That(playerState.IsDisposed, Is.False);
            Assert.That(playerState.Avatar, Is.SameAs(pawn));

            playerState.Dispose();
            GameplayTagManager.Instance.Shutdown();
            UnityEngine.Object.Destroy(definition);
            UnityEngine.Object.Destroy(tagSource);
            yield return null;
        }

        private static WeaponAnimationDefinition CreateDefinition(string weaponId)
        {
            WeaponAnimationDefinition definition = ScriptableObject.CreateInstance<WeaponAnimationDefinition>();
            typeof(WeaponAnimationDefinition)
                .GetField("weaponId", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(definition, weaponId);
            return definition;
        }

        private sealed class FakeDefinitionLease : IEquipmentDefinitionLease
        {
            public FakeDefinitionLease(WeaponAnimationDefinition definition)
            {
                Definition = definition;
            }

            public WeaponAnimationDefinition Definition { get; }
            public WeaponId WeaponId => Definition == null ? default : Definition.WeaponId;
            public bool IsValid => !IsDisposed && Definition != null && WeaponId.IsValid;
            public bool IsDisposed { get; private set; }
            public int ReleaseCount { get; private set; }

            public void Dispose()
            {
                if (IsDisposed)
                {
                    return;
                }

                IsDisposed = true;
                ReleaseCount++;
            }
        }

        private sealed class EquipmentSlotHost : MonoBehaviour
        {
            private EquipmentSlot slot;

            public void Initialize(EquipmentSlot equipmentSlot)
            {
                slot = equipmentSlot;
            }

            private void OnDestroy()
            {
                slot?.Dispose();
                slot = null;
            }
        }
    }
}
