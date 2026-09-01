using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CGame.GameplayCue.PlayModeTests
{
    public sealed class GameplayEnemySpawnMainlinePlayModeTests
    {
        [UnityTest]
        public IEnumerator QueueStateEvent_MovesPlayerCameraAndCapturesThreeTargets()
        {
            if (World.Current != null)
            {
                var shutdown = World.Current.ShutdownAsync();
                while (!shutdown.IsCompleted) yield return null;
            }
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Mouse mouse = InputSystem.AddDevice<Mouse>();
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
                GameInstance gameInstance = Object.FindObjectOfType<GameInstance>();
                while (gameInstance.InitializationTask == null || !gameInstance.InitializationTask.IsCompleted) yield return null;
                if (gameInstance.InitializationTask.IsFaulted) throw gameInstance.InitializationTask.Exception.GetBaseException();
                World world = gameInstance.RuntimeWorld;
                DefaultGameState gameState = (DefaultGameState)world.GameState;
                Assert.That(gameState.ExperienceManager.Components.TryGet(out EnemySpawnGameComponent enemies), Is.True);
                while (enemies.SpawnTask == null || !enemies.SpawnTask.IsCompleted) yield return null;
                if (enemies.SpawnTask.IsFaulted) throw enemies.SpawnTask.Exception.GetBaseException();
                Assert.That(enemies.Handles.Count, Is.EqualTo(3));
                for (int frame = 0; frame < 120 && !AreAllEnemiesGrounded(enemies); frame++) yield return null;
                Assert.That(AreAllEnemiesGrounded(enemies), Is.True, "All three Target enemies must settle on the ground.");

                PlayerController controller = (PlayerController)world.GameMode.PlayerController;
                Pawn pawn = controller.PossessedPawn;
                Vector3 startPosition = pawn.Transform.position;
                Quaternion startCameraRotation = pawn.GetComponent<PawnCameraComponent>().Camera.transform.rotation;

                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(30f, -12f) });
                for (int frame = 0; frame < 20; frame++) yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.QueueStateEvent(mouse, new MouseState());
                yield return null;

                Assert.That(Vector3.Distance(startPosition, pawn.Transform.position), Is.GreaterThan(0.01f));
                Assert.That(Quaternion.Angle(startCameraRotation, pawn.GetComponent<PawnCameraComponent>().Camera.transform.rotation), Is.GreaterThan(0.1f));
                Assert.That(controller.TickCount, Is.GreaterThan(0));
                for (int index = 0; index < enemies.Handles.Count; index++)
                {
                    Assert.That(enemies.Handles[index].Pawn.GetComponent<PawnMovementComponent>().Motor.GroundingStatus.IsStableOnGround, Is.True);
                }

                string capturePath = Path.Combine(Application.persistentDataPath, "GameplayEnemySpawnMainline-026.png");
                ScreenCapture.CaptureScreenshot(capturePath, 2);
                yield return new WaitForEndOfFrame();
                yield return new WaitForSeconds(0.2f);
                Assert.That(File.Exists(capturePath), Is.True, capturePath);
                Object.Destroy(gameInstance.gameObject);
                yield return null;
            }
            finally
            {
                if (keyboard.added) InputSystem.RemoveDevice(keyboard);
                if (mouse.added) InputSystem.RemoveDevice(mouse);
            }
        }

        private static bool AreAllEnemiesGrounded(EnemySpawnGameComponent enemies)
        {
            for (int index = 0; index < enemies.Handles.Count; index++)
            {
                if (!enemies.Handles[index].Pawn.GetComponent<PawnMovementComponent>().Motor.GroundingStatus.IsStableOnGround)
                    return false;
            }
            return enemies.Handles.Count == 3;
        }
    }
}
