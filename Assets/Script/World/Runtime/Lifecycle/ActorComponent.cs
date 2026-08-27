using System;
using System.Collections.Generic;

namespace CGame
{
    public abstract class ActorComponent
    {
        private readonly List<Type> dependencies = new List<Type>();
        private readonly List<TickTaskNode> tickTasks = new List<TickTaskNode>();
        private bool isInitializing;
        private bool structureFrozen;

        public Actor Owner { get; private set; }

        public IReadOnlyList<Type> Dependencies => dependencies;

        public IReadOnlyList<TickTaskNode> TickTasks => tickTasks;

        protected void AddDependency<T>() where T : ActorComponent
        {
            if (structureFrozen)
            {
                throw new InvalidOperationException("ActorComponent dependencies are frozen.");
            }

            Type dependency = typeof(T);
            if (!dependencies.Contains(dependency))
            {
                dependencies.Add(dependency);
            }
        }

        protected TickTaskNode AddTickTask(
            string name,
            TickGroup group,
            Action<float> callback,
            float tickInterval = 0f)
        {
            if (!isInitializing)
            {
                throw new InvalidOperationException("Tick tasks can only be declared while the component initializes.");
            }

            var node = new TickTaskNode(name, group, callback, tickInterval);
            tickTasks.Add(node);
            return node;
        }

        protected virtual void OnInitialize()
        {
        }

        protected virtual void OnBeginPlay()
        {
        }

        protected virtual void OnEndPlay()
        {
        }

        protected virtual void OnShutdown()
        {
        }

        internal void Attach(Actor owner)
        {
            if (Owner != null)
            {
                throw new InvalidOperationException($"{GetType().Name} is already attached to an Actor.");
            }

            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            structureFrozen = true;
        }

        internal void InitializeComponent()
        {
            isInitializing = true;
            try
            {
                OnInitialize();
            }
            finally
            {
                isInitializing = false;
            }
        }

        internal void BeginPlayComponent() => OnBeginPlay();

        internal void EndPlayComponent() => OnEndPlay();

        internal void ShutdownComponent() => OnShutdown();
    }
}
