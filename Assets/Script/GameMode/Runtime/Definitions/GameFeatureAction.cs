using System;
using UnityEngine;

namespace CGame
{
    public abstract class GameFeatureAction : ScriptableObject
    {
        public abstract GameFeatureActivationReceipt Activate(GameFeatureActivationContext context);
    }

    public readonly struct GameFeatureActivationContext
    {
        public GameFeatureActivationContext(object owner, Guid ownerId, string featureId)
        {
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            OwnerId = ownerId;
            FeatureId = featureId;
        }
        public object Owner { get; }
        public Guid OwnerId { get; }
        public string FeatureId { get; }

        public GameFeatureActivationReceipt InstallComponent(object component)
        {
            if (!(Owner is ExperienceManagerComponent manager))
            {
                throw new InvalidOperationException("GameFeature action owner does not support component installation.");
            }

            return manager.Components.Install(component, OwnerId);
        }
    }

    public sealed class GameFeatureActivationReceipt : IDisposable
    {
        private Action deactivate;
        public GameFeatureActivationReceipt(Guid ownerId, Action deactivateAction) { OwnerId = ownerId; deactivate = deactivateAction ?? throw new ArgumentNullException(nameof(deactivateAction)); }
        public Guid OwnerId { get; }
        public bool IsDisposed => deactivate == null;
        public void Dispose() { Action action = deactivate; deactivate = null; action?.Invoke(); }
    }
}
