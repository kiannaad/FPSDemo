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
            RuntimeWorld = World.Create(worldConfiguration);
            initializationCancellation = new CancellationTokenSource();
            InitializationTask = InitializeWorldAsync(initializationCancellation.Token);
        }

        protected virtual void FixedUpdate()
        {
            RuntimeWorld?.FixedTick(Time.fixedDeltaTime);
        }

        protected virtual void Update()
        {
            RuntimeWorld?.UpdateTick(Time.deltaTime);
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
                _ = ObserveInitializationAndShutdownAsync(InitializationTask, RuntimeWorld);
            }
        }

        private async Task InitializeWorldAsync(CancellationToken cancellationToken)
        {
            await RuntimeWorld.InitializeAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            RuntimeWorld.StartPlay();
        }

        private static async Task ObserveInitializationAndShutdownAsync(Task initializationTask, World world)
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
            finally
            {
                await world.ShutdownAsync();
            }
        }
    }
}
