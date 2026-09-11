using System.Collections;
using System.Collections.Generic;
using CGame.Ability.Attributes;
using CGame.Ability.Cues;
using CGame.GameplayTags;
using CGame.InventoryEquipment;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CGame.GameplayCue.PlayModeTests
{
    public sealed class GameplayDamageIntegrationPlayModeTests
    {
        [UnityTest]
        public IEnumerator FormalFireInput_CharacterHitAppliesDamageEffectAndCuesInOrder()
        {
            using var offlineFixture = new OfflineSampleSceneFixture();
            if (World.Current != null)
            {
                var shutdown = World.Current.ShutdownAsync();
                while (!shutdown.IsCompleted) yield return null;
            }

            Mouse mouse = InputSystem.AddDevice<Mouse>();
            GameInstance gameInstance = null;
            try
            {
#if UNITY_EDITOR
                AsyncOperation load = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                    "Assets/Scenes/SampleScene.unity",
                    new LoadSceneParameters(LoadSceneMode.Single));
#else
                AsyncOperation load = SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
#endif
                while (!load.isDone) yield return null;
                gameInstance = Object.FindObjectOfType<GameInstance>();
                Assert.That(gameInstance, Is.Not.Null);
                yield return offlineFixture.Start(gameInstance);
                while (gameInstance.InitializationTask == null || !gameInstance.InitializationTask.IsCompleted)
                    yield return null;
                if (gameInstance.InitializationTask.IsFaulted)
                    throw gameInstance.InitializationTask.Exception.GetBaseException();

                World world = gameInstance.RuntimeWorld;
                DefaultGameState gameState = (DefaultGameState)world.GameState;
                Assert.That(gameState.ExperienceManager.Components.TryGet(out EnemySpawnGameComponent enemies), Is.True);
                while (enemies.SpawnTask == null || !enemies.SpawnTask.IsCompleted) yield return null;
                if (enemies.SpawnTask.IsFaulted) throw enemies.SpawnTask.Exception.GetBaseException();
                Assert.That(enemies.Handles.Count, Is.EqualTo(3));

                PlayerController controller = (PlayerController)world.GameMode.PlayerController;
                Pawn pawn = controller.PossessedPawn;
                EquipmentManagerComponent equipment = pawn.GetComponent<EquipmentManagerComponent>();
                for (int frame = 0;
                     frame < 300 && (equipment.IsSwitchInProgress ||
                                     equipment.CurrentWeapon == null ||
                                     !equipment.CurrentWeapon.IsArmed);
                     frame++)
                {
                    yield return null;
                }

                int rifleSlot = FindDamageWeaponSlot(controller);
                Assert.That(rifleSlot, Is.GreaterThanOrEqualTo(0));
                Assert.That(controller.TryRequestQuickBarSlot(rifleSlot), Is.True);
                for (int frame = 0;
                     frame < 300 && (equipment.CurrentWeapon == null ||
                                     equipment.CurrentWeapon.ItemHandle != controller.QuickBar.Slots[rifleSlot] ||
                                     !equipment.CurrentWeapon.IsArmed ||
                                     equipment.IsSwitchInProgress);
                     frame++)
                {
                    yield return null;
                }

                WeaponInstance weapon = equipment.CurrentWeapon;
                Assert.That(weapon, Is.Not.Null);
                Assert.That(weapon.Definition.DamageEffect, Is.Not.Null);
                EnemySpawnHandle target = enemies.Handles[0];
                var enemyHealthSets = new HealthSet[enemies.Handles.Count];
                for (int index = 0; index < enemies.Handles.Count; index++)
                {
                    enemyHealthSets[index] =
                        enemies.Handles[index].Controller.PlayerState.AbilitySystem.GetSet<HealthSet>();
                    Assert.That(enemyHealthSets[index].Health.CurrentValue, Is.EqualTo(60f));
                }
                Collider targetCollider = target.Pawn.Root.GetComponentInChildren<Collider>(true);
                Assert.That(targetCollider, Is.Not.Null);

                Camera camera = pawn.GetComponent<PawnCameraComponent>().Camera;
                Vector3 closeTargetPosition = camera.transform.position + camera.transform.forward * 2f;
                closeTargetPosition.y = target.Pawn.Transform.position.y;
                target.Pawn.GetComponent<PawnMovementComponent>().Motor.SetPosition(closeTargetPosition);
                Physics.SyncTransforms();
                Vector3 aimDirection = (targetCollider.bounds.center - camera.transform.position).normalized;
                Quaternion aimRotation = Quaternion.LookRotation(aimDirection, Vector3.up);
                pawn.Transform.rotation = Quaternion.Euler(0f, aimRotation.eulerAngles.y, 0f);
                pawn.ApplyingControlRotation(aimRotation);
                yield return null;
                pawn.PublishCameraShotRay(camera.transform.position, aimDirection);

                var cueTrace = new List<string>();
                GameplayCueManager cueManager = world.GetSubSystem<GameplayCueManager>();
                GameplayTag fireTag = GameplayTagManager.Instance.RequestTag("GameplayCue.Weapon.Fire");
                GameplayTag impactTag = GameplayTagManager.Instance.RequestTag("GameplayCue.Weapon.Impact");
                GameplayTag damageTakenTag = GameplayTagManager.Instance.RequestTag("GameplayCue.Weapon.DamageTaken");
                cueManager.RegisterRoute(fireTag, (_, _, _) => cueTrace.Add("Fire"));
                cueManager.RegisterRoute(impactTag, (_, _, _) => cueTrace.Add("Impact"));
                cueManager.RegisterRoute(damageTakenTag, (_, _, _) => cueTrace.Add("DamageTaken"));
                int ammoBefore = weapon.Item.MagazineAmmo;

                for (int attempt = 0; attempt < 3 && weapon.Item.MagazineAmmo == ammoBefore; attempt++)
                {
                    InputSystem.QueueStateEvent(mouse, new MouseState());
                    InputSystem.Update();
                    yield return null;
                    InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
                    InputSystem.Update();
                    pawn.PublishCameraShotRay(camera.transform.position, aimDirection);
                    yield return null;
                    InputSystem.QueueStateEvent(mouse, new MouseState());
                    InputSystem.Update();
                    yield return new WaitForSeconds(0.15f);
                }

                Assert.That(weapon.Item.MagazineAmmo, Is.EqualTo(ammoBefore - 1));
                int damagedEnemyCount = 0;
                for (int index = 0; index < enemyHealthSets.Length; index++)
                {
                    if (enemyHealthSets[index].Health.CurrentValue == 40f)
                    {
                        damagedEnemyCount++;
                    }
                    else
                    {
                        Assert.That(enemyHealthSets[index].Health.CurrentValue, Is.EqualTo(60f));
                    }

                    Assert.That(enemyHealthSets[index].Damage.CurrentValue, Is.Zero);
                }
                Assert.That(damagedEnemyCount, Is.EqualTo(1));
                Assert.That(cueTrace.Count, Is.GreaterThanOrEqualTo(3));
                CollectionAssert.AreEqual(
                    new[] { "Fire", "Impact", "DamageTaken" },
                    cueTrace.GetRange(0, 3));
                Assert.That(enemies.Handles[1].Pawn.Root.activeSelf, Is.True);
                Assert.That(enemies.Handles[2].Pawn.Root.activeSelf, Is.True);
            }
            finally
            {
                if (mouse.added) InputSystem.RemoveDevice(mouse);
                if (gameInstance != null) Object.Destroy(gameInstance.gameObject);
            }

            yield return null;
        }

        private static int FindDamageWeaponSlot(PlayerController controller)
        {
            for (int index = 0; index < controller.QuickBar.Slots.Count; index++)
            {
                if (!controller.Inventory.TryGet(controller.QuickBar.Slots[index], out ItemInstance item) ||
                    !(item.Definition is WeaponItemDefinition weaponItem) ||
                    weaponItem.WeaponDefinition == null ||
                    weaponItem.WeaponDefinition.DamageEffect == null)
                {
                    continue;
                }

                return index;
            }

            return -1;
        }
    }
}
