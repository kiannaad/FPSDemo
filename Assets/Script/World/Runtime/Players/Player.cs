using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CGame
{
    public sealed class Player
    {
        private readonly Dictionary<Type, PlayerSubSystem> subSystemsByType =
            new Dictionary<Type, PlayerSubSystem>();
        private readonly List<PlayerSubSystem> orderedSubSystems = new List<PlayerSubSystem>();
        private readonly List<PlayerSubSystem> initializedSubSystems = new List<PlayerSubSystem>();
        private readonly List<PlayerSubSystem> begunSubSystems = new List<PlayerSubSystem>();
        private readonly Dictionary<PlayerSubSystem, IDisposable> tickRegistrations =
            new Dictionary<PlayerSubSystem, IDisposable>();

        internal Player(IEnumerable<PlayerSubSystem> subSystems)
        {
            AddSubSystems(subSystems ?? Array.Empty<PlayerSubSystem>());
            BuildSubSystemOrder();
        }

        public Controller Controller { get; private set; }

        public T GetSubSystem<T>() where T : PlayerSubSystem
        {
            return subSystemsByType.TryGetValue(typeof(T), out PlayerSubSystem subSystem)
                ? (T)subSystem
                : null;
        }

        internal async Task InitializeAsync(TickTaskManager tickTaskManager, CancellationToken cancellationToken)
        {
            for (int index = 0; index < orderedSubSystems.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                PlayerSubSystem subSystem = orderedSubSystems[index];
                subSystem.Attach(this);
                await subSystem.InitializeSubSystemAsync(cancellationToken);
                initializedSubSystems.Add(subSystem);
                tickRegistrations.Add(
                    subSystem,
                    tickTaskManager.RegisterOwner(subSystem, subSystem.TickTasks, critical: true));
            }
        }

        internal void BeginPlay(TickTaskManager tickTaskManager)
        {
            for (int index = 0; index < orderedSubSystems.Count; index++)
            {
                PlayerSubSystem subSystem = orderedSubSystems[index];
                subSystem.BeginPlaySubSystem();
                begunSubSystems.Add(subSystem);
                tickTaskManager.SetOwnerEnabled(subSystem, true);
            }
        }

        internal async Task ShutdownAsync(TickTaskManager tickTaskManager)
        {
            for (int index = begunSubSystems.Count - 1; index >= 0; index--)
            {
                PlayerSubSystem subSystem = begunSubSystems[index];
                try
                {
                    tickTaskManager.SetOwnerEnabled(subSystem, false);
                    subSystem.EndPlaySubSystem();
                }
                catch
                {
                }
            }

            begunSubSystems.Clear();
            Controller = null;
            for (int index = initializedSubSystems.Count - 1; index >= 0; index--)
            {
                PlayerSubSystem subSystem = initializedSubSystems[index];
                if (tickRegistrations.TryGetValue(subSystem, out IDisposable registration))
                {
                    registration.Dispose();
                    tickRegistrations.Remove(subSystem);
                }

                try
                {
                    await subSystem.ShutdownSubSystemAsync();
                }
                catch
                {
                }
            }

            initializedSubSystems.Clear();
        }

        internal void AttachController(Controller controller)
        {
            if (controller == null || !ReferenceEquals(controller.Player, this))
            {
                throw new InvalidOperationException("Controller belongs to another Player.");
            }

            if (Controller != null)
            {
                throw new InvalidOperationException("Player already owns a Controller.");
            }

            Controller = controller;
        }

        private void AddSubSystems(IEnumerable<PlayerSubSystem> subSystems)
        {
            foreach (PlayerSubSystem subSystem in subSystems)
            {
                if (subSystem == null || subSystemsByType.ContainsKey(subSystem.GetType()))
                {
                    throw new InvalidOperationException("PlayerSubSystem must be non-null and unique by concrete type.");
                }

                subSystemsByType.Add(subSystem.GetType(), subSystem);
            }
        }

        private void BuildSubSystemOrder()
        {
            var visiting = new HashSet<Type>();
            var visited = new HashSet<Type>();
            foreach (PlayerSubSystem subSystem in subSystemsByType.Values)
            {
                Visit(subSystem, visiting, visited);
            }
        }

        private void Visit(PlayerSubSystem subSystem, HashSet<Type> visiting, HashSet<Type> visited)
        {
            Type type = subSystem.GetType();
            if (visited.Contains(type))
            {
                return;
            }

            if (!visiting.Add(type))
            {
                throw new InvalidOperationException($"PlayerSubSystem dependency cycle includes {type.Name}.");
            }

            for (int index = 0; index < subSystem.Dependencies.Count; index++)
            {
                Type dependencyType = subSystem.Dependencies[index];
                if (!subSystemsByType.TryGetValue(dependencyType, out PlayerSubSystem dependency))
                {
                    throw new InvalidOperationException($"PlayerSubSystem {type.Name} requires missing {dependencyType.Name}.");
                }

                Visit(dependency, visiting, visited);
            }

            visiting.Remove(type);
            visited.Add(type);
            orderedSubSystems.Add(subSystem);
        }
    }
}
