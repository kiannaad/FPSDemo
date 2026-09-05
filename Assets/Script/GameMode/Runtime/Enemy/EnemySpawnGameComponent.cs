using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CGame
{
    public sealed class EnemySpawnGameComponent : IDisposable
    {
        private readonly World world;
        private readonly EnemyDefinition definition;
        private readonly int initialSpawnCount;
        private readonly EnemyPawnSpawner spawner;
        private readonly List<EnemySpawnHandle> handles = new List<EnemySpawnHandle>();
        private bool disposed;

        public EnemySpawnGameComponent(
            World world,
            EnemyDefinition definition,
            int count,
            EnemyPawnSpawner spawner = null)
        {
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
            initialSpawnCount = count > 0 ? count : throw new ArgumentOutOfRangeException(nameof(count));
            this.spawner = spawner ?? new EnemyPawnSpawner();
            world.GameplayReady += OnGameplayReady;
        }

        public IReadOnlyList<EnemySpawnHandle> Handles => handles;
        public Task SpawnTask { get; private set; }
        public event Action<EnemySpawnHandle> HandleSpawned;
        public event Action<EnemySpawnHandle> HandleDisposed;

        public Task StartInitialSpawn()
        {
            if (disposed) throw new ObjectDisposedException(nameof(EnemySpawnGameComponent));
            if (SpawnTask != null) throw new InvalidOperationException("Initial enemy batch can only spawn once.");
            SpawnTask = SpawnBatchAsync();
            return SpawnTask;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            world.GameplayReady -= OnGameplayReady;
            for (int index = handles.Count - 1; index >= 0; index--) handles[index].Dispose();
            handles.Clear();
            HandleSpawned = null;
            HandleDisposed = null;
        }

        private void OnGameplayReady()
        {
            _ = ObserveInitialSpawnAsync();
        }

        private async Task ObserveInitialSpawnAsync()
        {
            try
            {
                await StartInitialSpawn();
            }
            catch
            {
                // SpawnBatchAsync reports the failure to World; observing here prevents an unobserved event task.
            }
        }

        private async Task SpawnBatchAsync()
        {
            IReadOnlyList<SpawnPointReservation> reservations =
                world.LevelRuntime.ReserveRandomEnemyPoints(initialSpawnCount, new Random(20260830));
            try
            {
                for (int index = 0; index < reservations.Count; index++)
                {
                    EnemySpawnHandle handle = await spawner.SpawnAsync(world, definition, reservations[index]);
                    if (disposed)
                    {
                        handle.Dispose();
                        return;
                    }
                    handles.Add(handle);
                    handle.Disposed += OnHandleDisposed;
                    HandleSpawned?.Invoke(handle);
                }
            }
            catch (Exception exception)
            {
                for (int index = handles.Count - 1; index >= 0; index--) handles[index].Dispose();
                handles.Clear();
                world.ReportGameplayFailure(exception);
                throw;
            }
            finally
            {
                for (int index = reservations.Count - 1; index >= 0; index--) reservations[index].Dispose();
            }
        }

        private void OnHandleDisposed()
        {
            for (int index = handles.Count - 1; index >= 0; index--)
            {
                EnemySpawnHandle handle = handles[index];
                if (!handle.IsDisposed) continue;
                handle.Disposed -= OnHandleDisposed;
                handles.RemoveAt(index);
                HandleDisposed?.Invoke(handle);
            }
        }
    }
}
