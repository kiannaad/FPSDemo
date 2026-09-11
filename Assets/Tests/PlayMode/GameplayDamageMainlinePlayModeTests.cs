using System.Collections;
using System.Collections.Generic;
using System.IO;
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
    public sealed class GameplayDamageMainlinePlayModeTests
    {
        private static readonly string runId = "GameplayDamageOffline-" + System.DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");

        [UnityTest]
        public IEnumerator OfflineInput_WallThenRepeatedEnemyHits_CompletesDeathCleanupAndVisualClosure()
        {
            using var offlineFixture = new OfflineSampleSceneFixture();
            if (World.Current != null)
            {
                var shutdown = World.Current.ShutdownAsync();
                while (!shutdown.IsCompleted) yield return null;
            }

            Mouse mouse = InputSystem.AddDevice<Mouse>();
            GameInstance gameInstance = null;
            GameObject acceptanceWall = null;
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
                     frame < 300 && (equipment.IsSwitchInProgress || equipment.CurrentWeapon == null ||
                                     !equipment.CurrentWeapon.IsArmed);
                     frame++) yield return null;

                int rifleSlot = FindDamageWeaponSlot(controller);
                Assert.That(rifleSlot, Is.GreaterThanOrEqualTo(0));
                Assert.That(controller.TryRequestQuickBarSlot(rifleSlot), Is.True);
                for (int frame = 0;
                     frame < 300 && (equipment.CurrentWeapon == null ||
                                     equipment.CurrentWeapon.ItemHandle != controller.QuickBar.Slots[rifleSlot] ||
                                     !equipment.CurrentWeapon.IsArmed || equipment.IsSwitchInProgress);
                     frame++) yield return null;

                WeaponInstance weapon = equipment.CurrentWeapon;
                Assert.That(weapon, Is.Not.Null);
                Assert.That(weapon.IsArmed, Is.True);
                Assert.That(equipment.IsSwitchInProgress, Is.False);
                Assert.That(weapon.Definition.DamageEffect, Is.Not.Null);
                Camera camera = pawn.GetComponent<PawnCameraComponent>().Camera;
                EnemySpawnHandle target = enemies.Handles[0];
                HealthSet[] healthSets = new HealthSet[3];
                for (int index = 0; index < enemies.Handles.Count; index++)
                {
                    healthSets[index] = enemies.Handles[index].Controller.PlayerState.AbilitySystem.GetSet<HealthSet>();
                    Assert.That(healthSets[index].Health.CurrentValue, Is.EqualTo(60f));
                }

                ArrangeEnemiesForEvidence(enemies, camera, 2f);
                Physics.SyncTransforms();
                yield return null;
                string runDirectory = GetRunDirectory();
                Directory.CreateDirectory(runDirectory);
                string beforePath = Path.Combine(runDirectory, "before-three-enemies.png");
                string afterPath = Path.Combine(runDirectory, "after-two-enemies.png");

                var cueTrace = new List<string>();
                GameplayCueManager cueManager = world.GetSubSystem<GameplayCueManager>();
                cueManager.RegisterRoute(GameplayTagManager.Instance.RequestTag("GameplayCue.Weapon.Fire"),
                    (_, _, _) => cueTrace.Add("Fire"));
                cueManager.RegisterRoute(GameplayTagManager.Instance.RequestTag("GameplayCue.Weapon.Impact"),
                    (_, _, _) => cueTrace.Add("Impact"));
                cueManager.RegisterRoute(GameplayTagManager.Instance.RequestTag("GameplayCue.Weapon.DamageTaken"),
                    (_, _, _) => cueTrace.Add("DamageTaken"));

                acceptanceWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                acceptanceWall.name = "DamageMainlineAcceptanceWall";
                acceptanceWall.transform.position = camera.transform.position + camera.transform.forward * 4f + Vector3.up * 3f;
                acceptanceWall.transform.localScale = new Vector3(4f, 4f, 0.4f);
                Physics.SyncTransforms();
                Collider wallCollider = acceptanceWall.GetComponent<Collider>();
                float[] healthBeforeWall = { healthSets[0].Health.CurrentValue, healthSets[1].Health.CurrentValue, healthSets[2].Health.CurrentValue };
                int wallTraceStart = cueTrace.Count;
                yield return FireOnce(mouse, pawn, camera, weapon, wallCollider.bounds.center);
                Assert.That(cueTrace.Count - wallTraceStart, Is.GreaterThanOrEqualTo(2),
                    $"Wall cue trace was [{string.Join(",", cueTrace)}].");
                CollectionAssert.AreEqual(new[] { "Fire", "Impact" }, cueTrace.GetRange(wallTraceStart, 2));
                Assert.That(cueTrace.Count, Is.EqualTo(wallTraceStart + 2), "Wall hit must not emit DamageTaken.");
                Assert.That(GameObject.Find("GameplayCueBulletHole"), Is.Not.Null);
                for (int index = 0; index < healthSets.Length; index++)
                    Assert.That(healthSets[index].Health.CurrentValue, Is.EqualTo(healthBeforeWall[index]));
                Object.Destroy(acceptanceWall);
                acceptanceWall = null;
                yield return null;
                ArrangeEnemiesForEvidence(enemies, camera, 3f);
                Physics.SyncTransforms();
                yield return null;

                ScreenCapture.CaptureScreenshot(beforePath, 2);
                yield return new WaitForEndOfFrame();
                yield return new WaitForSeconds(0.2f);
                Assert.That(File.Exists(beforePath), Is.True, beforePath);
                ArrangeEnemiesForEvidence(enemies, camera, 0.8f);
                Physics.SyncTransforms();
                yield return null;

                HealthComponent health = target.Pawn.GetComponent<HealthComponent>();
                Collider targetCollider = target.Pawn.Root.GetComponentInChildren<Collider>(true);
                Vector3 targetPositionAfterDeath = targetCollider.bounds.center;
                GameObject targetRoot = target.Pawn.Root;
                ActorRegistration pawnRegistration = target.PawnRegistration;
                int actorsBeforeDeath = world.RegisteredActorCount;
                int deathStartedCount = 0;
                bool rootInactiveAtDeath = false;
                health.DeathStarted += () =>
                {
                    deathStartedCount++;
                    rootInactiveAtDeath = !targetRoot.activeSelf;
                };

                int ammoBeforeDamage = weapon.Item.MagazineAmmo;
                for (int shot = 1; shot <= 3; shot++)
                {
                    int traceStart = cueTrace.Count;
                    yield return FireOnce(mouse, pawn, camera, weapon, targetCollider.bounds.center);
                    Assert.That(cueTrace.Count - traceStart, Is.GreaterThanOrEqualTo(3),
                        $"Enemy shot {shot} cue trace was [{string.Join(",", cueTrace)}].");
                    CollectionAssert.AreEqual(
                        new[] { "Fire", "Impact", "DamageTaken" },
                        cueTrace.GetRange(traceStart, 3));
                    Assert.That(healthSets[0].Health.CurrentValue, Is.EqualTo(60f - shot * 20f));
                    Assert.That(healthSets[0].Damage.CurrentValue, Is.Zero);
                }

                Assert.That(weapon.Item.MagazineAmmo, Is.EqualTo(ammoBeforeDamage - 3));
                Assert.That(deathStartedCount, Is.EqualTo(1));
                Assert.That(rootInactiveAtDeath, Is.True);
                Assert.That(target.IsDisposed, Is.True);
                Assert.That(pawnRegistration.IsDisposed, Is.True);
                Assert.That(world.RegisteredActorCount, Is.EqualTo(actorsBeforeDeath - 2));
                Assert.That(world.LevelRuntime.GetStatus(target.PointId).State, Is.EqualTo(SpawnPointState.Available));
                Assert.That(enemies.Handles.Count, Is.EqualTo(2));
                Assert.That(enemies.Handles, Has.No.Member(target));
                foreach (EnemySpawnHandle survivor in enemies.Handles)
                    Assert.That(survivor.Pawn.Root.activeSelf, Is.True);

                float secondHealth = healthSets[1].Health.CurrentValue;
                float thirdHealth = healthSets[2].Health.CurrentValue;
                int postDeathTraceStart = cueTrace.Count;
                yield return FireOnce(mouse, pawn, camera, weapon, targetPositionAfterDeath);
                Assert.That(healthSets[1].Health.CurrentValue, Is.EqualTo(secondHealth));
                Assert.That(healthSets[2].Health.CurrentValue, Is.EqualTo(thirdHealth));
                Assert.That(cueTrace.GetRange(postDeathTraceStart, cueTrace.Count - postDeathTraceStart),
                    Does.Not.Contain("DamageTaken"));

                ArrangeSurvivorsForEvidence(enemies, camera);
                Physics.SyncTransforms();
                yield return null;
                ScreenCapture.CaptureScreenshot(afterPath, 2);
                yield return new WaitForEndOfFrame();
                yield return new WaitForSeconds(0.2f);
                Assert.That(File.Exists(afterPath), Is.True, afterPath);
            }
            finally
            {
                if (acceptanceWall != null) Object.Destroy(acceptanceWall);
                if (mouse.added) InputSystem.RemoveDevice(mouse);
                if (gameInstance != null) Object.Destroy(gameInstance.gameObject);
            }

            yield return null;
        }

        private static IEnumerator FireOnce(
            Mouse mouse,
            Pawn pawn,
            Camera camera,
            WeaponInstance weapon,
            Vector3 targetPosition)
        {
            var controller = (PlayerController)World.Current.GameMode.PlayerController;
            float aimDeadline = Time.realtimeSinceStartup + 5f;
            float aimError;
            do
            {
                Vector3 aim = targetPosition - camera.transform.position;
                float yaw = Mathf.Atan2(aim.x, aim.z) * Mathf.Rad2Deg;
                float pitch = -Mathf.Atan2(aim.y, new Vector2(aim.x, aim.z).magnitude) * Mathf.Rad2Deg;
                float yawDelta = Mathf.DeltaAngle(controller.ControlYaw, yaw);
                float pitchDelta = controller.ControlPitch - pitch;
                aimError = Mathf.Max(Mathf.Abs(yawDelta), Mathf.Abs(pitchDelta));
                InputSystem.QueueDeltaStateEvent(mouse.delta, new Vector2(
                    Mathf.Clamp(yawDelta, -3f, 3f), Mathf.Clamp(pitchDelta, -3f, 3f)));
                yield return null;
            } while (aimError > .5f && Time.realtimeSinceStartup < aimDeadline);
            Assert.That(aimError, Is.LessThanOrEqualTo(.5f), "Formal mouse input must aim before firing.");
            int ammoBefore = weapon.Item.MagazineAmmo;
            for (int attempt = 0; attempt < 3 && weapon.Item.MagazineAmmo == ammoBefore; attempt++)
            {
                InputSystem.QueueStateEvent(mouse, new MouseState());
                InputSystem.Update();
                yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
                InputSystem.Update();
                yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState());
                InputSystem.Update();
                yield return new WaitForSeconds(0.15f);
            }
            Assert.That(weapon.Item.MagazineAmmo, Is.EqualTo(ammoBefore - 1),
                "QueueStateEvent fire must commit exactly one round.");
        }

        private static void ArrangeEnemiesForEvidence(
            EnemySpawnGameComponent enemies,
            Camera camera,
            float targetDistance)
        {
            Vector3 forward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            for (int index = 0; index < enemies.Handles.Count; index++)
            {
                float horizontalOffset = index == 1 ? -1.5f : index == 2 ? 1.5f : 0f;
                float distance = index == 0 ? targetDistance : 3.5f;
                Vector3 position = camera.transform.position + forward * distance + right * horizontalOffset;
                position.y = enemies.Handles[index].Pawn.Transform.position.y;
                enemies.Handles[index].Pawn.GetComponent<PawnMovementComponent>().Motor.SetPosition(position);
            }
        }

        private static void ArrangeSurvivorsForEvidence(EnemySpawnGameComponent enemies, Camera camera)
        {
            Vector3 forward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            for (int index = 0; index < enemies.Handles.Count; index++)
            {
                float horizontalOffset = index == 0 ? -1.25f : 1.25f;
                Vector3 position = camera.transform.position + forward * 3.5f + right * horizontalOffset;
                position.y = enemies.Handles[index].Pawn.Transform.position.y;
                enemies.Handles[index].Pawn.GetComponent<PawnMovementComponent>().Motor.SetPosition(position);
            }
        }

        private static string GetRunDirectory()
        {
            DirectoryInfo repository = Directory.GetParent(Application.dataPath);
            return Path.Combine(repository.Parent.FullName, ".harness", "runs", runId);
        }

        private static int FindDamageWeaponSlot(PlayerController controller)
        {
            for (int index = 0; index < controller.QuickBar.Slots.Count; index++)
            {
                if (!controller.Inventory.TryGet(controller.QuickBar.Slots[index], out ItemInstance item) ||
                    !(item.Definition is WeaponItemDefinition weaponItem) ||
                    weaponItem.WeaponDefinition == null || weaponItem.WeaponDefinition.DamageEffect == null)
                    continue;
                return index;
            }
            return -1;
        }
    }
}
