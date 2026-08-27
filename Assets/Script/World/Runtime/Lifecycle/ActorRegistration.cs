using System;
using System.Collections.Generic;

namespace CGame
{
    public sealed class ActorRegistration : IDisposable
    {
        private readonly TickTaskManager tickTaskManager;
        private readonly List<ActorComponent> orderedComponents = new List<ActorComponent>();
        private readonly List<ActorComponent> initializedComponents = new List<ActorComponent>();
        private readonly List<ActorComponent> begunComponents = new List<ActorComponent>();
        private IDisposable tickRegistration;
        private bool actorBeganPlay;
        private bool disposed;

        private ActorRegistration(Actor actor, TickTaskManager tickTaskManager, bool critical)
        {
            Actor = actor;
            this.tickTaskManager = tickTaskManager;
            Critical = critical;
        }

        public Actor Actor { get; }

        public bool Critical { get; }

        public bool IsDisposed => disposed;

        internal event Action<ActorRegistration> Disposed;

        public static ActorRegistration Register(Actor actor, TickTaskManager tickTaskManager, bool critical = false)
        {
            if (actor == null)
            {
                throw new ArgumentNullException(nameof(actor));
            }

            if (tickTaskManager == null)
            {
                throw new ArgumentNullException(nameof(tickTaskManager));
            }

            if (actor.State != ActorState.Constructed)
            {
                throw new InvalidOperationException($"Actor cannot register from state {actor.State}.");
            }

            var registration = new ActorRegistration(actor, tickTaskManager, critical);
            registration.Initialize();
            return registration;
        }

        public void Activate()
        {
            EnsureNotDisposed();
            if (Actor.State != ActorState.Initialized)
            {
                throw new InvalidOperationException($"Actor cannot activate from state {Actor.State}.");
            }

            Actor.State = ActorState.ActivationPending;
            try
            {
                for (int index = 0; index < orderedComponents.Count; index++)
                {
                    ActorComponent component = orderedComponents[index];
                    component.BeginPlayComponent();
                    begunComponents.Add(component);
                }

                Actor.BeginPlayActor();
                actorBeganPlay = true;
                Actor.State = ActorState.Playing;
                tickTaskManager.SetOwnerEnabled(Actor, true);
            }
            catch
            {
                RollbackBeginPlay();
                DisposeInternal();
                throw;
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            DisposeInternal();
        }

        private void Initialize()
        {
            Actor.State = ActorState.Registered;
            Actor.State = ActorState.Initializing;
            try
            {
                Actor.InitializeActor();
                BuildComponentOrder();
                for (int index = 0; index < orderedComponents.Count; index++)
                {
                    ActorComponent component = orderedComponents[index];
                    component.Attach(Actor);
                    component.InitializeComponent();
                    initializedComponents.Add(component);
                }

                Actor.PostInitializeComponents();
                List<TickTaskNode> nodes = CollectTickTasks();
                tickRegistration = tickTaskManager.RegisterOwner(Actor, nodes, Critical);
                tickTaskManager.OwnerFaulted += OnOwnerFaulted;
                Actor.State = ActorState.Initialized;
            }
            catch
            {
                Actor.State = ActorState.InitializationFaulted;
                ShutdownInitializedComponents();
                Actor.ShutdownActor();
                tickRegistration?.Dispose();
                tickRegistration = null;
                Actor.State = ActorState.Unregistered;
                disposed = true;
                throw;
            }
        }

        private void BuildComponentOrder()
        {
            var componentsByType = new Dictionary<Type, ActorComponent>();
            for (int index = 0; index < Actor.Components.Count; index++)
            {
                ActorComponent component = Actor.Components[index];
                Type type = component.GetType();
                if (componentsByType.ContainsKey(type))
                {
                    throw new InvalidOperationException($"Actor contains duplicate component {type.Name}.");
                }

                componentsByType.Add(type, component);
            }

            var visiting = new HashSet<Type>();
            var visited = new HashSet<Type>();
            for (int index = 0; index < Actor.Components.Count; index++)
            {
                VisitComponent(Actor.Components[index], componentsByType, visiting, visited);
            }
        }

        private void VisitComponent(
            ActorComponent component,
            Dictionary<Type, ActorComponent> componentsByType,
            HashSet<Type> visiting,
            HashSet<Type> visited)
        {
            Type type = component.GetType();
            if (visited.Contains(type))
            {
                return;
            }

            if (!visiting.Add(type))
            {
                throw new InvalidOperationException($"ActorComponent dependency cycle includes {type.Name}.");
            }

            for (int index = 0; index < component.Dependencies.Count; index++)
            {
                Type dependencyType = component.Dependencies[index];
                if (!componentsByType.TryGetValue(dependencyType, out ActorComponent dependency))
                {
                    throw new InvalidOperationException($"ActorComponent {type.Name} requires missing {dependencyType.Name}.");
                }

                VisitComponent(dependency, componentsByType, visiting, visited);
            }

            visiting.Remove(type);
            visited.Add(type);
            orderedComponents.Add(component);
        }

        private List<TickTaskNode> CollectTickTasks()
        {
            var result = new List<TickTaskNode>();
            for (int index = 0; index < Actor.TickTasks.Count; index++)
            {
                result.Add(Actor.TickTasks[index]);
            }

            for (int componentIndex = 0; componentIndex < orderedComponents.Count; componentIndex++)
            {
                IReadOnlyList<TickTaskNode> componentTasks = orderedComponents[componentIndex].TickTasks;
                for (int taskIndex = 0; taskIndex < componentTasks.Count; taskIndex++)
                {
                    result.Add(componentTasks[taskIndex]);
                }
            }

            return result;
        }

        private void OnOwnerFaulted(TickTaskFault fault)
        {
            if (disposed || !ReferenceEquals(fault.Owner, Actor))
            {
                return;
            }

            Actor.State = ActorState.TickFaulted;
            DisposeInternal();
        }

        private void DisposeInternal()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            tickTaskManager.OwnerFaulted -= OnOwnerFaulted;
            if (Actor.State == ActorState.Playing || Actor.State == ActorState.TickFaulted || actorBeganPlay || begunComponents.Count > 0)
            {
                Actor.State = ActorState.EndingPlay;
                if (actorBeganPlay)
                {
                    Actor.EndPlayActor();
                    actorBeganPlay = false;
                }

                EndBegunComponents();
                Actor.State = ActorState.Ended;
            }

            tickRegistration?.Dispose();
            tickRegistration = null;
            ShutdownInitializedComponents();
            Actor.ShutdownActor();
            Actor.State = ActorState.Unregistered;
            Disposed?.Invoke(this);
            Disposed = null;
        }

        private void RollbackBeginPlay()
        {
            if (actorBeganPlay)
            {
                Actor.EndPlayActor();
                actorBeganPlay = false;
            }

            EndBegunComponents();
        }

        private void EndBegunComponents()
        {
            for (int index = begunComponents.Count - 1; index >= 0; index--)
            {
                begunComponents[index].EndPlayComponent();
            }

            begunComponents.Clear();
        }

        private void ShutdownInitializedComponents()
        {
            for (int index = initializedComponents.Count - 1; index >= 0; index--)
            {
                initializedComponents[index].ShutdownComponent();
            }

            initializedComponents.Clear();
        }

        private void EnsureNotDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(ActorRegistration));
            }
        }
    }
}
