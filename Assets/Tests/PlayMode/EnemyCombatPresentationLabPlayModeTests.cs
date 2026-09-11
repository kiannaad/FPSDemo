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
    public sealed class EnemyCombatPresentationLabPlayModeTests
    {
        [UnityTest]
        public IEnumerator SinglePlayerReady_SpawnsThreePatrollingEnemiesAndAcceptsMovementInput()
        {
            // Requires the real Harness ServerHost running EnemyCombatPresentationLab.
            // No fake snapshots, enemy transforms, or direct motor commands are used.
            if (World.Current != null)
            {
                var shutdown = World.Current.ShutdownAsync();
                yield return WaitFor(() => shutdown.IsCompleted, 10f, "Previous world shutdown");
                if (shutdown.IsFaulted) throw shutdown.Exception.GetBaseException();
            }
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var mouse = InputSystem.AddDevice<Mouse>();
            GameInstance instance = null;
            UnityEditor.EditorWindow gameWindow = null;
            try
            {
                var load = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                    "Assets/Scenes/EnemyCombatPresentationLab.unity", new LoadSceneParameters(LoadSceneMode.Single));
                yield return WaitFor(() => load.isDone, 15f, "Lab scene load");
                instance = UnityEngine.Object.FindObjectOfType<GameInstance>();
                Assert.That(instance, Is.Not.Null);
                var gameViewType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
                gameWindow = ScriptableObject.CreateInstance(gameViewType) as UnityEditor.EditorWindow;
                gameWindow.titleContent = new GUIContent("Enemy AI Verification");
                gameWindow.position = new Rect(100, 100, 1280, 760);
                gameWindow.ShowUtility();
                gameWindow.Focus();
                Debug.Log("[Lab068] VisualReady: real lobby, patrol, combat and death capture.");
                yield return WaitFor(() => instance.RuntimeWorld?.GameMode is INetworkLobby,
                    15f, "Lab network game mode");
                var lobby = (INetworkLobby)instance.RuntimeWorld.GameMode;
                yield return WaitFor(() => lobby.NetworkStatus == "Connected", 15f,
                    "ServerHost handshake (start Harness server with Lab LevelId)");
                var create = lobby.CreateRoomAsync();
                yield return WaitFor(() => create.IsCompleted, 15f, "Create single-player room");
                if (create.IsFaulted) throw create.Exception.GetBaseException();
                var ready = lobby.SetReadyAsync(true);
                yield return WaitFor(() => ready.IsCompleted, 30f, "Ready and Dedicated startup");
                if (ready.IsFaulted) throw ready.Exception.GetBaseException();
                yield return WaitFor(() => instance.RuntimeWorld.IsGameplayReady, 20f, "GameplayReady");
                yield return WaitFor(() => UnityEngine.Object.FindObjectsOfType<EnemyPresentation>().Length == 3,
                    10f, "Three authoritative enemy spawns");
                var enemies = UnityEngine.Object.FindObjectsOfType<EnemyPresentation>();
                foreach (string variant in new[] { "Pistol", "Rifle", "Ak" })
                    Assert.That(enemies.Count(enemy => enemy.name.StartsWith("NetworkEnemy" + variant)), Is.EqualTo(1));
                var starts = enemies.Select(enemy => enemy.transform.position).ToArray();
                var displacement = new float[enemies.Length];
                float deadline = Time.realtimeSinceStartup + 6f;
                while (Time.realtimeSinceStartup < deadline)
                {
                    for (int index = 0; index < enemies.Length; index++)
                        displacement[index] = Mathf.Max(displacement[index], Vector3.Distance(starts[index], enemies[index].transform.position));
                    yield return null;
                }
                for (int index = 0; index < enemies.Length; index++)
                {
                    Assert.That(displacement[index], Is.GreaterThan(1f), enemies[index].name + " must patrol in world space.");
                    Assert.That(enemies[index].HasPlayableGraph, Is.True);
                }
                Pawn pawn = ((PlayerController)instance.RuntimeWorld.GameMode.PlayerController).PossessedPawn;
                Vector3 playerStart = pawn.Transform.position;
                gameWindow.Focus();
                yield return WaitFor(() => Application.isFocused, 10f,
                    "Focused Game window required by the production input gate");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                yield return new WaitForSeconds(1f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                Assert.That(Vector3.Distance(playerStart, pawn.Transform.position), Is.GreaterThan(0.5f));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                yield return new WaitForSeconds(4f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return WaitFor(() => enemies.Any(enemy => enemy.LastConfirmedAction == EnemyActionKind.Fire),
                    15f, "Authority-confirmed enemy fire");
                yield return new WaitForSeconds(2f);
                Assert.That(enemies.Any(enemy => enemy.LastConfirmedAction == EnemyActionKind.Hit), Is.False,
                    "The player never fired: hitting the player must not make the shooter flinch.");

                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Digit2));
                yield return new WaitForSeconds(0.15f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return new WaitForSeconds(3f);
                var controller = (PlayerController)instance.RuntimeWorld.GameMode.PlayerController;
                // Walk around the central block through the open aisle. Merely
                // walking into its front leaves Rifle occluded throughout combat.
                yield return MoveThroughInput(keyboard, mouse, controller, pawn, new Vector3(3.5f, 0f, 0f));
                yield return MoveThroughInput(keyboard, mouse, controller, pawn, new Vector3(3.5f, 0f, 4.5f));
                yield return WaitFor(() => enemies.All(enemy => enemy.LastConfirmedAction == EnemyActionKind.Fire),
                    10f, "All three archetypes must confirm fire before the player shoots");
                var camera = Camera.allCameras.OrderByDescending(value => value.depth).First();
                EnemyPresentation target = null;
                yield return WaitFor(() =>
                {
                    target = enemies.Where(enemy => enemy != null && !enemy.IsDead &&
                        HasClearAim(camera, pawn, enemy)).OrderBy(enemy =>
                        Vector3.Distance(pawn.Transform.position, enemy.transform.position)).FirstOrDefault();
                    return target != null;
                }, 10f, "An exposed enemy to aim at");
                bool sawHit = false;
                float fireDeadline = Time.realtimeSinceStartup + 12f;
                float nextShot = 0f;
                float releaseAt = 0f;
                while (target != null && !target.IsDead && Time.realtimeSinceStartup < fireDeadline)
                {
                    sawHit |= target.LastConfirmedAction == EnemyActionKind.Hit;
                    Vector3 aim = target.transform.position + Vector3.up * 1.3f - camera.transform.position;
                    float yaw = Mathf.Atan2(aim.x, aim.z) * Mathf.Rad2Deg;
                    float pitch = -Mathf.Atan2(aim.y, new Vector2(aim.x, aim.z).magnitude) * Mathf.Rad2Deg;
                    float yawDelta = Mathf.DeltaAngle(controller.ControlYaw, yaw);
                    float pitchDelta = controller.ControlPitch - pitch;
                    InputSystem.QueueDeltaStateEvent(mouse.delta, new Vector2(
                        Mathf.Clamp(yawDelta, -3f, 3f), Mathf.Clamp(pitchDelta, -3f, 3f)));
                    float now = Time.realtimeSinceStartup;
                    if (releaseAt > 0f && now >= releaseAt)
                    {
                        InputSystem.QueueDeltaStateEvent(mouse.leftButton, (byte)0);
                        releaseAt = 0f;
                    }
                    if (now >= nextShot && Mathf.Abs(yawDelta) < 1f && Mathf.Abs(pitchDelta) < 1f &&
                        HasClearAim(camera, pawn, target))
                    {
                        InputSystem.QueueDeltaStateEvent(mouse.leftButton, (byte)1);
                        releaseAt = now + 0.08f;
                        nextShot = now + 0.6f;
                    }
                    yield return null;
                }
                InputSystem.QueueDeltaStateEvent(mouse.leftButton, (byte)0);
                Assert.That(sawHit, Is.True, "Real player shots must produce an enemy Hit confirmation.");
                Assert.That(target != null && target.IsDead, Is.True, "Real player shots must reach the enemy death terminal.");
                Vector3 deathRoot = target.transform.position;
                float deathObservationUntil = Time.realtimeSinceStartup + 1f;
                while (Time.realtimeSinceStartup < deathObservationUntil)
                {
                    // Follow the falling torso with mouse input, keeping it above
                    // the first-person weapon instead of hidden at the screen edge.
                    Vector3 aim = target.VisualRoot.position + target.VisualRoot.up * 0.9f - camera.transform.position;
                    float yaw = Mathf.Atan2(aim.x, aim.z) * Mathf.Rad2Deg;
                    float pitch = -Mathf.Atan2(aim.y, new Vector2(aim.x, aim.z).magnitude) * Mathf.Rad2Deg;
                    InputSystem.QueueDeltaStateEvent(mouse.delta, new Vector2(
                        Mathf.Clamp(Mathf.DeltaAngle(controller.ControlYaw, yaw), -2f, 2f),
                        Mathf.Clamp(controller.ControlPitch - pitch, -2f, 2f)));
                    yield return null;
                }
                Assert.That(target, Is.Not.Null, "Death must remain visible before retirement.");
                Assert.That(Vector3.Distance(deathRoot, target.transform.position), Is.LessThan(0.01f));
                Assert.That(Quaternion.Angle(Quaternion.identity, target.VisualRoot.localRotation), Is.GreaterThan(60f));
                yield return WaitFor(() => target == null, 4f, "Death presentation retirement");
                foreach (var decal in UnityEngine.Object.FindObjectsOfType<CGame.Ability.Cues.BulletHoleLifetime>())
                    Assert.That(Physics.CheckSphere(decal.transform.position, 0.05f,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore), Is.True,
                        "A persistent environment decal must remain on geometry, not float at a retired enemy's hit point.");
                var survivor = enemies.FirstOrDefault(enemy => enemy != null && !enemy.IsDead);
                float observeUntil = Time.realtimeSinceStartup + 7f;
                while (survivor != null && Time.realtimeSinceStartup < observeUntil)
                {
                    Vector3 aim = survivor.transform.position + Vector3.up * 1.3f - camera.transform.position;
                    float yaw = Mathf.Atan2(aim.x, aim.z) * Mathf.Rad2Deg;
                    float pitch = -Mathf.Atan2(aim.y, new Vector2(aim.x, aim.z).magnitude) * Mathf.Rad2Deg;
                    InputSystem.QueueDeltaStateEvent(mouse.delta, new Vector2(
                        Mathf.Clamp(Mathf.DeltaAngle(controller.ControlYaw, yaw), -1f, 1f),
                        Mathf.Clamp(controller.ControlPitch - pitch, -1f, 1f)));
                    yield return null;
                }
            }
            finally
            {
                if (keyboard.added) InputSystem.RemoveDevice(keyboard);
                if (mouse.added) InputSystem.RemoveDevice(mouse);
                if (instance != null) instance.ShutdownRuntimeWorld();
                if (gameWindow != null) gameWindow.Close();
            }
        }

        private static IEnumerator MoveThroughInput(Keyboard keyboard, Mouse mouse, PlayerController controller,
            Pawn pawn, Vector3 destination)
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            Vector3 delta = destination - pawn.Transform.position;
            delta.y = 0f;
            while (delta.magnitude > 0.6f && Time.realtimeSinceStartup < deadline)
            {
                float yaw = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
                float turn = Mathf.DeltaAngle(controller.ControlYaw, yaw);
                InputSystem.QueueDeltaStateEvent(mouse.delta, new Vector2(Mathf.Clamp(turn, -3f, 3f),
                    Mathf.Clamp(controller.ControlPitch, -2f, 2f)));
                InputSystem.QueueStateEvent(keyboard, Mathf.Abs(turn) < 15f
                    ? new KeyboardState(Key.W) : new KeyboardState());
                yield return null;
                delta = destination - pawn.Transform.position;
                delta.y = 0f;
            }
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            Debug.Log($"[Lab068] WalkDestination={destination} ActualPosition={pawn.Transform.position}");
            Assert.That(delta.magnitude, Is.LessThanOrEqualTo(0.6f), "Formal input must reach the open observation aisle.");
        }

        private static IEnumerator WaitFor(Func<bool> predicate, float seconds, string stage)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(predicate(), Is.True, stage + " timed out.");
        }

        private static bool HasClearAim(Camera camera, Pawn pawn, EnemyPresentation enemy)
        {
            Vector3 aim = enemy.transform.position + Vector3.up * 1.3f - camera.transform.position;
            return !Physics.RaycastAll(camera.transform.position, aim.normalized, aim.magnitude - 0.2f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore).Any(hit =>
                !hit.transform.IsChildOf(pawn.Transform) && !hit.transform.IsChildOf(enemy.transform));
        }
    }
}
