using System;
using System.Collections.Generic;

namespace CGame
{
    public sealed class GameFeatureComponentRegistry : IDisposable
    {
        private readonly Dictionary<Type, object> components = new Dictionary<Type, object>();

        public int Count => components.Count;

        public bool TryGet<T>(out T component) where T : class
        {
            if (components.TryGetValue(typeof(T), out object value))
            {
                component = (T)value;
                return true;
            }
            component = null;
            return false;
        }

        public GameFeatureActivationReceipt Install(object component, Guid ownerId)
        {
            if (component == null) throw new ArgumentNullException(nameof(component));
            Type type = component.GetType();
            if (components.ContainsKey(type))
            {
                throw new InvalidOperationException($"GameFeature component type {type.Name} is already installed.");
            }

            components.Add(type, component);
            return new GameFeatureActivationReceipt(ownerId, () =>
            {
                if (components.TryGetValue(type, out object installed) && ReferenceEquals(installed, component))
                {
                    components.Remove(type);
                    (component as IDisposable)?.Dispose();
                }
            });
        }

        public void Dispose()
        {
            foreach (object component in components.Values) (component as IDisposable)?.Dispose();
            components.Clear();
        }
    }
}
