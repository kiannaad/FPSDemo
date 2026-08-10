using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace CGame
{
    [DisallowMultipleComponent]
    public sealed class WorldBehaviour : MonoBehaviour
    {
        private const float FirstPersonNearClipPlane = 0.03f;
        [SerializeField] private ScriptableObject gameBootstrap;
        [SerializeField] private Camera playerCamera;
        private IGameplaySceneBootstrap gameplaySceneBootstrap;
        private CursorLockMode previousCursorLockMode;
        private bool previousCursorVisible;
        private bool ownsCursorLock;

        public World RuntimeWorld { get; private set; }

        public Task<WorldStartResult> InitializationTask { get; private set; }

        public Camera PlayerCamera => playerCamera;

        private void Awake()
        {
            if (gameBootstrap == null)
            {
                return;
            }

            if (!(gameBootstrap is IWorldBootstrapFactory bootstrapFactory))
            {
                throw new InvalidOperationException(
                    $"{gameBootstrap.name} does not implement {nameof(IWorldBootstrapFactory)}.");
            }

            gameplaySceneBootstrap = gameBootstrap as IGameplaySceneBootstrap;
            if (playerCamera != null)
            {
                playerCamera.nearClipPlane = Mathf.Min(
                    playerCamera.nearClipPlane,
                    FirstPersonNearClipPlane);
            }

            previousCursorLockMode = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            ownsCursorLock = true;

            Task<WorldStartResult> startTask = StartWorld(
                bootstrapFactory.CreateCoreServices(),
                bootstrapFactory.CreateLauncher(),
                bootstrapFactory.CreateCharacterMotorSimulation());
            if (bootstrapFactory is IGameSessionBootstrapFactory sessionBootstrapFactory)
            {
                InitializationTask = StartGameplaySessionAfterLaunchAsync(
                    startTask,
                    sessionBootstrapFactory);
            }
        }

        public Task<WorldStartResult> StartWorld(
            IEnumerable<IWorldCoreService> coreServices,
            GameLauncher gameLauncher,
            ICharacterMotorSimulation characterMotorSimulation = null)
        {
            if (InitializationTask != null)
            {
                throw new InvalidOperationException("WorldBehaviour has already started a World.");
            }

            RuntimeWorld = World.Create(
                coreServices,
                gameLauncher,
                characterMotorSimulation: characterMotorSimulation);
            InitializationTask = RuntimeWorld.StartAsync();
            return InitializationTask;
        }

        private void FixedUpdate()
        {
            RuntimeWorld?.FixedTick(Time.fixedDeltaTime);
        }

        private void Update()
        {
            RuntimeWorld?.UpdateTick(Time.deltaTime);
        }

        private void LateUpdate()
        {
            RuntimeWorld?.LateTick(Time.deltaTime, Time.time);
            UpdatePlayerCameraPresentation();
        }

        private void OnDestroy()
        {
            if (ownsCursorLock)
            {
                Cursor.lockState = previousCursorLockMode;
                Cursor.visible = previousCursorVisible;
                ownsCursorLock = false;
            }

            if (RuntimeWorld == null)
            {
                return;
            }

            Task shutdownTask = RuntimeWorld.ShutdownAsync();
            if (!shutdownTask.IsCompleted)
            {
                _ = ObserveShutdownAsync(shutdownTask);
            }
        }

        private static async Task ObserveShutdownAsync(Task shutdownTask)
        {
            try
            {
                await shutdownTask;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private async Task<WorldStartResult> StartGameplaySessionAfterLaunchAsync(
            Task<WorldStartResult> startTask,
            IGameSessionBootstrapFactory sessionBootstrapFactory)
        {
            WorldStartResult startResult = await startTask;
            if (!startResult.Succeeded)
            {
                return startResult;
            }

            IGameSessionFactory sessionFactory = sessionBootstrapFactory.CreateGameSessionFactory();
            if (sessionFactory == null)
            {
                return startResult;
            }

            GameSessionStartResult sessionResult = RuntimeWorld.StartGame(sessionFactory);
            if (!sessionResult.Succeeded)
            {
                return WorldStartResult.Fail(sessionResult.Failure);
            }

            return gameplaySceneBootstrap?.StartScene(RuntimeWorld, startResult) ?? startResult;
        }

        private void UpdatePlayerCameraPresentation()
        {
            if (playerCamera == null
                || gameplaySceneBootstrap == null
                || !gameplaySceneBootstrap.TryGetPlayerCameraPose(
                    out Vector3 position,
                    out Quaternion rotation))
            {
                return;
            }

            playerCamera.transform.SetPositionAndRotation(position, rotation);
        }
    }
}
