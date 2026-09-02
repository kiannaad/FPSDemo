using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace CGame
{
    [DisallowMultipleComponent]
    public class GameInstance : MonoBehaviour
    {
        [SerializeField] private WorldConfiguration worldConfiguration;
        private CancellationTokenSource initializationCancellation;

        public World RuntimeWorld { get; private set; }

        public Task InitializationTask { get; private set; }

        protected virtual void Awake()
        {
#if UNITY_SERVER
            enabled = false;
            return;
#endif
            RuntimeWorld = World.Create(worldConfiguration);
            initializationCancellation = new CancellationTokenSource();
            InitializationTask = InitializeWorldAsync(initializationCancellation.Token);
        }

        protected virtual void FixedUpdate()
        {
        }

        protected virtual void Update()
        {
            if (RuntimeWorld?.State == WorldState.Initialized && RuntimeWorld.GameMode is INetworkPrePlayExecution networkGameMode)
            {
                networkGameMode.PumpPrePlay();
            }

            RuntimeWorld?.UpdateTick(Time.deltaTime);
            RuntimeWorld?.FixedTick(Time.deltaTime);
            RuntimeWorld?.PreAnimationTick(Time.deltaTime);
        }

        protected virtual void LateUpdate()
        {
            RuntimeWorld?.LateTick(Time.deltaTime);
        }

        protected virtual void OnDestroy()
        {
            initializationCancellation?.Cancel();
            initializationCancellation?.Dispose();
            initializationCancellation = null;
            if (RuntimeWorld != null)
            {
                World world = RuntimeWorld;
                RuntimeWorld = null;
                _ = world.ShutdownAsync();
                _ = ObserveInitializationAsync(InitializationTask);
            }
        }

        private async Task InitializeWorldAsync(CancellationToken cancellationToken)
        {
            while (!gameObject.scene.isLoaded)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
            }

            await RuntimeWorld.InitializeAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (RuntimeWorld.GameMode is INetworkPrePlayExecution networkGameMode)
            {
                await networkGameMode.WaitForNetworkStartAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
            }
            RuntimeWorld.StartPlay();
        }

        private static async Task ObserveInitializationAsync(Task initializationTask)
        {
            try
            {
                if (initializationTask != null)
                {
                    await initializationTask;
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}
