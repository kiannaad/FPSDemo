using System;
using System.Collections;
using System.Linq;
using CGame.Network;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CGame.GameplayCue.PlayModeTests
{
    public sealed class SampleSceneEnemyCombatInteractionPlayModeTests
    {
        private Keyboard keyboard;
        private Mouse mouse;
        private GameInstance instance;
        private UnityEditor.EditorWindow window;
        private OwnerGameplayStateEvent latestVitals;
        private PlayerController controller;
        private Pawn pawn;
        private Coroutine cueObserver;
        private EnemyPresentation[] enemies;
        private float[] distances;
        private System.Collections.Generic.HashSet<string> muzzleEffects;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            if (World.Current != null)
            {
                var shutdown = World.Current.ShutdownAsync();
                yield return WaitFor(() => shutdown.IsCompleted, 10f, "Previous World shutdown");
                if (shutdown.IsFaulted) throw shutdown.Exception.GetBaseException();
            }
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            latestVitals = null;
            var load = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/SampleScene.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return WaitFor(() => load.isDone, 15f, "SampleScene load");
            instance = UnityEngine.Object.FindObjectOfType<GameInstance>();
            window = ScriptableObject.CreateInstance(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")) as UnityEditor.EditorWindow;
            window.titleContent = new GUIContent("Enemy AI Verification");
            window.position = new Rect(100, 100, 1280, 760);
            window.ShowUtility();
            window.Focus();
            Debug.Log("[Sample069] VisualReady: formal SampleScene combat interaction");
            yield return WaitFor(() => instance.RuntimeWorld?.GameMode is INetworkLobby, 15f, "Network game mode");
            var lobby = (INetworkLobby)instance.RuntimeWorld.GameMode;
            var network = instance.RuntimeWorld.GetSubSystem<ClientNetworkSubSystem>();
            network.OwnerGameplayStateReceived += state => latestVitals = state;
            yield return WaitFor(() => lobby.NetworkStatus == "Connected", 15f, "SampleScene ServerHost handshake");
            window.Focus();
            yield return WaitFor(() => Application.isFocused, 10f, "Lobby input focus");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.C));
            yield return new WaitForSeconds(.15f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return WaitFor(() => !string.IsNullOrEmpty(lobby.RoomId), 15f, "C input creates the room");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
            yield return new WaitForSeconds(.15f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return WaitFor(() => instance.RuntimeWorld.IsGameplayReady, 40f, "R input starts Dedicated and reaches GameplayReady");
            yield return WaitFor(() => UnityEngine.Object.FindObjectsOfType<EnemyPresentation>().Length == 3, 10f, "Three authoritative enemies");
            enemies = UnityEngine.Object.FindObjectsOfType<EnemyPresentation>();
            var starts = enemies.Select(enemy => enemy.transform.position).ToArray();
            distances = new float[enemies.Length];
            muzzleEffects = new System.Collections.Generic.HashSet<string>();
            cueObserver = instance.StartCoroutine(ObserveEnemyActivity(enemies, muzzleEffects, starts, distances));
            Assert.That(CGame.Ability.Cues.GameplayCueRouter.Current, Is.Not.Null, "The formal World must own its Cue router.");
            Assert.That(CGame.GameplayTags.GameplayTagManager.Instance.TryRequestTag("GameplayCue.Enemy.Fire", out _),
                Is.True, "The formal Bootstrap must register the enemy Fire Cue tag, not silently skip real fire effects.");
            Assert.That(CGame.GameplayTags.GameplayTagManager.Instance.TryRequestTag("GameplayCue.Enemy.Impact", out _),
                Is.True, "The formal Bootstrap must register the enemy Hit Cue tag.");
            controller = (PlayerController)instance.RuntimeWorld.GameMode.PlayerController;
            pawn = controller.PossessedPawn;
            window.Focus();
            yield return WaitFor(() => Application.isFocused, 10f, "Production input focus");
            var equipment = pawn.GetComponent<EquipmentManagerComponent>();
            yield return WaitFor(() => equipment.CurrentWeapon != null && equipment.CurrentWeapon.IsArmed &&
                !equipment.IsSwitchInProgress, 10f, "Initial equipment ready");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Digit2));
            yield return new WaitForSeconds(0.15f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return WaitFor(() => equipment.CurrentWeapon.ItemHandle == controller.QuickBar.Slots[1] &&
                equipment.CurrentWeapon.IsArmed && !equipment.IsSwitchInProgress, 10f, "Formal rifle selection");
        }

        private IEnumerator ObserveCombat()
        {
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right));
            yield return WaitFor(() => pawn.IsAiming, 3f, "Complete mouse state activates formal right-button Aim");
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return WaitFor(() => !pawn.IsAiming, 3f, "Releasing right-button Aim restores normal view");
            // Retain the observation interval before the right-side approach:
            // its timing allows all three patrols to enter engagement range.
            yield return new WaitForSeconds(6f);
            for (int index = 0; index < enemies.Length; index++)
                Assert.That(distances[index], Is.GreaterThan(1f), enemies[index].name + " must traverse the mainline patrol area.");
            // The replicated arena has a central plinth ahead. Use the open right
            // approach so setup does not pin the owner in three crossfire lanes.
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
            yield return new WaitForSeconds(2.5f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return new WaitForSeconds(.3f);
            yield return WaitFor(() =>
            {
                foreach (var enemy in enemies)
                    if (enemy.GetComponentsInChildren<Transform>().Any(value => value.name == "flash(Clone)"))
                        muzzleEffects.Add(enemy.name);
                return enemies.All(enemy => enemy.LastConfirmedAction == EnemyActionKind.Fire) && muzzleEffects.Count == 3;
            }, 20f, "All three archetypes must fire through the real Cue route and spawn their muzzle effect");
        }

        private static IEnumerator ObserveEnemyActivity(EnemyPresentation[] enemies,
            System.Collections.Generic.HashSet<string> effects, Vector3[] starts, float[] distances)
        {
            // Start at spawn: by the time weapon/ADS setup finishes, an enemy
            // can already have completed its approach and be correctly holding cover.
            while (true)
            {
                for (int index = 0; index < enemies.Length; index++)
                {
                    var enemy = enemies[index];
                    if (enemy == null) continue;
                    distances[index] = Mathf.Max(distances[index], Vector3.Distance(starts[index], enemy.transform.position));
                    if (enemy.GetComponentsInChildren<Transform>().Any(value => value.name == "flash(Clone)"))
                        effects.Add(enemy.name);
                }
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator EnemyFire_ReachesOwnerDeathAndDisablesFormalGameplayInput()
        {
            yield return ObserveCombat();
            yield return WaitFor(() => latestVitals != null && latestVitals.Health < latestVitals.MaxHealth, 30f, "Enemy damage to owner");
            Assert.That(pawn.Root.GetComponent<OwnerGameplayHud>().Text, Does.Contain("HP "));
            yield return WaitFor(() => latestVitals.IsDead && latestVitals.Health == 0, 50f, "Authority owner death");
            yield return null;
            Assert.That(pawn.GetComponent<PawnHeroComponent>().IsBound, Is.False, "Dead owner must stop the formal InputTag callbacks.");
            Vector3 position = pawn.Transform.position;
            var input = instance.RuntimeWorld.LocalPlayer.GetSubSystem<InputSubSystem>();
            var motor = pawn.GetComponent<PawnMovementComponent>().Motor;
            var mode = (NetworkGameMode)instance.RuntimeWorld.GameMode;
            Assert.That(mode.LastOwnerAuditSnapshot.HasValue, Is.True);
            Vector3 authorityPosition = mode.LastOwnerAuditSnapshot.Value.State.Position.ToMeters();
            Debug.Log($"[Sample069] DeathStart Position={position:F4} Motor={motor.TransientPosition:F4} Velocity={motor.BaseVelocity:F4} InputEnabled={input.GameplayInputEnabled}");
            float yaw = controller.ControlYaw;
            var weapon = pawn.GetComponent<EquipmentManagerComponent>().CurrentWeapon;
            int ammo = weapon.Item.MagazineAmmo;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.R));
            InputSystem.QueueDeltaStateEvent(mouse.leftButton, (byte)1);
            InputSystem.QueueDeltaStateEvent(mouse.delta, new Vector2(30f, 0f));
            yield return new WaitForSeconds(1f);
            Debug.Log($"[Sample069] DeathInputProbe Position={pawn.Transform.position:F4} Motor={motor.TransientPosition:F4} Velocity={motor.BaseVelocity:F4} PawnInput={pawn.PeekingMovementInput():F4} SourceInput={input.ReadControlIntent().MovementInput:F4}");
            // Presentation colliders can depenetrate a stationary body when
            // surviving enemies return past it. Verify control and authority,
            // not the stronger (and incorrect) assumption of a frozen corpse.
            Assert.That(input.GameplayInputEnabled, Is.False);
            Assert.That(input.ReadControlIntent().MovementInput, Is.EqualTo(Vector3.zero));
            Assert.That(pawn.PeekingMovementInput(), Is.EqualTo(Vector3.zero));
            Assert.That(motor.BaseVelocity.sqrMagnitude, Is.LessThan(0.0001f));
            Assert.That(Vector3.Distance(authorityPosition, mode.LastOwnerAuditSnapshot.Value.State.Position.ToMeters()), Is.LessThan(0.1f));
            Assert.That(controller.ControlYaw, Is.EqualTo(yaw).Within(0.1f));
            Assert.That(weapon.Item.MagazineAmmo, Is.EqualTo(ammo));
            Assert.That(UnityEngine.Object.FindObjectsOfType<TMPro.TextMeshProUGUI>().Any(label => label.text.Contains("YOU DIED")), Is.True);
            yield return new WaitForSeconds(2f);
            var world = instance.RuntimeWorld;
            pawn.Root.GetComponent<OwnerGameplayTerminalPresenter>().RequestExit();
            yield return WaitFor(() => world.State == WorldState.Destroyed, 10f, "Death result exits the real World safely");
            Assert.That(world.IsGameplayReady, Is.False);
        }

        [UnityTest]
        public IEnumerator PlayerFire_ProducesAuthoritativeEnemyHitDeathAndRetirement()
        {
            yield return ObserveCombat();
            var enemies = UnityEngine.Object.FindObjectsOfType<EnemyPresentation>();
            var camera = pawn.GetComponent<PawnCameraComponent>().Camera;
            EnemyPresentation target = null;
            yield return WaitFor(() =>
            {
                target = enemies.Where(enemy => enemy != null && !enemy.IsDead && HasClearAim(camera, enemy))
                    .OrderBy(enemy => Vector3.Distance(pawn.Transform.position, enemy.transform.position)).FirstOrDefault();
                return target != null;
            }, 15f, "An exposed authoritative enemy");
            bool sawHit = false;
            bool sawImpactEffect = false;
            float deadline = Time.realtimeSinceStartup + 18f;
            float nextShot = 0f;
            float releaseAt = 0f;
            while (target != null && !target.IsDead && Time.realtimeSinceStartup < deadline)
            {
                sawHit |= target.LastConfirmedAction == EnemyActionKind.Hit;
                sawImpactEffect |= target.transform.Find("hitEffect(Clone)") != null;
                Vector3 aim = target.transform.position + Vector3.up * 1.3f - camera.transform.position;
                float yaw = Mathf.Atan2(aim.x, aim.z) * Mathf.Rad2Deg;
                float pitch = -Mathf.Atan2(aim.y, new Vector2(aim.x, aim.z).magnitude) * Mathf.Rad2Deg;
                float yawDelta = Mathf.DeltaAngle(controller.ControlYaw, yaw);
                float pitchDelta = controller.ControlPitch - pitch;
                InputSystem.QueueDeltaStateEvent(mouse.delta, new Vector2(Mathf.Clamp(yawDelta, -3f, 3f),
                    Mathf.Clamp(pitchDelta, -3f, 3f)));
                float now = Time.realtimeSinceStartup;
                if (releaseAt > 0f && now >= releaseAt)
                {
                    InputSystem.QueueDeltaStateEvent(mouse.leftButton, (byte)0);
                    releaseAt = 0f;
                }
                if (now >= nextShot && Mathf.Abs(yawDelta) < 1f && Mathf.Abs(pitchDelta) < 1f && HasClearAim(camera, target))
                {
                    InputSystem.QueueDeltaStateEvent(mouse.leftButton, (byte)1);
                    releaseAt = now + .08f;
                    nextShot = now + .6f;
                }
                yield return null;
            }
            InputSystem.QueueDeltaStateEvent(mouse.leftButton, (byte)0);
            Assert.That(sawHit, Is.True, "InputTag -> Ability -> server must confirm Hit.");
            Assert.That(sawImpactEffect, Is.True, "Confirmed enemy Hit must execute the real impact Cue.");
            Assert.That(target != null && target.IsDead, Is.True, "Server must confirm lethal player shots.");
            Vector3 deathRoot = target.transform.position;
            yield return new WaitForSeconds(1f);
            Assert.That(target, Is.Not.Null, "Death must remain visible before retirement.");
            Assert.That(Vector3.Distance(deathRoot, target.transform.position), Is.LessThan(.01f));
            float lowest = float.PositiveInfinity;
            foreach (var renderer in target.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var mesh = new Mesh();
                renderer.BakeMesh(mesh);
                foreach (Vector3 vertex in mesh.vertices)
                    lowest = Mathf.Min(lowest, renderer.transform.TransformPoint(vertex).y);
                UnityEngine.Object.Destroy(mesh);
            }
            Debug.Log($"[Sample069] DeathGround RootY={deathRoot.y:F4} LowestVertexY={lowest:F4}");
            Assert.That(lowest, Is.EqualTo(deathRoot.y).Within(.03f), "The real posed corpse must contact the ground without penetration.");
            Assert.That(Quaternion.Angle(Quaternion.identity, target.VisualRoot.localRotation), Is.GreaterThan(60f));
            yield return WaitFor(() => target == null, 4f, "Enemy death presentation retirement");
        }

        [UnityTest]
        public IEnumerator ReloadInput_ReplenishesEquippedWeaponWithoutSendingLobbyReady()
        {
            // Validate reload while alive, independently of the lethal crossfire
            // and corpse-observation scenario whose owner may legitimately die.
            var lobby = (INetworkLobby)instance.RuntimeWorld.GameMode;
            string lobbyStatus = lobby.NetworkStatus;
            var weapon = pawn.GetComponent<EquipmentManagerComponent>().CurrentWeapon;
            int initialAmmo = weapon.Item.MagazineAmmo;
            InputSystem.QueueDeltaStateEvent(mouse.leftButton, (byte)1);
            yield return new WaitForSeconds(.15f);
            InputSystem.QueueDeltaStateEvent(mouse.leftButton, (byte)0);
            yield return WaitFor(() => weapon.Item.MagazineAmmo < initialAmmo, 5f,
                "A real shot creates reloadable space in the magazine");
            int ammoBeforeReload = weapon.Item.MagazineAmmo;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
            yield return new WaitForSeconds(.15f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return WaitFor(() => weapon.Item.MagazineAmmo > ammoBeforeReload, 8f,
                "R input reloads the actual equipped weapon");
            Assert.That(lobby.NetworkStatus, Is.EqualTo(lobbyStatus),
                "Combat Reload must not send a new Lobby Ready request.");
        }

        private bool HasClearAim(Camera camera, EnemyPresentation enemy)
        {
            Vector3 aim = enemy.transform.position + Vector3.up * 1.3f - camera.transform.position;
            return !Physics.RaycastAll(camera.transform.position, aim.normalized, aim.magnitude - .2f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore).Any(hit =>
                !hit.transform.IsChildOf(pawn.Transform) && !hit.transform.IsChildOf(enemy.transform));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (cueObserver != null && instance != null) instance.StopCoroutine(cueObserver);
            if (keyboard?.added == true) InputSystem.RemoveDevice(keyboard);
            if (mouse?.added == true) InputSystem.RemoveDevice(mouse);
            if (instance != null) instance.ShutdownRuntimeWorld();
            if (window != null) window.Close();
            yield return null;
        }

        private static IEnumerator WaitFor(Func<bool> condition, float seconds, string description)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, description);
        }
    }
}
