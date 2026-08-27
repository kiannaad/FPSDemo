using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CGame
{
    public abstract class SubSystem
    {
        private readonly List<Type> dependencies = new List<Type>();
        private readonly List<TickTaskNode> tickTasks = new List<TickTaskNode>();
        private bool isInitializing;
        private bool structureFrozen;

        public object Owner { get; private set; }

        public IReadOnlyList<Type> Dependencies => dependencies;

        public IReadOnlyList<TickTaskNode> TickTasks => tickTasks;

        protected void AddDependency<T>() where T : SubSystem
        {
            if (structureFrozen)
            {
                throw new InvalidOperationException("SubSystem dependencies are frozen after attachment.");
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
                throw new InvalidOperationException("Tick tasks can only be declared while the SubSystem initializes.");
            }

            var node = new TickTaskNode(name, group, callback, tickInterval);
            tickTasks.Add(node);
            return node;
        }

        protected virtual Task OnInitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        protected virtual void OnBeginPlay()
        {
        }

        protected virtual void OnEndPlay()
        {
        }

        protected virtual Task OnShutdownAsync() => Task.CompletedTask;

        internal void Attach(object owner)
        {
            if (Owner != null)
            {
                throw new InvalidOperationException($"{GetType().Name} is already attached to an Owner.");
            }

            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            structureFrozen = true;
        }

        internal async Task InitializeSubSystemAsync(CancellationToken cancellationToken)
        {
            isInitializing = true;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                await OnInitializeAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
            }
            finally
            {
                isInitializing = false;
            }
        }

        internal void BeginPlaySubSystem() => OnBeginPlay();

        internal void EndPlaySubSystem() => OnEndPlay();

        internal Task ShutdownSubSystemAsync() => OnShutdownAsync();
    }
}
