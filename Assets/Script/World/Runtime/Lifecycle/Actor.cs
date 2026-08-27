using System;
using System.Collections.Generic;

namespace CGame
{
    public abstract class Actor
    {
        private readonly List<ActorComponent> components = new List<ActorComponent>();
        private readonly List<TickTaskNode> tickTasks = new List<TickTaskNode>();
        private bool isDeclaringStructure;
        private bool isShutdown;

        protected Actor()
        {
            State = ActorState.Constructed;
        }

        public ActorState State { get; internal set; }

        public IReadOnlyList<ActorComponent> Components => components;

        public IReadOnlyList<TickTaskNode> TickTasks => tickTasks;

        public T GetComponent<T>() where T : ActorComponent
        {
            if (TryGetComponent(out T component))
            {
                return component;
            }

            throw new InvalidOperationException($"Actor does not contain component {typeof(T).Name}.");
        }

        public bool TryGetComponent<T>(out T component) where T : ActorComponent
        {
            for (int index = 0; index < components.Count; index++)
            {
                if (components[index] is T candidate)
                {
                    component = candidate;
                    return true;
                }
            }

            component = null;
            return false;
        }

        protected T AddComponent<T>(T component) where T : ActorComponent
        {
            if (!isDeclaringStructure)
            {
                throw new InvalidOperationException("Actor components can only be declared during Initialize.");
            }

            if (component == null)
            {
                throw new ArgumentNullException(nameof(component));
            }

            Type concreteType = component.GetType();
            for (int index = 0; index < components.Count; index++)
            {
                if (components[index].GetType() == concreteType)
                {
                    throw new InvalidOperationException($"Actor already contains component {concreteType.Name}.");
                }
            }

            components.Add(component);
            return component;
        }

        protected TickTaskNode AddTickTask(
            string name,
            TickGroup group,
            Action<float> callback,
            float tickInterval = 0f)
        {
            if (!isDeclaringStructure)
            {
                throw new InvalidOperationException("Actor tick tasks can only be declared during Initialize.");
            }

            var node = new TickTaskNode(name, group, callback, tickInterval);
            tickTasks.Add(node);
            return node;
        }

        protected virtual void OnInitialize()
        {
        }

        protected virtual void OnPostInitializeComponents()
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

        internal void InitializeActor()
        {
            isDeclaringStructure = true;
            try
            {
                OnInitialize();
            }
            finally
            {
                isDeclaringStructure = false;
            }
        }

        internal void PostInitializeComponents() => OnPostInitializeComponents();

        internal void BeginPlayActor() => OnBeginPlay();

        internal void EndPlayActor() => OnEndPlay();

        internal void ShutdownActor()
        {
            if (isShutdown)
            {
                return;
            }

            isShutdown = true;
            OnShutdown();
        }
    }
}
